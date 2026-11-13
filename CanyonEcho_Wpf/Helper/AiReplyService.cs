using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using CanyonEcho_Wpf.ViewModels;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// AI 阴阳怪气反击生成服务（支持 OpenAI 兼容接口 + 内置离线引擎无缝切换）
    /// </summary>
    public class AiReplyService
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        /// <summary>
        /// 根据 OCR 文本生成阴阳怪气反击语录
        /// </summary>
        public static async Task<(string Reply, string SourceDesc, LolChatParseResult ParseInfo)> GenerateCounterReplyAsync(
            string rawOcrText,
            AiConfig config,
            WordsLibrary library = null)
        {
            config = config ?? AiConfig.Load();
            var roster = await LiveClientDataService.GetCurrentMatchRosterAsync();
            var parseResult = LolChatParser.Parse(rawOcrText, roster);

            // 若开启了“仅针对恶意评论反击”且未检测到恶意特征，则返回空提示
            if (config.OnlyReplyToMalicious && !parseResult.IsMalicious)
            {
                return ("", $"未检测到明显恶意特征（识别文本：{parseResult.CleanMessage}）", parseResult);
            }

            string targetText = !string.IsNullOrWhiteSpace(parseResult.CleanMessage)
                ? parseResult.CleanMessage
                : rawOcrText?.Trim();

            // 1. 纯内置离线引擎模式
            if (config.EngineMode == ReplyEngineMode.BuiltInOnly)
            {
                string builtInReply = BuiltInSnarkyEngine.GenerateReply(parseResult, library, config.UseRoastMode);
                return (builtInReply, $"内置引擎{(config.UseRoastMode ? "[粗口Roast]" : "")} [{parseResult.IntentCategory}]", parseResult);
            }

            // 2. LLM 模式 或 智能混合模式（且已配置 API Key 或使用本地 Ollama）
            bool hasApiKey = !string.IsNullOrWhiteSpace(config.ApiKey) ||
                             (config.ApiBaseUrl != null && config.ApiBaseUrl.Contains("localhost"));

            if (hasApiKey)
            {
                try
                {
                    string llmReply = await CallOpenAiCompatibleApiAsync(parseResult, targetText, config);
                    if (!string.IsNullOrWhiteSpace(llmReply))
                    {
                        string cleaned = CleanAiReply(llmReply);
                        // v4.2.0: LLM 也可能吐出原形脏话, 在这里先隐形一次, 发送时还会再隐形一次
                        // (SwearObfuscator 是幂等的, 第二次不再变化)。
                        if (config.UseRoastMode && config.UseSwearObfuscation)
                            cleaned = SwearObfuscator.Obfuscate(cleaned);
                        return (cleaned, $"AI大模型 ({config.ModelName}){(config.UseRoastMode ? "[隐形Roast]" : "")}", parseResult);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Log.Warn("调用 LLM 接口失败，准备切换内置引擎", ex);
                    if (config.EngineMode == ReplyEngineMode.LlmOnly)
                    {
                        throw new Exception($"LLM 接口调用失败：{ex.Message}", ex);
                    }
                }
            }
            else if (config.EngineMode == ReplyEngineMode.LlmOnly)
            {
                throw new Exception("当前为【仅使用 LLM 大模型】模式，请先在 AI 设置中填写 API Key，或切换为【智能混合/内置离线】模式！");
            }

            // 3. 智能降级到内置离线阴阳怪气引擎
            string fallbackReply = BuiltInSnarkyEngine.GenerateReply(parseResult, library, config.UseRoastMode);
            return (fallbackReply, $"内置引擎兜底{(config.UseRoastMode ? "[粗口Roast]" : "")} [{parseResult.IntentCategory}]", parseResult);
        }

        private static async Task<string> CallOpenAiCompatibleApiAsync(
            LolChatParseResult parseResult,
            string enemyText,
            AiConfig config)
        {
            string baseUrl = (config.ApiBaseUrl ?? "https://api.deepseek.com/v1").TrimEnd('/');
            string endpoint = baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
                ? baseUrl
                : $"{baseUrl}/chat/completions";

            string statsInfo = parseResult?.MatchedPlayer != null
                ? $"实时数据：[{parseResult.MatchedPlayer.FormatStatsSummary()}]；"
                : "";

            string speakerContext = (parseResult != null && parseResult.IsAccompliceDefending)
                ? $"【多人帮腔护短场景】主目标：{parseResult.LockedPrimaryTarget}，跳出来帮腔的人：{parseResult.AccompliceSpeaker}；{statsInfo}基于你对他俩的一切了解一起往死里喷，中文俚语+大量脏话拉满，别收着！"
                : (!string.IsNullOrWhiteSpace(parseResult?.Speaker)
                    ? $"发言人/英雄：{parseResult.Speaker}；{statsInfo}意图分类：{parseResult.IntentCategory}；"
                    : $"{statsInfo}意图分类：{parseResult?.IntentCategory ?? "游戏嘲讽"}；");

            // Roast 模式 (默认开): 基于你对发言人的一切了解往死里喷, 中文俚语+大量脏话,
            // 别收着。关掉 UseRoastMode 才回到干净阴阳怪气。
            string roastSuffix = config.UseRoastMode
                ? "Based on everything you know about this player (英雄、KDA战绩、补刀、阵亡状态、意图、原话), roast and don't hold back. Say it in Chinese slang, add way more Chinese swear words (必须用隐形写法)，40字以内，只输出喷人的话本身："
                : "请生成一句针对性的阴阳怪气反击（28字以内，不带脏字，只输出反击句子本身）：";
            string userPrompt = string.IsNullOrWhiteSpace(enemyText)
                ? (config.UseRoastMode ? "对面在公屏发问号嘲讽我们，往死里喷他，别收着，脏话拉满。" : "对面在公屏发问号嘲讽我们，请给出一句高情商阴阳怪气反击。")
                : $"{speakerContext}对方在英雄联盟聊天框说：“{enemyText}”。{roastSuffix}";

            var payload = new
            {
                model = string.IsNullOrWhiteSpace(config.ModelName) ? "deepseek-chat" : config.ModelName.Trim(),
                messages = new[]
                {
                    new { role = "system", content = config.GetEffectiveSystemPrompt() },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.95,
                max_tokens = 120
            };

            string jsonBody = JsonConvert.SerializeObject(payload);
            // v4.3.0: 原来只发一次, 429 限流/5xx/12 秒超时直接抛, 调用方立刻
            // 降级内置引擎, 对面刷屏时 LLM 基本等于摆设。改成最多 3 次:
            // 只重试 429/5xx/网络异常/超时, 400/401/403/404 (Key 错了、模型
            // 不存在) 重试也没用, 直接抛。退避 500ms、1000ms, 别一上来就猛冲。
            Exception lastError = null;
            bool fatalHttpError = false;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                    {
                        if (!string.IsNullOrWhiteSpace(config.ApiKey))
                        {
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey.Trim());
                        }
                        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                        using (var response = await _httpClient.SendAsync(request))
                        {
                            string respContent = await response.Content.ReadAsStringAsync();
                            if (response.IsSuccessStatusCode)
                            {
                                var jObj = JObject.Parse(respContent);
                                return jObj["choices"]?[0]?["message"]?["content"]?.ToString();
                            }

                            int status = (int)response.StatusCode;
                            bool retryable = status == 429 || status >= 500;
                            if (!retryable)
                            {
                                // 400/401/403/404: Key 错了、模型不存在, 重试 100 次也没用。
                                fatalHttpError = true;
                                throw new HttpRequestException($"HTTP {status}: {respContent}");
                            }
                            if (attempt == 2)
                                throw new HttpRequestException($"HTTP {status}: {respContent}");
                            lastError = new HttpRequestException($"HTTP {status}: {respContent}");
                        }
                    }
                }
                catch (TaskCanceledException ex)
                {
                    // 超时 (12 秒) 当可重试, 最后一试再抛。
                    if (attempt == 2) throw;
                    lastError = ex;
                }
                catch (HttpRequestException ex)
                {
                    // 400/401/403/404 直接抛, 别重试; 最后一试也直接抛。
                    if (fatalHttpError || attempt == 2) throw;
                    lastError = ex;
                }

                LogHelper.Log.Warn($"LLM 调用失败, {500 * (attempt + 1)}ms 后重试 (attempt {attempt + 1}/3)", lastError);
                await Task.Delay(500 * (attempt + 1));
            }
            throw lastError ?? new HttpRequestException("LLM 调用失败 (重试 3 次后仍失败)");
        }

        private static string CleanAiReply(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            // 移除 <think>...</think> 标签（兼容 DeepSeek-R1 等推理模型）
            int thinkEnd = raw.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
            if (thinkEnd >= 0)
            {
                raw = raw.Substring(thinkEnd + 8);
            }

            string cleaned = raw.Trim()
                .Trim('"', '“', '”', ''', '‘', '’', '「', '」')
                .Replace("
", "")
                .Replace("
", " ");

            // Roast 模式放宽到 80 字 (原来 60 字会把脏话骂到一半截断)。
            // v4.2.1: Substring(0, 80) 会在第 80 个 char 正好是代理对高位时把它拆开,
            // 跟 Bug 5 同款。改成按字素簇截, 宁可少一个字, 不发半个 emoji。
            int maxLen = 80;
            if (cleaned.Length > maxLen)
            {
                int cut = maxLen;
                if (cut > 0 && cut < cleaned.Length
                    && char.IsHighSurrogate(cleaned[cut - 1]) && char.IsLowSurrogate(cleaned[cut]))
                {
                    cut -= 1;
                }
                cleaned = cleaned.Substring(0, cut);
            }
            return cleaned;
        }
    }
}
