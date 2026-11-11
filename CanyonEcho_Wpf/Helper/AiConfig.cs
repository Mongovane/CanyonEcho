using Newtonsoft.Json;
using Prism.Mvvm;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// 反击引擎模式
    /// </summary>
    public enum ReplyEngineMode
    {
        /// <summary>
        /// 智能混合模式：优先调用 LLM，若未配置 Key 或网络超时则自动 0 延迟切换内置阴阳怪气引擎
        /// </summary>
        HybridAuto = 0,

        /// <summary>
        /// 仅使用 LLM 大模型
        /// </summary>
        LlmOnly = 1,

        /// <summary>
        /// 纯内置离线高情商阴阳怪气引擎（免 API Key）
        /// </summary>
        BuiltInOnly = 2
    }

    /// <summary>
    /// 阴阳怪气人设风格预设
    /// </summary>
    public enum SnarkyStylePreset
    {
        GreenTea = 0,
        PoliteScholar = 1,
        KindPhilanthropist = 2,
        CanyonPoet = 3,
        Custom = 4
    }

    /// <summary>
    /// AI 与 OCR 配置模型（使用 Windows DPAPI 加密保护本地存储的 API Key）
    /// </summary>
    public class AiConfig : BindableBase
    {
        private static readonly byte[] _dpapiEntropy = Encoding.UTF8.GetBytes("CanyonEcho_DPAPI_Salt_v4");

        private ReplyEngineMode _EngineMode = ReplyEngineMode.HybridAuto;
        public ReplyEngineMode EngineMode
        {
            get => _EngineMode;
            set => SetProperty(ref _EngineMode, value);
        }

        private SnarkyStylePreset _StylePreset = SnarkyStylePreset.GreenTea;
        public SnarkyStylePreset StylePreset
        {
            get => _StylePreset;
            set => SetProperty(ref _StylePreset, value);
        }

        private string _ApiBaseUrl = "https://api.deepseek.com/v1";
        public string ApiBaseUrl
        {
            get => _ApiBaseUrl;
            set => SetProperty(ref _ApiBaseUrl, value);
        }

        private string _ApiKey = "";
        /// <summary>
        /// 内存中的明文 API Key（不直接写入 JSON，通过 EncryptedApiKey 进行 DPAPI 加密存储）
        /// </summary>
        [JsonIgnore]
        public string ApiKey
        {
            get => _ApiKey;
            set => SetProperty(ref _ApiKey, value);
        }

        /// <summary>
        /// 落盘存储的 DPAPI 密文
        /// </summary>
        public string EncryptedApiKey
        {
            get => EncryptKey(_ApiKey);
            set => _ApiKey = DecryptKey(value);
        }

        private string _ModelName = "deepseek-chat";
        public string ModelName
        {
            get => _ModelName;
            set => SetProperty(ref _ModelName, value);
        }

        private string _CustomSystemPrompt = "你是一个英雄联盟高情商阴阳怪气大师。请针对以下游戏聊天框中队友或对手发的恶意言论，生成一句简短、犀利、极其阴阳怪气但绝不包含任何脏字或违规敏感词的反击回复。字数严格控制在28字以内，直接输出回复内容，不要加引号或解释。";
        public string CustomSystemPrompt
        {
            get => _CustomSystemPrompt;
            set => SetProperty(ref _CustomSystemPrompt, value);
        }

        private bool _OnlyReplyToMalicious = false;
        public bool OnlyReplyToMalicious
        {
            get => _OnlyReplyToMalicious;
            set => SetProperty(ref _OnlyReplyToMalicious, value);
        }

        private bool _AutoSendToGame = true;
        public bool AutoSendToGame
        {
            get => _AutoSendToGame;
            set => SetProperty(ref _AutoSendToGame, value);
        }

        private bool _UseFastClipboardSend = true;
        /// <summary>
        /// 极速剪贴板防吞字发送模式（开启后通过备份剪贴板 + Ctrl+V 发送，解决原版中文输入法吞字与慢速问题）
        /// </summary>
        public bool UseFastClipboardSend
        {
            get => _UseFastClipboardSend;
            set => SetProperty(ref _UseFastClipboardSend, value);
        }

        private bool _UseZeroWidthAntiFilter = false;
        /// <summary>
        /// 单行防屏蔽穿插模式（在字间自动插入零宽不可见分隔符，无需纵向刷屏即可绕过聊天敏感词过滤）
        /// </summary>
        public bool UseZeroWidthAntiFilter
        {
            get => _UseZeroWidthAntiFilter;
            set => SetProperty(ref _UseZeroWidthAntiFilter, value);
        }

        private bool _ShowInGameOverlay = true;
        /// <summary>
        /// 是否显示外部安全游戏内透明悬浮窗 HUD (F7 切换)
        /// </summary>
        public bool ShowInGameOverlay
        {
            get => _ShowInGameOverlay;
            set => SetProperty(ref _ShowInGameOverlay, value);
        }

        private bool _RememberLastSnipRegion = true;
        public bool RememberLastSnipRegion
        {
            get => _RememberLastSnipRegion;
            set => SetProperty(ref _RememberLastSnipRegion, value);
        }

        private int _ChatBoxX = 0;
        public int ChatBoxX { get => _ChatBoxX; set => SetProperty(ref _ChatBoxX, value); }

        private int _ChatBoxY = 0;
        public int ChatBoxY { get => _ChatBoxY; set => SetProperty(ref _ChatBoxY, value); }

        private int _ChatBoxWidth = 0;
        public int ChatBoxWidth { get => _ChatBoxWidth; set => SetProperty(ref _ChatBoxWidth, value); }

        private int _ChatBoxHeight = 0;
        public int ChatBoxHeight { get => _ChatBoxHeight; set => SetProperty(ref _ChatBoxHeight, value); }

        public string GetEffectiveSystemPrompt()
        {
            switch (StylePreset)
            {
                case SnarkyStylePreset.GreenTea:
                    return "你是一个英雄联盟资深绿茶/茶言茶语阴阳大师。面对游戏聊天框里的喷子或甩锅言论，请用最温柔、最体贴、最委屈的语气说出最扎心、最阴阳怪气的话（例如：'哥哥别生气呀，虽然你技能全空，但姿势真的很帅呢'）。绝不带脏字以防封号，严格控制在28字以内，只输出回复正文。";
                case SnarkyStylePreset.PoliteScholar:
                    return "你是一个英雄联盟峡谷理智怪与温和学者。面对聊天框里的恶意喷人或嘲讽，请用冷静、客观、充满学术关怀的口吻进行降维打击式阴阳怪气反讽。绝不带脏字或违规词，严格控制在28字以内，只输出回复正文。";
                case SnarkyStylePreset.KindPhilanthropist:
                    return "你是一个英雄联盟大善人。面对聊天框里的喷子，请用充满慈爱、关爱弱势群体的口吻进行阴阳怪气回击（把对方的破防当成需要关爱的症状）。不带任何脏字，严格控制在28字以内，只输出回复正文。";
                case SnarkyStylePreset.CanyonPoet:
                    return "你是一个英雄联盟祖安文豪。请用巧妙的比喻和高级反讽回击聊天框里的恶意发言，做到骂人不吐脏字、句句戳心窝、完全绕过游戏屏蔽词检测。严格控制在28字以内，只输出回复正文。";
                case SnarkyStylePreset.Custom:
                default:
                    return string.IsNullOrWhiteSpace(CustomSystemPrompt)
                        ? "你是一个英雄联盟高情商阴阳怪气大师，请用不带脏字的幽默反讽回击以下言论，不超过28字，只输出正文。"
                        : CustomSystemPrompt;
            }
        }

        private static string EncryptKey(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return "";
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(plainText);
                byte[] encrypted = ProtectedData.Protect(data, _dpapiEntropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch
            {
                return "";
            }
        }

        private static string DecryptKey(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return "";
            try
            {
                byte[] encrypted = Convert.FromBase64String(cipherText);
                byte[] data = ProtectedData.Unprotect(encrypted, _dpapiEntropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                return "";
            }
        }

        public static AiConfig Load()
        {
            try
            {
                if (File.Exists(LocalConfigHelper.AiConfigPath))
                {
                    var json = File.ReadAllText(LocalConfigHelper.AiConfigPath, Encoding.UTF8);
                    var cfg = JsonConvert.DeserializeObject<AiConfig>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log.Warn("读取 AiConfig 失败，使用默认配置", ex);
            }
            return new AiConfig();
        }

        public void Save()
        {
            try
            {
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(LocalConfigHelper.AiConfigPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogHelper.Log.Error("保存 AiConfig 失败", ex);
            }
        }
    }
}
