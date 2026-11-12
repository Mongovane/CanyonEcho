using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// 脏话隐形处理 (v4.2.0, 用户要求: 往死里喷, 但脏话不要这么明显, 要让英雄联盟
    /// 聊天框能发出去, 而不是硬骂然后被吞字)。
    ///
    /// 原理, 按优先级三层, 只动脏话本身, 不动整句话:
    /// 1. 同音/变体字替换, 人一眼看懂, 聊天框的中文关键词正则认不出来:
    ///    操-&gt;艹, 你妈-&gt;尼玛, 他妈-&gt;特么, 傻逼-&gt;傻B, 废物-&gt;废勿,
    ///    菜逼-&gt;菜B, 滚-&gt;衮, 垃圾-&gt;辣鸡, 脑瘫-&gt;脑摊, 脑残-&gt;脑蚕,
    ///    智障-&gt;制杖, 弱智-&gt;若智, 白痴-&gt;白吃, 牛逼-&gt;牛福, 狗东西-&gt;苟东西。
    /// 2. 亲属称呼侮辱 (v4.2.0 新增, 高危区, 旧腾讯词库里你妈/你爸/傻逼是原形
    ///    命中, 儿子/爹/孙子单字不在旧词库里, 但现在 LoL 是组合+语义过滤,
    ///    照样会被吞, 所以一样要换):
    ///    儿子-&gt;儿砸 (同音, 最自然, 网文里 "儿砸, 叫爸" 就是这个写法),
    ///    儿子-&gt;JL子 (备选, 字母拆字: "儿" 拆成 J + L 形近, 过滤器按汉字
    ///    匹配认不出来, 人一眼看懂; 模板里主用儿砸, JL子写在提示词里当备胎),
    ///    爹-&gt;蝶, 叫爹-&gt;叫蝶, 我是你爹-&gt;我是你蝶, 爸爸-&gt;粑粑,
    ///    孙子-&gt;孙砸, 龟孙子-&gt;龟孙砸, 孝子-&gt;孝砸, 老妈-&gt;老冯
    ///    (牛逼-&gt;牛福 / 老妈-&gt;老冯 是 2026 年实测演变, 见 VERIFICATION.md)。
    /// 3. 拼音首字母 (备用, 模板里少量用, 不在 Obfuscate() 里自动转):
    ///    操你妈-&gt;CNM, 他妈的-&gt;TMD, 傻逼-&gt;SB, 你妈死了-&gt;NMSL。
    /// 4. 零宽字符只插在脏话词内部: 傻B -&gt; 傻\u200BB, 艹尼玛 -&gt; 艹\u200B尼\u200B玛,
    ///    儿砸 -&gt; 儿\u200B砸, 制杖 -&gt; 制\u200B杖。
    ///    跟 F8 全句零宽不一样: F8 是每个字之间都插, 长度翻倍还容易顶爆字数上限;
    ///    这里只插脏话词内部, 一句话最多多 2-3 个不可见字符。
    ///
    /// 诚实说明: 腾讯的过滤词表不公开, 也没在真游戏里测过, 这套只能降低被吞的概率,
    /// v4.2.1: Obfuscate() 零宽插入不再拆代理对, 详见 GameSendHelper Bug 5。
    /// 不能保证 100% 发出去。旧腾讯词库 (2022, lunzima/profanities.txt, 5168 行)
    /// 里 傻逼/你妈/他妈/傻B/SB/TMD/你爸/傻子 都是原形命中, 儿子/孙子/智障/
    /// 废物/垃圾/牛逼/滚 单字不在旧词库里, 但现在是组合+语义+随机过滤
    /// (快科技 2026-08-03: "没有任何一个人能够彻底避开", "叠" 和 "爹" 成了
    /// 当代通假字, "狗哥牛逼" 发不出只能发 "狗头牛逼"), 而且 Riot 零容忍词
    /// 是整句拦截 + 当局禁言, 不是打星号。见 VERIFICATION.md 第 5 节。
    /// </summary>
    public static class SwearObfuscator
    {
        public const char ZeroWidthSpace = '\u200B';

        /// <summary>会被聊天框吞掉的原形 -&gt; 人看得懂、过滤器认不出的写法。按长度降序匹配, 长词先换。</summary>
        public static readonly IReadOnlyDictionary<string, string> Obfuscations =
            new Dictionary<string, string>
            {
                // 长词先列, 短词后列 (Obfuscate 里还会再按长度降序排一次, 双保险)。
                ["操你妈"] = "艹尼玛",
                ["草泥马"] = "艹泥马",
                ["我是你爹"] = "我是你蝶",
                ["去你妈"] = "去尼玛",
                ["你妈逼"] = "尼玛B",
                ["你妈的"] = "尼玛的",
                ["你儿子"] = "你儿砸",
                ["龟孙子"] = "龟孙砸",
                ["叫爸爸"] = "叫粑粑",
                ["他妈的"] = "特么的",
                ["操蛋"] = "艹蛋",
                ["我操"] = "艹",
                ["你妈"] = "尼玛",
                ["他妈"] = "特么",
                ["死妈"] = "屎妈",
                ["老妈"] = "老冯",
                ["你妹"] = "尼妹",
                ["叫爹"] = "叫蝶",
                ["儿子"] = "儿砸",
                ["孙子"] = "孙砸",
                ["孝子"] = "孝砸",
                ["爸爸"] = "粑粑",
                ["傻逼"] = "傻哔",
                ["傻屄"] = "傻哔",
                ["傻比"] = "傻哔",
                ["傻子"] = "傻砸",
                ["傻X"] = "傻叉",
                ["二逼"] = "二哔",
                ["装逼"] = "装哔",
                ["牛逼"] = "牛福",
                ["菜逼"] = "菜B",
                ["菜鸡"] = "菜吉",
                ["废物"] = "废勿",
                ["脑瘫"] = "脑摊",
                ["脑残"] = "脑蚕",
                ["智障"] = "制杖",
                ["弱智"] = "若智",
                ["白痴"] = "白吃",
                ["狗东西"] = "苟东西",
                ["贱人"] = "践人",
                ["下贱"] = "下践",
                ["婊子"] = "表子",
                ["滚蛋"] = "衮蛋",
                ["去死"] = "去似",
                ["妈蛋"] = "麻蛋",
                ["垃圾"] = "辣鸡",
                ["爹"] = "蝶",
                ["屌"] = "吊",
                ["操"] = "艹",
                ["滚"] = "衮",
                // v4.3.0: 下面 6 个是 "烧掉的隐形写法" 的清理映射, LLM 吐出来也要再洗一次。
                // 旧腾讯词库 (2022, 5168 行) 里原样就有: 傻B / SB / TMD / 卧艹 / 卧槽 / 去屎,
                // 模板和提示词里已经删了, 但 LLM 不听话时还可能吐, 所以 Obfuscate() 兜底再转一次。
                ["傻B"] = "傻哔",
                ["SB"] = "煞笔",
                ["TMD"] = "特么",
                ["卧艹"] = "艹",
                ["卧槽"] = "艹",
                ["去屎"] = "去似",
            };

        // v4.3.0: 原来每次 Obfuscate()/ContainsObviousSwear() 都要对 50 个映射
        // 排一次序, 零宽扫描里更是每个字符都排一次 (O(n*m log m)),
        // 一句话 80 字就要排 80 次。字典是只读不变的, 排一次存起来就够了。
        private static readonly KeyValuePair<string, string>[] SortedObfuscations =
            Obfuscations.OrderByDescending(kv => kv.Key.Length).ToArray();
        private static readonly string[] SortedDisguisedWords = new[]
            { "艹尼玛", "艹泥马", "我是你蝶", "去尼玛", "尼玛B", "尼玛的", "你儿砸",
              "龟孙砸", "叫粑粑", "特么的", "艹蛋", "卧艹", "尼玛", "特么", "屎妈", "老冯", "尼妹",
              "叫蝶", "儿砸", "孙砸", "孝砸", "粑粑", "傻B", "傻砸", "傻叉", "二哔", "装哔", "牛福",
              "菜B", "菜吉", "蔡吉", "废勿", "费物", "脑摊", "脑蚕", "制杖", "若智", "白吃", "苟东西",
              "勾东西", "践人", "下践", "表子", "衮蛋", "去似", "麻蛋", "马蛋", "辣鸡", "拉基", "乐色",
              "傻哔", "煞笔", "儍B", "傻笔", "尼麻", "特麻", "牛啵", "喋" }
            .OrderByDescending(w => w.Length).ToArray();

        /// <summary>原形脏话表, 仅用于测试/自检, 不要拿它去拼句子。</summary>
        public static readonly string[] ObviousSwears =
            { "操你妈", "草泥马", "我是你爹", "去你妈", "你妈逼", "你妈的", "你儿子", "龟孙子", "叫爸爸",
              "他妈的", "操蛋", "我操", "你妈", "他妈", "死妈", "老妈", "你妹", "叫爹", "儿子", "孙子",
              "孝子", "爸爸", "傻逼", "傻屄", "傻比", "傻子", "傻X", "二逼", "装逼", "牛逼", "菜逼",
              "菜鸡", "废物", "脑瘫", "脑残", "智障", "弱智", "白痴", "狗东西", "贱人", "下贱",
              "婊子", "滚蛋", "去死", "妈蛋", "垃圾", "爹", "屌", "操", "滚" };

        /// <summary>隐形后的脏话写法, 模板和 LLM 输出里应该只出现这些。</summary>
        // v4.3.0: 傻B / SB / TMD / 卧艹 / 卧槽 / 去屎 已从这里删掉, 它们在旧腾讯词库
        // 里原样就有, 再用就是自首。新加: 傻哔 / 煞笔 / 儍B / 傻笔 / 尼麻 / 特麻 /
        // 乐色 / 费物 / 蔡吉 / 牛啵 / 去似 / 勾东西 / 喋 (全都不在旧词库里, 儍B 还是
        // 台服 2024 实测绕过零容忍的异体字写法, 见 VERIFICATION.md 第 12 节)。
        public static readonly string[] DisguisedSwears =
            { "艹", "艹尼玛", "艹泥马", "艹蛋", "尼玛", "尼玛B", "尼麻", "特么", "特么的", "特麻",
              "傻哔", "煞笔", "儍B", "傻笔", "菜B", "废勿", "衮", "衮蛋", "辣鸡", "脑摊", "脑蚕",
              "制杖", "若智", "白吃", "CNM", "NMSL", "儿砸", "JL子", "蝶", "喋", "粑粑", "孙砸", "龟孙砸",
              "孝砸", "你儿砸", "我是你蝶", "叫蝶", "叫粑粑", "牛福", "牛批", "牛啵", "牛B", "二哔", "装哔",
              "菜吉", "蔡吉", "傻砸", "傻叉", "苟东西", "勾东西", "践人", "表子", "去似", "麻蛋", "马蛋",
              "屎妈", "老冯", "尼妹", "吊", "拉基", "费物", "乐色" };

        /// <summary>
        /// "儿子" 的字母拆字备选写法。主映射是儿砸, JL子是备胎: "儿" 拆成 J + L
        /// 形近, 过滤器按汉字匹配认不出来。模板里主用儿砸, 提示词里两个都列出来,
        /// LLM 在儿砸也被吞的时候可以改用 JL子。
        /// </summary>
        public const string SonVisualVariant = "JL子";

        /// <summary>
        /// v4.3.0 变体轮换表: 同一个脏话每次发换一种写法, 别总用同一个。
        /// 固定 1 对 1 的映射用久了就是给过滤器送靶子, 儿砸天天发, 迟早也被收进词库。
        /// 第一个是默认值 (harness 的 python 镜像按第一个检查), 后面是轮换备选。
        /// 儍B (儍 U+511A) 是 "傻" 的异体字, 台服 2024-09 实测绕过零容忍 (Dcard),
        /// 国服现在是否还管用没测过, 所以只占 1/4 的轮换权重, 不当主力。
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string[]> ObfuscationVariants =
            new Dictionary<string, string[]>
            {
                ["傻逼"] = new[] { "傻哔", "煞笔", "儍B", "傻笔" },
                ["你妈"] = new[] { "尼玛", "尼麻" },
                ["他妈"] = new[] { "特么", "特麻" },
                ["牛逼"] = new[] { "牛福", "牛批", "牛啵" },
                ["垃圾"] = new[] { "辣鸡", "拉基", "乐色" },
                ["废物"] = new[] { "废勿", "费物" },
                ["菜鸡"] = new[] { "菜吉", "蔡吉" },
                ["狗东西"] = new[] { "苟东西", "勾东西" },
                ["妈蛋"] = new[] { "麻蛋", "马蛋" },
                ["儿子"] = new[] { "儿砸", "JL子" },
                ["爹"] = new[] { "蝶", "喋" },
            };

        private static readonly Random _variantRng = new Random();
        private static readonly object _variantRngLock = new object();

        private static string ResolveVariant(string key, string defaultValue)
        {
            if (!ObfuscationVariants.TryGetValue(key, out var variants) || variants.Length <= 1)
                return defaultValue;
            lock (_variantRngLock)
            {
                return variants[_variantRng.Next(variants.Length)];
            }
        }

        /// <summary>
        /// v4.3.0 零宽轮换: 原来只插 U+200B, 现代过滤器 (go-sensitive-word 这类)
        /// 的标准动作就是先剥掉 U+200B/U+200C/U+200D 再匹配, 只用一种等于裸奔。
        /// 改成每次随机挑一个: U+200B / U+200C / U+200D / U+2060, 字节形态每次都不一样。
        /// 老实说: 只要过滤器先剥零宽再匹配, 四种全会被剥掉, 这招只能对付不剥零宽
        /// 的那类过滤器, 不能当主力, 主力是上面的变体轮换 + 一句话最多一个脏话词。
        /// U+FEFF 不用, 它在首尾会被 trim 掉。
        /// </summary>
        public static readonly char[] ZeroWidthChars = { '\u200B', '\u200C', '\u200D', '\u2060' };

        private static char PickZeroWidthChar()
        {
            lock (_variantRngLock)
            {
                return ZeroWidthChars[_variantRng.Next(ZeroWidthChars.Length)];
            }
        }

        /// <summary>正常词里含 "操/滚" 但不是脏话, 替换前要先占位保护, 免得误伤。</summary>
        public static readonly string[] ProtectedNormalWords =
            { "操作", "操控", "操纵", "体操", "早操", "操场", "滚动", "滚烫" };

        /// <summary>这句话里还有没有原形脏话。</summary>
        public static bool ContainsObviousSwear(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;
            // 先把正常词占位, 免得 "操作" 里的 "操" 被误判。
            string probe = input;
            foreach (var w in ProtectedNormalWords)
                probe = probe.Replace(w, "\uE000");
            // 长词先查, 免得 "操你妈" 被单字 "操" 抢先但逻辑一样, 这里只是为了可读性。
            foreach (var swear in SortedObfuscations.Select(kv => kv.Key))
            {
                if (swear.Length == 1)
                {
                    // 单字只认独立用法: "操" 在 "操蛋" 里是脏话 (但 "操蛋" 本身已是长词,
                    // 会先命中), "滚" 在 "滚蛋" 里同理; 这里不再额外跳过。
                    if (probe.Contains(swear)) return true;
                    continue;
                }
                if (probe.Contains(swear)) return true;
            }
            return false;
        }

        /// <summary>
        /// 把原形脏话换成隐形写法, 并在每个隐形脏话词内部插一个零宽字符。
        /// 幂等: 第二次调用不再变化 (隐形写法本身不在 Obfuscations 的 key 里)。
        /// </summary>
        public static string Obfuscate(string input, bool insertZeroWidthInsideSwear = true)
        {
            if (string.IsNullOrEmpty(input)) return input;

            string result = input;
            // 正常词先占位保护, 用私用区字符 + 序号, 不会跟正文撞车。
            var placeholders = new Dictionary<string, string>();
            for (int p = 0; p < ProtectedNormalWords.Length; p++)
            {
                string w = ProtectedNormalWords[p];
                if (!result.Contains(w)) continue;
                string holder = "\uE000" + p + "\uE001";
                placeholders[holder] = w;
                result = result.Replace(w, holder);
            }

            foreach (var kv in SortedObfuscations)
            {
                // v4.3.0: 有轮换表的 key 每次随机挑一种写法, 没表的走默认映射。
                result = result.Replace(kv.Key, ResolveVariant(kv.Key, kv.Value));
            }

            foreach (var kv in placeholders)
                result = result.Replace(kv.Key, kv.Value);

            if (!insertZeroWidthInsideSwear) return result;

            // 只在隐形脏话词内部插零宽, 不动其他字。
            var sb = new StringBuilder(result.Length + 8);
            // 隐形词按长度降序, 逐词扫描, 命中就插零宽。单字词 (艹/蝶/吊/衮) 没地方插, 原样。
            int i = 0;
            while (i < result.Length)
            {
                string hit = null;
                foreach (var w in SortedDisguisedWords)
                {
                    if (i + w.Length <= result.Length && result.Substring(i, w.Length) == w)
                    {
                        hit = w;
                        break;
                    }
                }
                if (hit == null)
                {
                    // v4.2.1 Bug 5 顺手修: 代理对 (emoji / 生僻字, 两个 char) 必须一起追加,
                    // 绝不能拆开, 否则零宽插进去或者下游按 char 切分都会把它弄成乱码。
                    if (i + 1 < result.Length && char.IsHighSurrogate(result[i]) && char.IsLowSurrogate(result[i + 1]))
                    {
                        sb.Append(result[i]);
                        sb.Append(result[i + 1]);
                        i += 2;
                    }
                    else
                    {
                        sb.Append(result[i]);
                        i++;
                    }
                }
                else
                {
                    // 词内每两个字之间插一个零宽, 如 傻哔 -> 傻\u200B哔, 儿砸 -> 儿\u200B砸。
                    // v4.3.0: 零宽字符每次随机挑 (U+200B/U+200C/U+200D/U+2060), 别总用同一个。
                    for (int j = 0; j < hit.Length; j++)
                    {
                        sb.Append(hit[j]);
                        if (j < hit.Length - 1) sb.Append(PickZeroWidthChar());
                    }
                    i += hit.Length;
                }
            }
            return sb.ToString();
        }

        /// <summary>去掉零宽字符, 方便测试时比对肉眼可见的文本。</summary>
        public static string StripZeroWidth(string input)
        {
            return string.IsNullOrEmpty(input) ? input : input.Replace("\u200B", "");
        }
    }
}
