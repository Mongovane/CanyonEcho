using System;
using System.Collections.Generic;
using CanyonEcho_Wpf.ViewModels;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// 真实玩家语感（去 AI 味）Roast 引擎：默认粗口骂人模式，短促、空格断句、中文俚语+脏话、直戳峡谷痛点；关掉 UseRoastMode 回到干净阴阳怪气
    /// </summary>
    public static class BuiltInSnarkyEngine
    {
        private static readonly Random _rng = new Random();
        // v4.3.0: Random.Next 不是线程安全的, OCR 回调和 UI 线程可能同时调
        // GenerateReply, 旧写法会撞种子/返回 0 扎堆/内部状态损坏。所有取随机
        // 数都走带锁的 Next/NextDouble, 绝不直接碰 _rng。
        private static readonly object _rngLock = new object();
        private static int RngNext(int maxValue) { lock (_rngLock) { return _rng.Next(maxValue); } }
        private static double RngNextDouble() { lock (_rngLock) { return _rng.NextDouble(); } }

        private static readonly Dictionary<string, string[]> IntentTemplates = new Dictionary<string, string[]>
        {
            ["抱团帮腔"] = new[]
            {
                "哟 这就请上辩护律师了？一个送一个洗 绝配",
                "两口子双排？操作凑不出一波高光 护短倒是挺默契",
                "护这么紧 是怕他红温把网线拔了吗",
                "别帮他洗了 十个公关团队来也圆不回他那波操作",
                "战绩面板唯唯诺诺 公屏组团重拳出击是吧",
                "一个人嘴硬就算了 还带个捧哏的来峡谷说相声？",
                "他负责送温暖 你负责在公屏开发布会 分工挺明确啊",
                "看把你急的 不知道还以为我刚才说的是你家老板呢",
                "没事 你们俩继续互相安慰 出了这把也没人夸你们了"
            },
            ["甩锅打野"] = new[]
            {
                "打野住你塔下也没用 扶不起的阿斗",
                "线上打成全自动提款机 怪打野没给你交保护费？",
                "别人对线要钱 你对线要命 还怪打野不来替你收尸",
                "奇怪了 对面打野怎么不抓别人 是看你比较好提款吗",
                "小地图是黑的 眼是不插的 死完是怪打野的 经典",
                "别喊打野了 大罗金仙下凡也救不了连塔刀都不会补的人",
                "打野在刷野 你在刷黑屏次数 大家都有光明的未来",
                "要不打野把惩戒交你身上 看看能不能激活一下你的意识"
            },
            ["质疑操作"] = new[]
            {
                "打字这么利索 刚才闪现怎么按不出来",
                "这波也在你的计算之中吗",
                "哥们你这操作回放自己敢看第二遍吗",
                "键盘上撒把米 鸡啄的连招都比你顺",
                "刚才那波是网卡了还是手抽筋了 替你找好借口了",
                "真羡慕你 每波都能精准避开所有正确选项",
                "刚才那套连招除了没打中人 姿势还挺帅",
                "你这理解不去LPL登峰组当教练屈才了",
                "玩得挺好 下次别玩了",
                "代练刚下号？前两分钟看着还没这么抽象"
            },
            ["战绩嘲讽"] = new[]
            {
                "看你打字这气势 我还以为你10-0呢 点开面板原来是座机号",
                "战绩看着像区号 嘴倒是比对面防御塔还硬",
                "要不你挂机吧 少个提款机我们四打五好赢点",
                "对面真该给你凑个红包 没你这把最佳第六人他们真赢不了",
                "你这把唯一的作用 就是帮对面C位把杀人书叠满",
                "嘴硬能加护甲的话 你现在应该比六神装石头人还肉了 菜逼",
                "但凡栓条狗在键盘上踩两脚 打出来的输出都比这高",
                "对面杀你都嫌不值钱 连大招都舍不得交了"
            },
            ["消极投降"] = new[]
            {
                "自己对线打穿了就喊15 赶着去下一把继续送？",
                "人还在泉水读秒 战术遗言已经先打出来了",
                "顺风只会亮狗牌 逆风只会点投降 经典神人",
                "心态这么脆建议去玩消消乐 那个没人压力你",
                "黑白屏看的比谁都久 指导起队友来倒是头头是道",
                "别急着投啊 大家还没看够你的下饭集锦呢"
            },
            ["问号嘲讽"] = new[]
            {
                "问号敲这么响 是终于连自己都看不懂刚才那波操作了吗",
                "问号键卡住了？需要队友帮你抠下来吗",
                "别发问号了 你的操作比问号本身还抽象",
                "怎么一直在打问号 是找不到回泉水的路了吗",
                "就这？我还以为多狠呢"
            },
            ["祖安问候"] = new[]
            {
                "红温了？打字连标点都开始哆嗦了",
                "汗流浃背了兄弟 先擦擦手上的汗吧",
                "你看你 又急 破防速度比你黑屏速度还快",
                "操作打不过 就只能在聊天框找存在感了 理解",
                "你就这点词汇量？连人机吵架都比你有新意",
                "别气坏了身体 医药费可比你这把打的输出贵多了",
                "对线唯唯诺诺 打字重拳出击 键盘快拍碎了吧"
            },
            ["通用嘲讽"] = new[]
            {
                "你这么菜你队友不会生气吧 哥哥",
                "手在抖？",
                "别急 越急越像人机",
                "要是走位有你打字一半利索 对面早就投了 傻逼",
                "显示器开了吗",
                "承认自己菜没那么难 不用敲这么多字挽尊",
                "先别打字了 研究下怎么活过三分钟吧",
                "去玩云顶吧 那个不用自己按技能"
            }
        };


        /// <summary>
        /// 粗口 Roast 模板 (v4.2.0 隐形版, 默认用这套, UseRoastMode=true 时生效)。
    /// v4.2.0 新增 21 条: 儿砸/JL子/蝶/孙砸/制杖/牛福/苟东西/白吃, 8 个分类共 82 条, 每条必含隐形脏话。
        /// 规矩: 只喷游戏行为 (操作/意识/战绩/甩锅/嘴硬/帮腔), 不碰种族/性别/地域/残疾,
        /// 不威胁现实暴力。脏话全是隐形写法 (艹/尼玛/特么/傻哔/煞笔/儍B/菜B/废勿/衮/辣鸡/脑摊),
        /// 一句原形都没有, 人一眼看懂, 过滤器认不出来。发送时 SwearObfuscator 会再
        /// 在脏话词内插零宽。关掉 UseRoastMode 就回到上面那套干净的 IntentTemplates。
        /// </summary>
        private static readonly Dictionary<string, string[]> RoastTemplates = new Dictionary<string, string[]>
        {
            ["抱团帮腔"] = new[]
            {
                "哟 这就请上辩护律师了 艹 一个送一个洗 绝配 两个傻哔",
                "两口子双排是吧 艹 操作凑不出一波高光 护短倒是挺默契",
                "护这么紧干嘛 是怕他红温把网线拔了 还是怕你俩一起丢人 煞笔",
                "别特么帮他洗了 十个公关团队来也圆不回他那波儍B操作",
                "战绩面板唯唯诺诺 公屏组团重拳出击是吧 两个废勿",
                "一个人嘴硬就算了 还带个捧哏的来峡谷说相声 艹",
                "他负责送温暖 你负责在公屏开发布会 分工挺明确啊 傻哔组合",
                "看把你急的 艹 不知道还以为我刚才骂的是你爹呢",
                "没事 你们俩继续互相安慰 出了这把也没人夸你们了 菜B",
                "儿砸护蝶是吧 你俩一个送一个洗 绝配",
                "JL子这么急着帮你蝶洗是吧 一个送一个开发布会",
                "孙砸别叫了 你蝶那波操作看了都摇头"
            },
            ["甩锅打野"] = new[]
            {
                "艹 打野住你塔下也没用 扶不起的阿斗 傻哔",
                "线上打成全自动提款机 还怪打野没给你交保护费 尼玛的 煞笔",
                "别人对线要钱 你对线要命 还怪打野不来替你收尸 废勿",
                "奇怪了 对面打野怎么不抓别人 是看你比较好提款吗 菜B",
                "小地图是黑的 眼是不插的 死完是怪打野的 经典傻哔",
                "别喊打野了 艹 大罗金仙下凡也救不了连塔刀都不会补的人",
                "打野在刷野 你在刷黑屏次数 大家都有光明的未来 煞笔",
                "要不打野把惩戒交你身上 看看能不能激活一下你的脑子 儍B",
                "儿砸 线上打成提款机 还怪打野没交保护费 制杖",
                "叫蝶也没用 你蝶来了也救不了你这废勿",
                "牛福啊 打野没来你就送 来了你还送 菜B"
            },
            ["质疑操作"] = new[]
            {
                "艹 打字这么利索 刚才闪现怎么按不出来 傻哔",
                "这波也在你的计算之中吗 脑摊",
                "哥们你这操作回放自己敢看第二遍吗 丢不丢人 煞笔",
                "键盘上撒把米 鸡啄的连招都比你顺 废勿",
                "刚才那波是网卡了还是手抽筋了 艹 替你找好借口了",
                "真特么羡慕你 每波都能精准避开所有正确选项",
                "刚才那套连招除了没打中人 姿势还挺帅 傻笔",
                "你这理解不去LPL当教练屈才了 菜B",
                "玩得挺好 下次别玩了 衮",
                "代练刚下号是吧 前两分钟看着还没这么抽象 废勿",
                "制杖操作看得我直乐 艹 闪现迁坟都比你准",
                "白吃啊 这波操作 狗看了都摇头 傻哔",
                "苟东西 打字挺凶 操作跟屎一样 辣鸡"
            },
            ["战绩嘲讽"] = new[]
            {
                "看你打字这气势 我还以为你10-0呢 点开面板原来是座机号 艹",
                "战绩看着像区号 嘴倒是比对面防御塔还硬 菜B",
                "要不你挂机吧 艹 少个提款机我们四打五好赢点",
                "对面真该给你凑个红包 没你这把最佳第六人他们真赢不了 煞笔",
                "你这把唯一的作用 就是帮对面C位把杀人书叠满 废勿",
                "嘴硬能加护甲的话 你现在应该比六神装石头人还肉了 菜B",
                "但凡栓条狗在键盘上踩两脚 打出来的输出都比这高 辣鸡",
                "对面杀你都嫌不值钱 连大招都舍不得交了 菜B",
                "战绩跟你蝶电话号似的 一长串零 制杖",
                "0-8的战绩还敢叫 儿砸你看看你这制杖操作",
                "牛福 菜成这样还敢喷人 辣鸡 先看看面板吧"
            },
            ["消极投降"] = new[]
            {
                "自己对线打穿了就喊15 艹 赶着去下一把继续送是吧",
                "人还在泉水读秒 战术遗言已经先打出来了 煞笔",
                "顺风只会亮狗牌 逆风只会点投降 经典神人 废勿",
                "心态这么脆建议去玩消消乐 那个没人压力你 菜B",
                "黑白屏看的比谁都久 指导起队友来倒是头头是道 艹",
                "别急着投啊 大家还没看够你的下饭集锦呢 儍B",
                "15投是吧 儿砸你倒是跑得快 制杖",
                "投降投得比你送得还快 傻哔 衮蛋"
            },
            ["问号嘲讽"] = new[]
            {
                "问号敲尼玛呢 是终于连自己都看不懂刚才那波煞笔操作了吗",
                "问号键卡住了是吧 需要队友帮你抠下来吗 废勿",
                "别发问号了 艹 你的操作比问号本身还抽象",
                "怎么一直在打问号 是找不到回泉水的路了吗 菜B",
                "就这 艹 我还以为多狠呢 傻笔",
                "问号发给谁看呢 儿砸 你蝶教你这么玩的",
                "问号敲尼玛呢 制杖 连自己操作都看不懂"
            },
            ["祖安问候"] = new[]
            {
                "红温了是吧 艹 打字连标点都开始哆嗦了",
                "汗流浃背了兄弟 先擦擦手上的汗吧 傻哔",
                "你看你 又急 破防速度比你黑屏速度还快 废勿",
                "操作打不过 就只能在聊天框找存在感了 理解 菜B",
                "你就这点词汇量 连人机吵架都比你有新意 辣鸡",
                "别气坏了身体 艹 医药费可比你这把打的输出贵多了",
                "对线唯唯诺诺 打字重拳出击 键盘快拍碎了吧 煞笔",
                "红温了是吧 艹 脸比你战绩还红 制杖",
                "操作打不过就急眼了是吧 菜B 汗流浃背了 儍B"
            },
            ["通用嘲讽"] = new[]
            {
                "艹 你这么菜你队友不会生气吧 傻哔",
                "手在抖是吧 菜B",
                "别急 越急越像人机 废勿",
                "要是走位有你打字一半利索 对面早就投了 煞笔",
                "显示器开了吗 傻笔",
                "承认自己菜没那么难 不用敲这么多字挽尊 艹",
                "先别打字了 研究下怎么活过三分钟吧 菜B",
                "去玩云顶吧 那个不用自己按技能 废勿",
                "儿砸 别打字了 你蝶看了都想退游戏",
                "制杖一个 操作菜嘴还硬 菜B",
                "牛福啊 菜成这样还敢喷人 辣鸡"
            }
        };

        public static string GenerateReply(LolChatParseResult parseResult, WordsLibrary library = null, bool useRoastMode = true)
        {
            // 默认 Roast 模式: 用 RoastTemplates (脏话拉满)。关掉才用干净的 IntentTemplates。
            var templatesByIntent = useRoastMode ? RoastTemplates : IntentTemplates;
            string category = parseResult?.IntentCategory ?? "通用嘲讽";
            string speaker = parseResult?.Speaker ?? "";
            string cleanMsg = parseResult?.CleanMessage ?? "";

            // v4.3.0: Roast 模式下 25% 概率从祖安语录外站词库 (ZuanbotLibrary,
            // cndiandian/zuanbot.com Apache-2.0, min 口吐芬芳 655 条, 已筛过)
            // 抽一条, 免得 82 条内置模板翻来覆去就那几句, 对面一看就知道是机器人。
            // 外站词库导入时已经过 SwearObfuscator 同款映射, 发送时 GameSendHelper
            // 还会再隐形一次。抽不到 (词库没加载出来) 就老老实实走下面的模板。
            if (useRoastMode && RngNextDouble() < 0.25)
            {
                string zuanbotLine = ZuanbotLibrary.GetRandomLine();
                if (!string.IsNullOrWhiteSpace(zuanbotLine))
                    return zuanbotLine;
            }

            // 1. 多人抱团帮腔 / 双排护短场景
            if (parseResult != null && parseResult.IsAccompliceDefending &&
                templatesByIntent.TryGetValue("抱团帮腔", out var groupList) && groupList.Length > 0)
            {
                string baseReply = groupList[RngNext(groupList.Length)];
                if (!string.IsNullOrWhiteSpace(parseResult.AccompliceSpeaker) &&
                    !string.IsNullOrWhiteSpace(parseResult.LockedPrimaryTarget) &&
                    RngNextDouble() < 0.65)
                {
                    return useRoastMode
                        ? $"{parseResult.AccompliceSpeaker}这么急着帮{parseResult.LockedPrimaryTarget}洗是吧 艹 一个送一个开发布会 两个煞笔"
                        : $"{parseResult.AccompliceSpeaker}这么急着帮{parseResult.LockedPrimaryTarget}洗 一个送一个开发布会是吧";
                }
                return baseReply;
            }

            // 2. 意图分类匹配
            if (templatesByIntent.TryGetValue(category, out var templates) && templates.Length > 0)
            {
                string picked = templates[RngNext(templates.Length)];

                // 带上英雄名点名嘲讽（不加“哥哥”等AI感前缀，直接用空格点名，如 "亚索 打字这么利索 刚才闪现怎么按不出来"）
                if (!string.IsNullOrWhiteSpace(speaker) && speaker.Length <= 6 && RngNextDouble() < 0.5)
                {
                    string named = $"{speaker} {picked}";
                    if (named.Length <= (useRoastMode ? 48 : 38))
                        return named;
                }

                if (!string.IsNullOrWhiteSpace(cleanMsg) && cleanMsg.Length >= 2 && cleanMsg.Length <= 8 && RngNextDouble() < 0.25)
                {
                    return useRoastMode
                        ? $"打成这样还有脸发“{cleanMsg}” 艹 真是汗流浃背了 煞笔"
                        : $"打成这样还有脸发“{cleanMsg}” 真是汗流浃背了";
                }

                return picked;
            }

            if (library != null)
            {
                try { return library.GetLoacalWord("阴阳怪气词库"); }
                catch { }
            }

            var fallback = templatesByIntent["通用嘲讽"];
            return fallback[RngNext(fallback.Length)];
        }
    }
}
