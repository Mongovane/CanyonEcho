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
                string builtInReply = BuiltInSnarkyEngine.GenerateReply(parseResult, library);
                return (builtInReply, $"内置引擎 [{parseResult.IntentCategory}]", parseResult);
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
                        return (CleanAiReply(llmReply), $"AI大模型 ({config.ModelName})", parseResult);
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
            string fallbackReply = BuiltInSnarkyEngine.GenerateReply(parseResult, library);
            return (fallbackReply, $"内置引擎兜底 [{parseResult.IntentCategory}]", parseResult);
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

            string speakerContext = (parseResult != null && parseResult.IsAccompliceDefending)
                ? $"【多人帮腔护短场景】主目标：{parseResult.LockedPrimaryTarget}，跳出来帮腔的人：{parseResult.AccompliceSpeaker}；请用一句幽默高情商的话同时调侃他们双排护短/请辩护律师的行为！"
                : (!string.IsNullOrWhiteSpace(parseResult?.Speaker)
                    ? $"发言人/英雄：{parseResult.Speaker}；意图分类：{parseResult.IntentCategory}；"
                    : $"意图分类：{parseResult?.IntentCategory ?? "游戏嘲讽"}；");

            string userPrompt = string.IsNullOrWhiteSpace(enemyText)
                ? "对面在公屏发问号嘲讽我们，请给出一句高情商阴阳怪气反击。"
                : $"{speakerContext}对方在英雄联盟聊天框说：“{enemyText}”。请立即生成一句针对性的阴阳怪气反击（28字以内，不带脏字不违规，只输出反击句子本身）：";

            var payload = new
            {
                model = string.IsNullOrWhiteSpace(config.ModelName) ? "deepseek-chat" : config.ModelName.Trim(),
                messages = new[]
                {
                    new { role = "system", content = config.GetEffectiveSystemPrompt() },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.85,
                max_tokens = 80
            };

            string jsonBody = JsonConvert.SerializeObject(payload);
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
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {respContent}");
                    }

                    var jObj = JObject.Parse(respContent);
                    return jObj["choices"]?[0]?["message"]?["content"]?.ToString();
                }
            }
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

            if (cleaned.Length > 60)
            {
                cleaned = cleaned.Substring(0, 60);
            }
            return cleaned;
        }
    }
}
