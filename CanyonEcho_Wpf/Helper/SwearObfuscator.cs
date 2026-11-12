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
                ["我操"] = "卧艹",
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
                ["傻逼"] = "傻B",
                ["傻屄"] = "傻B",
                ["傻比"] = "傻B",
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
                ["去死"] = "去屎",
                ["妈蛋"] = "麻蛋",
                ["垃圾"] = "辣鸡",
                ["爹"] = "蝶",
                ["屌"] = "吊",
                ["操"] = "艹",
                ["滚"] = "衮",
            };

        /// <summary>原形脏话表, 仅用于测试/自检, 不要拿它去拼句子。</summary>
        public static readonly string[] ObviousSwears =
            { "操你妈", "草泥马", "我是你爹", "去你妈", "你妈逼", "你妈的", "你儿子", "龟孙子", "叫爸爸",
              "他妈的", "操蛋", "我操", "你妈", "他妈", "死妈", "老妈", "你妹", "叫爹", "儿子", "孙子",
              "孝子", "爸爸", "傻逼", "傻屄", "傻比", "傻子", "傻X", "二逼", "装逼", "牛逼", "菜逼",
              "菜鸡", "废物", "脑瘫", "脑残", "智障", "弱智", "白痴", "狗东西", "贱人", "下贱",
              "婊子", "滚蛋", "去死", "妈蛋", "垃圾", "爹", "屌", "操", "滚" };

        /// <summary>隐形后的脏话写法, 模板和 LLM 输出里应该只出现这些。</summary>
        public static readonly string[] DisguisedSwears =
            { "艹", "艹尼玛", "艹泥马", "艹蛋", "卧艹", "卧槽", "尼玛", "尼玛B", "特么", "特么的",
              "傻B", "SB", "菜B", "废勿", "衮", "衮蛋", "辣鸡", "脑摊", "脑蚕", "制杖", "若智",
              "白吃", "TMD", "CNM", "NMSL", "煞笔", "儿砸", "JL子", "蝶", "粑粑", "孙砸", "龟孙砸",
              "孝砸", "你儿砸", "我是你蝶", "叫蝶", "叫粑粑", "牛福", "牛批", "牛B", "二哔", "装哔",
              "菜吉", "傻砸", "傻叉", "苟东西", "践人", "表子", "去屎", "麻蛋", "屎妈", "老冯",
              "尼妹", "吊", "拉基", "费物" };

        /// <summary>
        /// "儿子" 的字母拆字备选写法。主映射是儿砸, JL子是备胎: "儿" 拆成 J + L
        /// 形近, 过滤器按汉字匹配认不出来。模板里主用儿砸, 提示词里两个都列出来,
        /// LLM 在儿砸也被吞的时候可以改用 JL子。
        /// </summary>
        public const string SonVisualVariant = "JL子";

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
            foreach (var swear in Obfuscations.Keys.OrderByDescending(k => k.Length))
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

            foreach (var kv in Obfuscations.OrderByDescending(kv => kv.Key.Length))
            {
                result = result.Replace(kv.Key, kv.Value);
            }

            foreach (var kv in placeholders)
                result = result.Replace(kv.Key, kv.Value);

            if (!insertZeroWidthInsideSwear) return result;

            // 只在隐形脏话词内部插零宽, 不动其他字。
            var sb = new StringBuilder(result.Length + 8);
            // 隐形词按长度降序, 逐词扫描, 命中就插零宽。单字词 (艹/蝶/吊/衮) 没地方插, 原样。
            var disguisedWords = new[] { "艹尼玛", "艹泥马", "我是你蝶", "去尼玛", "尼玛B", "尼玛的", "你儿砸",
                "龟孙砸", "叫粑粑", "特么的", "艹蛋", "卧艹", "尼玛", "特么", "屎妈", "老冯", "尼妹",
                "叫蝶", "儿砸", "孙砸", "孝砸", "粑粑", "傻B", "傻砸", "傻叉", "二哔", "装哔", "牛福",
                "菜B", "菜吉", "废勿", "脑摊", "脑蚕", "制杖", "若智", "白吃", "苟东西", "践人",
                "下践", "表子", "衮蛋", "去屎", "麻蛋", "辣鸡" };
            int i = 0;
            while (i < result.Length)
            {
                string hit = null;
                foreach (var w in disguisedWords.OrderByDescending(w => w.Length))
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
                    // 词内每两个字之间插一个零宽, 如 傻B -> 傻\u200BB, 儿砸 -> 儿\u200B砸
                    for (int j = 0; j < hit.Length; j++)
                    {
                        sb.Append(hit[j]);
                        if (j < hit.Length - 1) sb.Append(ZeroWidthSpace);
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
