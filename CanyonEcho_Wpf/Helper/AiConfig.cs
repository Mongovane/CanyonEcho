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
            const string antiAiRules = @"
【去AI味硬性规则】：
1. 绝不许像AI客服或朗诵腔！严禁使用“哇”、“呢”、“真让人欣慰”、“行为艺术”、“哲学层面”等书面词。
2. 模仿真实英雄联盟贴吧/峡谷之巅老玩家随手打字的习惯：字数控制在 6~20 字以内，短促、冷漠、轻蔑，句尾不要加句号，分句用空格代替逗号。
3. 零脏字防封号，但必须直戳痛处（常用词：红温、汗流浃背、手在抖、座机号战绩、提款机、闪现迁坟、代练下号、辩护律师）。
4. 参考范例：
   - 对方说“打野会不会玩” -> 输出：线上打成全自动提款机 怪打野没给你交保护费？
   - 对方说“？” -> 输出：问号敲这么响 是连自己都看不懂刚才的操作了吗
   - 对方说“垃圾队友15吧” -> 输出：战绩看着像座机号 嘴倒是比防御塔还硬
   - 有人帮他说话 -> 输出：哟 这就请上辩护律师了？一个送一个洗 绝配
只输出最终回复的一句话正文，不要加任何引号或解释。";

            switch (StylePreset)
            {
                case SnarkyStylePreset.GreenTea:
                    return "你正在玩英雄联盟，请用最茶言茶语、最气人的反问句恶心对面的喷子（例如：'你这么菜你队友不会生气吧 哥哥'、'刚才那波是网卡了还是手抽筋了 替你找好借口了'）。" + antiAiRules;
                case SnarkyStylePreset.PoliteScholar:
                    return "你正在玩英雄联盟，请用冷酷、高高在上、像看低分段人机一样的语气嘲讽对方（例如：'这波也在你的计算之中吗'、'玩得挺好 下次别玩了'、'哥们你这操作回放自己敢看第二遍吗'）。" + antiAiRules;
                case SnarkyStylePreset.KindPhilanthropist:
                    return "你正在玩英雄联盟，请把对方的破防当成红温急眼，用云淡风轻的语气让对方更破防（例如：'红温了？打字连标点都哆嗦了'、'汗流浃背了兄弟 先擦擦手上的汗吧'）。" + antiAiRules;
                case SnarkyStylePreset.CanyonPoet:
                    return "你正在玩英雄联盟，请用最损、最毒辣但不带脏字的峡谷黑话回击对方（例如：'键盘上撒把米 鸡啄的连招都比你顺'、'要不你挂机吧 少个提款机我们四打五好赢点'）。" + antiAiRules;
                case SnarkyStylePreset.Custom:
                default:
                    return (string.IsNullOrWhiteSpace(CustomSystemPrompt)
                        ? "你是一个英雄联盟老玩家，请用极短、冷嘲热讽的真实玩家口吻回击对方。"
                        : CustomSystemPrompt) + antiAiRules;
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
