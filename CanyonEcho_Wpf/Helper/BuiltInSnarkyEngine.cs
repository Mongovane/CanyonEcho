using System;
using System.Collections.Generic;
using CanyonEcho_Wpf.ViewModels;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// 内置离线高杀伤力阴阳怪气反击引擎（纯阴阳怪气破防语录、免 API Key、0 延迟、不带脏字防封号）
    /// </summary>
    public static class BuiltInSnarkyEngine
    {
        private static readonly Random _rng = new Random();

        private static readonly Dictionary<string, string[]> IntentTemplates = new Dictionary<string, string[]>
        {
            ["抱团帮腔"] = new[]
            {
                "哟，这就开始召唤亲友团帮腔啦？不知道的还以为峡谷开庭请了辩护律师呢",
                "两个人加起来的操作凑不出一波高光，打字护短的默契倒是满分呀",
                "这就是传说中的双排连体婴吗？一个负责送温暖，一个负责在公屏开发布会",
                "别急着帮他说话呀，他那操作就算请十个公关团队来解释也圆不回来呢",
                "真感人，峡谷年度最佳护友奖非你莫属，建议赛后直接结拜",
                "一个人嘴硬就算了，怎么还带了捧哏的？你们俩是组团来峡谷说相声的吧",
                "看把你急得，不知道的还以为我刚才点评的是你家俱乐部老板呢",
                "好家伙，战绩面板上唯唯诺诺，聊天框里组团重拳出击是吧？",
                "没事的，你们俩继续在聊天框互相取暖吧，毕竟看战绩确实挺让人心寒的",
                "真是一出好戏，操作不入流，护短第一名，峡谷奥斯卡最佳男配角颁给你"
            },
            ["甩锅打野"] = new[]
            {
                "打野哪怕把户口迁到你防御塔下，也拦不住你一颗想给对面送温暖的心呀",
                "需要打野把饭喂到你嘴边再帮你按技能吗？幼儿园小朋友都没你这么难带呢",
                "奇怪了，对面打野怎么没把别的路抓崩，是看你走位比较有喜感吗？",
                "别喊打野了哥哥，大罗金仙下凡也救不了连塔刀都不会补还要硬压线的人呀",
                "自己线上打成全自动提款机，怪打野没给你交保护费是吧？太幽默了",
                "打野是在刷野发育，你是在刷黑屏次数，大家都在各自的领域发光发热呢",
                "你那路小地图是黑的，眼是不插的，人是冲到对面塔下的，锅是打野背的，真棒",
                "要不打野把惩戒交在你身上吧，看看能不能把你的游戏意识给激活一下"
            },
            ["质疑操作"] = new[]
            {
                "哥哥这波操作要是剪到网上，评论区高低得劝你去医院查查手部神经反射",
                "我是技能偶尔偏了一点，您是整局都在向空气挥拳，咱们境界确实不一样呢",
                "看你指点江山的样子，不知道的还以为你刚从S赛领奖台下来呢",
                "建议把脚从键盘上拿下来用手玩呢，虽然用脚玩确实能体现你的身残志坚",
                "没关系呀，虽然我失误了一波，但你可是整局都在给大家表演行为艺术呢",
                "你这游戏理解已经领先当前版本三个赛季了，凡人确实看不懂你的脑回路",
                "刚才那波闪现迁坟简直是神来之笔，连导播镜头都差点没跟上你的手速",
                "你这技能命中率，去游乐园玩套圈老板都得感动得把整个摊位送给你",
                "别急着点评别人呀，你刚才那套连招除了没打中人之外，姿势还是挺优美的"
            },
            ["战绩嘲讽"] = new[]
            {
                "真羡慕你，不仅温暖送得干脆利落，甩锅打字的姿势也这么行云流水",
                "看你盯着战绩面板研究半天，是在找自己的KDA掉到哪一层地心了吗？",
                "不会真有人觉得靠在聊天框嘴硬，就能把负战绩聊成全场MVP吧？太可爱了",
                "对面C位能肥成这样，军功章绝对有你的一大半呀，峡谷最佳慈善家",
                "你这局最大的战略贡献，就是帮对面五个人建立了绝对的经济自信呢",
                "别光看战绩呀，看看你的打字手速，这把的APM全点在回车键上了吧",
                "要是嘴硬能提供护甲加成的话，你现在应该比六神装石头人还要能抗了",
                "看你这豪华的黑白屏时长，这局游戏你有一半时间都在泉水喝茶吧？"
            },
            ["消极投降"] = new[]
            {
                "怎么这么快就想点投降呀，是赶着去下一把继续给路人队友做抗压训练吗？",
                "逆风只会点投降和打字甩锅，顺风只会抢人头亮牌子，这就是峡谷大聪明吗",
                "别急着投呀哥哥，大家还没看够你的下饭操作呢，再留下来演两波嘛",
                "心态这么脆弱建议去玩开心消消乐呢，那个游戏里的小动物不会打字气你",
                "泉水指挥官已经正式就位了，大家快拿小本本记下这位大师的战术遗言",
                "自己把对线打穿了就喊十五投，你是来峡谷体验极速下班服务的吗？"
            },
            ["问号嘲讽"] = new[]
            {
                "问号打得这么熟练，是对自己的下饭操作也充满了哲学层面的疑惑吗？",
                "键盘上的问号键都快被你敲出火星子了，怎么技能键还是冰凉的呀？",
                "看你一直在公屏发问号，是不认识小地图还是找不到回泉水的路呀？",
                "别发问号了哥哥，你刚才那波操作比问号本身还要让人摸不着头脑呢",
                "笑得这么开心，是因为终于在训练营学会怎么用回车键打字了吗？真棒",
                "看你一顿狂发问号，不知道的还以为你在公屏求大家给你解答三年高考五年模拟呢"
            },
            ["祖安问候"] = new[]
            {
                "急了急了，连打出来的标点符号都透着一股破防后的焦躁，看把孩子气的",
                "看你气得打字连语序都乱了，要不先深呼吸喝口温水，别把键盘拍坏了",
                "虽然你在对线期唯唯诺诺，但你在聊天框里重拳出击的样子真的很滑稽呢",
                "你就这点词汇量吗？翻来覆去就这几句，连简单人机吵架都比你有新意",
                "破防的速度比你黑屏的速度还要快，这就是传说中的峡谷玻璃心天花板吗？",
                "别气坏了身体呀哥哥，毕竟医药费可比你这局打出来的输出贵多了",
                "看你在聊天框上蹿下跳的样子，像极了被抢了松果却咬不到人的小松鼠呢",
                "真可怜，操作打不过别人，就只能在聊天框里靠敲键盘找回一点点存在感啦"
            },
            ["通用嘲讽"] = new[]
            {
                "打字速度这么快，平时的手速一定全用在键盘对线上了吧？失敬失敬",
                "看你这么努力地在聊天框证明自己，大家都替你的操作水平感到惋惜呢",
                "要是你的走位能有你打字甩锅一半利索，对面早就点投降了呢",
                "别激动呀哥哥，血压高了对身体不好，虽然看你操作确实挺考验队友血压的",
                "刚才那波是在用实力演绎什么叫眼里完全没有小地图吗？太逼真了",
                "真好，今天又在召唤师峡谷见识到了物种与脑回路的多样性，谢谢你的表演",
                "建议把显示器擦一擦呢，我看你放技能的方向总是跟敌人的位置差着十万八千里",
                "不用解释，我们都理解，一定是今天的风向影响了你的技能弹道对不对？"
            }
        };

        /// <summary>
        /// 根据解析出的聊天框上下文生成高杀伤力阴阳怪气反击
        /// </summary>
        public static string GenerateReply(LolChatParseResult parseResult, WordsLibrary library = null)
        {
            string category = parseResult?.IntentCategory ?? "通用嘲讽";
            string speaker = parseResult?.Speaker ?? "";
            string cleanMsg = parseResult?.CleanMessage ?? "";

            // 1. 若检测到“多人抱团帮腔 / 双排护短”场景，优先触发双人连带阴阳怪气
            if (parseResult != null && parseResult.IsAccompliceDefending &&
                IntentTemplates.TryGetValue("抱团帮腔", out var groupList) && groupList.Length > 0)
            {
                string baseReply = groupList[_rng.Next(groupList.Length)];
                if (!string.IsNullOrWhiteSpace(parseResult.AccompliceSpeaker) &&
                    !string.IsNullOrWhiteSpace(parseResult.LockedPrimaryTarget) &&
                    _rng.NextDouble() < 0.65)
                {
                    string customGroup = $"{parseResult.AccompliceSpeaker}这么急着帮{parseResult.LockedPrimaryTarget}洗白，你们俩一个负责送温暖一个负责开发布会是吧？";
                    if (customGroup.Length <= 48)
                        return customGroup;
                }
                return baseReply;
            }

            // 2. 根据意图类别匹配专属高杀伤阴阳怪气模板
            if (IntentTemplates.TryGetValue(category, out var templates) && templates.Length > 0)
            {
                string picked = templates[_rng.Next(templates.Length)];

                // 若识别到了说话人的英雄名（如“亚索”），带上英雄称呼增强精准破防感
                if (!string.IsNullOrWhiteSpace(speaker) && speaker.Length <= 6 && _rng.NextDouble() < 0.5)
                {
                    string prefix = $"{speaker}哥哥，";
                    if ((prefix + picked).Length <= 42)
                        return prefix + picked;
                }

                // 若对方发言较短（2~9个字），用“打成这样还有心情发‘XXX’呢”反讽
                if (!string.IsNullOrWhiteSpace(cleanMsg) && cleanMsg.Length >= 2 && cleanMsg.Length <= 9 && _rng.NextDouble() < 0.3)
                {
                    return $"不会吧不会吧，操作这么下饭还有心情在聊天框发“{cleanMsg}”呢？太逗了";
                }

                return picked;
            }

            // 3. 从词库中抽取阴阳怪气词条
            if (library != null)
            {
                try
                {
                    return library.GetLoacalWord("阴阳怪气词库");
                }
                catch { }
            }

            var fallback = IntentTemplates["通用嘲讽"];
            return fallback[_rng.Next(fallback.Length)];
        }
    }
}
