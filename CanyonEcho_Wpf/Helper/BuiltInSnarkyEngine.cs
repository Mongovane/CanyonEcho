using System;
using System.Collections.Generic;
using CanyonEcho_Wpf.ViewModels;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// 真实玩家语感（去 AI 味）阴阳怪气引擎：短促、空格断句、冷嘲热讽、直戳峡谷痛点
    /// </summary>
    public static class BuiltInSnarkyEngine
    {
        private static readonly Random _rng = new Random();

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
                "嘴硬能加护甲的话 你现在应该比六神装石头人还肉了",
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
                "要是走位有你打字一半利索 对面早就投了",
                "显示器开了吗",
                "承认自己菜没那么难 不用敲这么多字挽尊",
                "先别打字了 研究下怎么活过三分钟吧",
                "去玩云顶吧 那个不用自己按技能"
            }
        };

        public static string GenerateReply(LolChatParseResult parseResult, WordsLibrary library = null)
        {
            string category = parseResult?.IntentCategory ?? "通用嘲讽";
            string speaker = parseResult?.Speaker ?? "";
            string cleanMsg = parseResult?.CleanMessage ?? "";

            // 1. 多人抱团帮腔 / 双排护短场景
            if (parseResult != null && parseResult.IsAccompliceDefending &&
                IntentTemplates.TryGetValue("抱团帮腔", out var groupList) && groupList.Length > 0)
            {
                string baseReply = groupList[_rng.Next(groupList.Length)];
                if (!string.IsNullOrWhiteSpace(parseResult.AccompliceSpeaker) &&
                    !string.IsNullOrWhiteSpace(parseResult.LockedPrimaryTarget) &&
                    _rng.NextDouble() < 0.65)
                {
                    return $"{parseResult.AccompliceSpeaker}这么急着帮{parseResult.LockedPrimaryTarget}洗 一个送一个开发布会是吧";
                }
                return baseReply;
            }

            // 2. 意图分类匹配
            if (IntentTemplates.TryGetValue(category, out var templates) && templates.Length > 0)
            {
                string picked = templates[_rng.Next(templates.Length)];

                // 带上英雄名点名嘲讽（不加“哥哥”等AI感前缀，直接用空格点名，如 "亚索 打字这么利索 刚才闪现怎么按不出来"）
                if (!string.IsNullOrWhiteSpace(speaker) && speaker.Length <= 6 && _rng.NextDouble() < 0.5)
                {
                    string named = $"{speaker} {picked}";
                    if (named.Length <= 38)
                        return named;
                }

                if (!string.IsNullOrWhiteSpace(cleanMsg) && cleanMsg.Length >= 2 && cleanMsg.Length <= 8 && _rng.NextDouble() < 0.25)
                {
                    return $"打成这样还有脸发“{cleanMsg}” 真是汗流浃背了";
                }

                return picked;
            }

            if (library != null)
            {
                try { return library.GetLoacalWord("阴阳怪气词库"); }
                catch { }
            }

            var fallback = IntentTemplates["通用嘲讽"];
            return fallback[_rng.Next(fallback.Length)];
        }
    }
}
