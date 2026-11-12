using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// 祖安语录外站词库 (v4.3.0 新增)。
    /// 来源: GitHub cndiandian/zuanbot.com, Apache-2.0, db/data.db 里筛出来的
    /// min 口吐芬芳 655 条 (见 tools/import_zuanbot.py, 筛选规则写在那里面)。
    /// 不要直接爬 caonima.de: 那个站没有公开 API, 首页注释写了未经许可不要抄袭
    /// 代码, 同一份库在 GitHub 是 Apache-2.0 开源的, 走这里才是合法路线。
    /// max 火力全开没打包: 大量性/杀全家内容, 是 Riot 26.5 要封号的严重辱骂,
    /// 不是换几个字就能发的, 打包进来等于给用户埋雷。
    /// 用法: BuiltInSnarkyEngine 在 Roast 模式下有 25% 概率从这里抽一条,
    /// 剩下的 75% 还是走内置 RoastTemplates, 免得外站语录把意图分类冲没了。
    /// 抽词带防重复: 最近 15 条不重复抽, 抽完一轮才清空, 跟 WordsLibrary 同款逻辑。
    /// </summary>
    public static class ZuanbotLibrary
    {
        private static readonly object _lock = new object();
        private static readonly Random _rng = new Random();
        private static List<string> _lines;
        private static readonly Queue<string> _recent = new Queue<string>();

        public static int Count
        {
            get
            {
                EnsureLoaded();
                lock (_lock) { return _lines.Count; }
            }
        }

        private static void EnsureLoaded()
        {
            if (_lines != null) return;
            lock (_lock)
            {
                if (_lines != null) return;
                var loaded = new List<string>();
                try
                {
                    using (var stream = ManifestHelper.GetManifestStream("zuanbotLibrary.json"))
                    {
                        if (stream != null)
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                var obj = JObject.Parse(reader.ReadToEnd());
                                foreach (var item in obj["lines"] ?? new JArray())
                                {
                                    string text = item["text"]?.ToString();
                                    if (!string.IsNullOrWhiteSpace(text) && text.Length <= 80)
                                        loaded.Add(text);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Log.Warn("祖安语录外站词库加载失败, 已跳过 (不影响内置模板)", ex);
                }
                _lines = loaded;
            }
        }

        /// <summary>随机抽一条, 最近 15 条不重复。词库没加载出来返回 null, 调用方用内置模板兜底。</summary>
        public static string GetRandomLine()
        {
            EnsureLoaded();
            lock (_lock)
            {
                if (_lines.Count == 0) return null;
                if (_lines.Count == 1) return _lines[0];

                var candidates = new List<string>();
                foreach (var line in _lines)
                {
                    if (!_recent.Contains(line)) candidates.Add(line);
                }
                if (candidates.Count == 0)
                {
                    _recent.Clear();
                    candidates = new List<string>(_lines);
                }
                string picked = candidates[_rng.Next(candidates.Count)];
                _recent.Enqueue(picked);
                while (_recent.Count > 15) _recent.Dequeue();
                return picked;
            }
        }
    }
}
