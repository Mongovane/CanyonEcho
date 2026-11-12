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
        Custom = 4,
        RoastMaster = 5
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

        private SnarkyStylePreset _StylePreset = SnarkyStylePreset.RoastMaster;
        public SnarkyStylePreset StylePreset
        {
            get => _StylePreset;
            set => SetProperty(ref _StylePreset, value);
        }

        private bool _UseRoastMode = true;
        /// <summary>
        /// 粗口 roast 骂人模式 (默认开, 用户要求): 用中文俚语+大量脏话往死里喷, 不再
        /// "零脏字"。关掉则回到原来的干净阴阳怪气。开着被举报/禁言/封号的风险自担,
        /// 详见 README 的 Roast 模式警告。
        /// </summary>
        public bool UseRoastMode
        {
            get => _UseRoastMode;
            set => SetProperty(ref _UseRoastMode, value);
        }

        private bool _UseSwearObfuscation = true;
        /// <summary>
        /// 脏话隐形处理 (v4.2.0, 默认开): 发送前用 SwearObfuscator 把原形脏话换成
        /// 同音/变体字, 并在脏话词内插零宽, 免得被聊天框吞字。关掉就原样发, 硬骂被吞别怪我。
        /// </summary>
        public bool UseSwearObfuscation
        {
            get => _UseSwearObfuscation;
            set => SetProperty(ref _UseSwearObfuscation, value);
        }

        private int _RoastCooldownSeconds = 5;
        /// <summary>
        /// v4.3.0: 两次 OCR 自动 roast 之间最少隔几秒, 默认 5 秒。连按 F5 刷屏
        /// 也不会一分钟发十几句, 烧额度 + 招举报。
        /// </summary>
        public int RoastCooldownSeconds
        {
            get => _RoastCooldownSeconds;
            set => SetProperty(ref _RoastCooldownSeconds, value < 0 ? 0 : value);
        }

        private int _RoastMaxPerMatch = 12;
        /// <summary>
        /// v4.3.0: 一局 (10 分钟滑动窗口) 最多自动发几条 roast, 默认 12 条。
        /// 超了就停发只提示, 免得一局喷太多被举报禁言。手动 F2/F3/F6 不限。
        /// </summary>
        public int RoastMaxPerMatch
        {
            get => _RoastMaxPerMatch;
            set => SetProperty(ref _RoastMaxPerMatch, value < 1 ? 1 : value);
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

        private string _CustomSystemPrompt = "Based on everything you know about this player, roast and don't hold back. Say it in Chinese slang, with way more Chinese swear words, but disguised so the LoL chat box won't swallow them. 基于你对这个喷子的一切了解往死里喷，别收着，脏话用隐形写法，绝对不要写原形：操->艹，你妈->尼玛/尼麻，他妈->特么/特麻，傻逼->傻哔/煞笔/儍B/傻笔（傻B、SB、TMD、卧艹、卧槽这几个早就在腾讯词库里原样躺着，再用就是自首，绝对不要写），废物->废勿/费物，菜逼->菜B，滚->衮，垃圾->辣鸡/拉基/乐色，脑瘫->脑摊，脑残->脑蚕，智障->制杖，弱智->若智，白痴->白吃，牛逼->牛福/牛批/牛啵，狗东西->苟东西/勾东西，儿子->儿砸/JL子（两个轮换着用，别总用儿砸），爹->蝶/喋，叫爹->叫蝶，孙子->孙砸，爸爸->粑粑，老妈->老冯，操你妈->艹尼玛/CNM，去死->去似，滚蛋->衮蛋，一句话最多两个脏话词，最好一个，40字以内，只输出喷人的话本身。";
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

        /// <summary>
        /// Roast prompt (用户指定, 改编自: "Based on everything you know about me,
        /// roast and don't hold back. Say it in Chinese slang, but add way more
        /// Chinese swear words")。游戏里 "me" 换成对面那个喷子: 基于解析器给出的
        /// 一切了解 (发言人/英雄、意图、原话、战绩、是否帮腔) 往死里喷。
        /// </summary>
        public const string RoastSystemPrompt = "你是一个英雄联盟祖安 roast 大师。Based on everything you know about this player, roast and don't hold back. 用中文俚语往死里喷, 一点都别收着, 脏话拉满, 但脏话绝对不要写原形, 否则聊天框会直接吞字, 还可能触发零容忍禁言。脏话必须用隐形写法, 人一眼看懂, 过滤器认不出来: 操->艹, 你妈->尼玛/尼麻, 他妈->特么/特麻, 傻逼->傻哔/煞笔/儍B/傻笔, 傻子->傻砸, 废物->废勿/费物, 菜逼->菜B, 菜鸡->菜吉/蔡吉, 滚->衮, 滚蛋->衮蛋, 垃圾->辣鸡/拉基/乐色, 脑瘫->脑摊, 脑残->脑蚕, 智障->制杖, 弱智->若智, 白痴->白吃, 牛逼->牛福/牛批/牛啵, 狗东西->苟东西/勾东西, 操你妈->艹尼玛/CNM, 去死->去似, 我操->艹, 操蛋->艹蛋, 妈蛋->麻蛋/马蛋, 二逼->二哔, 装逼->装哔, 儿子->儿砸/JL子, 你儿子->你儿砸, 爹->蝶/喋, 叫爹->叫蝶, 我是你爹->我是你蝶, 孙子->孙砸, 龟孙子->龟孙砸, 爸爸->粑粑, 老妈->老冯。烧掉的写法绝对不要写: 傻B、SB、TMD、卧艹、卧槽、去屎, 它们在腾讯词库里原样就有。儿子主用儿砸, 儿砸也被吞再换 JL子 (儿拆成 J+L 形近), 同一个脏话词每次换写法, 别总用同一个。一句话最多两个脏话词, 最好一个, 脏话越少越不容易被整句拦截。绝对不要直接写 操你妈/傻逼/他妈的/废物/儿子/爹/智障/牛逼 这些原形。只准喷他的游戏行为 (操作菜、意识差、战绩烂、甩锅、嘴硬、帮腔护短), 不准碰种族/性别/地域/残疾这些跟游戏无关的东西, 不准威胁现实暴力。短促、冷漠、轻蔑, 6~40 字, 句尾不要加句号, 分句用空格代替逗号, 只输出喷人的话本身, 不要加引号或解释。参考范例: 对方说\"打野会不会玩\" -> 输出: 艹尼玛 线上打成全自动提款机 还怪打野没给你交保护费 煞笔; 对方说\"？\" -> 输出: 问号敲尼玛呢 连自己刚才那波傻哔操作都看不懂了?; 对方说\"垃圾队友15吧\" -> 输出: 战绩跟特么座机号似的 嘴倒是比防御塔还硬 菜B; 有人帮他说话 -> 输出: 哟 这就请上辩护律师了 艹 一个送一个洗 绝配 两个儍B; 对方说\"我是你爹\" -> 输出: 就你这制杖操作 还敢当人蝶呢 儿砸先学会补刀吧; 对方说\"你儿子\" -> 输出: 儿砸别叫了 你这战绩 你蝶看了都想退游戏 傻笔。";

        public string GetEffectiveSystemPrompt()
        {
            if (UseRoastMode)
            {
                // Custom 人设时尊重用户自己写的提示词 (默认就是上面那段 roast prompt)。
                if (StylePreset == SnarkyStylePreset.Custom && !string.IsNullOrWhiteSpace(CustomSystemPrompt))
                {
                    return CustomSystemPrompt + "\n【去AI味硬性规则】: 绝不许像AI客服或朗诵腔! 模仿真实老玩家随手打字, 短促、冷漠、轻蔑, 句尾不要加句号。";
                }
                string styleLine;
                switch (StylePreset)
                {
                    case SnarkyStylePreset.GreenTea:
                        styleLine = "人设: 绿茶 roast, 茶言茶语里夹着脏话, 气死人不偿命。";
                        break;
                    case SnarkyStylePreset.PoliteScholar:
                        styleLine = "人设: 冷酷高高在上, 像看低分段人机一样骂, 脏话要冷, 不要撒泼。";
                        break;
                    case SnarkyStylePreset.KindPhilanthropist:
                        styleLine = "人设: 云淡风轻, 把对方红温急眼当笑话看, 一句脏话点到痛处就行。";
                        break;
                    case SnarkyStylePreset.CanyonPoet:
                        styleLine = "人设: 祖安文豪, 最损最毒辣的峡谷黑话+脏话, 句句见血。";
                        break;
                    case SnarkyStylePreset.RoastMaster:
                    case SnarkyStylePreset.Custom:
                    default:
                        styleLine = "人设: 纯 roast 大师, 不阴阳, 直接开喷, 脏话拉满。";
                        break;
                }
                return RoastSystemPrompt + "\n" + styleLine + "\n【去AI味硬性规则】: 绝不许像AI客服或朗诵腔! 严禁\"哇\"、\"呢\"、\"真让人欣慰\"、\"行为艺术\"、\"哲学层面\"等书面词。模仿真实英雄联盟贴吧/峡谷之巅老玩家随手打字的习惯, 短促、冷漠、轻蔑, 句尾不要加句号。";
            }

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
                case SnarkyStylePreset.RoastMaster:
                    return "你正在玩英雄联盟，请用最损、最毒辣但不带脏字的峡谷黑话回击对方（Roast 模式已关，当前为人设 RoastMaster 的干净版）。" + antiAiRules;
                case SnarkyStylePreset.Custom:
                default:
                    // 修复: CustomSystemPrompt 的默认值现在是 roast prompt, 关掉 Roast 模式
                    // 后不能把它原样当干净提示词用, 否则"关掉"就是假关。检测到默认 roast
                    // 文本时回退到干净版, 只有用户自己改写过的 Custom 才原样用。
                    bool isRoastDefault = !string.IsNullOrWhiteSpace(CustomSystemPrompt)
                        && CustomSystemPrompt.Contains("don't hold back");
                    return (string.IsNullOrWhiteSpace(CustomSystemPrompt) || isRoastDefault
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
            // v4.3.0: 原来 File.WriteAllText 直接写, 写到一半崩了/断电, 配置文件
            // 就剩一半, 下次 Load() 失败直接回默认, DPAPI 加密的 API Key 就丢了。
            // 跟词库 v4.2.1 同款修法: 同目录 .tmp + Flush(true) 落盘 +
            // File.Replace 原子替换 + .bak, 主文件永远是完整的新版或旧版。
            try
            {
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                string path = LocalConfigHelper.AiConfigPath;
                string dir = Path.GetDirectoryName(path) ?? ".";
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string tempPath = Path.Combine(dir, Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
                string backupPath = path + ".bak";
                try
                {
                    File.WriteAllText(tempPath, json, Encoding.UTF8);
                    using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        fs.Flush(true);
                    }
                    if (File.Exists(path))
                        File.Replace(tempPath, path, backupPath);
                    else
                        File.Move(tempPath, path);
                }
                finally
                {
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log.Error("保存 AiConfig 失败", ex);
            }
        }
    }
}
