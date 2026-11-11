using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// 英雄联盟聊天框解析结果（含主目标锁定与多人帮腔/双排护短识别）
    /// </summary>
    public class LolChatParseResult
    {
        public string RawOcrText { get; set; } = "";
        public string Speaker { get; set; } = "";
        public string CleanMessage { get; set; } = "";
        public bool IsMalicious { get; set; }
        public string IntentCategory { get; set; } = "通用嘲讽";
        public string MatchedKeyword { get; set; } = "";

        /// <summary>
        /// 是否为他人帮腔/双排抱团护短场景
        /// </summary>
        public bool IsAccompliceDefending { get; set; }

        /// <summary>
        /// 当前锁定的主目标玩家（用于区分主目标与帮腔者/路人）
        /// </summary>
        public string LockedPrimaryTarget { get; set; } = "";

        /// <summary>
        /// 跳出来帮腔护短的第二发言人
        /// </summary>
        public string AccompliceSpeaker { get; set; } = "";

        /// <summary>
        /// 根据官方 2999 接口或标签识别出的阵营（true=敌方所有人，false=本方队伍）
        /// </summary>
        public bool? IsEnemySpeaker { get; set; }
    }

    /// <summary>
    /// 英雄联盟聊天框 OCR 文本清洗、路人排除与多人帮腔识别器
    /// </summary>
    public static class LolChatParser
    {
        private static readonly Regex TimeStampRegex = new Regex(@"^\[?\d{1,2}:\d{2}\]?\s*", RegexOptions.Compiled);
        private static readonly Regex ChannelRegex = new Regex(@"^\[(所有人|队伍|小队|All|Team|Party)\]\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static string _lockedPrimaryTarget = "";
        private static DateTime _lastLockTime = DateTime.MinValue;

        private static readonly string[] SystemNoisePatterns = new[]
        {
            "购买了", "已击杀", "摧毁了", "正在路上", "协助", "请求集合", "敌人不见了", "小心", "需要视野",
            "存活", "冷却时间", "法力值不足", "经验值", "金币", "欢迎来到英雄联盟", "全军出击"
        };

        private static readonly Dictionary<string, string[]> MaliciousIntentKeywords = new Dictionary<string, string[]>
        {
            ["抱团帮腔"] = new[] { "他说的", "没毛病", "确实怪你", "少说两句", "别怪他", "双排", "我朋友", "怪我咯", "人家", "明明是你", "怎么不看你自己", "你比他更", "俩人", "护着" },
            ["甩锅打野"] = new[] { "打野", "不抓", "野王", "刷野", "万年野", "龙呢", "反蹲", "差距", "住上", "住下", "住中", "帮我" },
            ["质疑操作"] = new[] { "会玩", "菜", "演", "演员", "脚本", "人机", "空大", "空技能", "勾", "钩", "闪现", "操作", "瞎", "眼瞎", "手残", "脚玩" },
            ["战绩嘲讽"] = new[] { "0-", "0/", "超鬼", "送", "提款机", "战绩", "输出", "补刀", "坑", "躺", "混子", "c不动", "带不动" },
            ["消极投降"] = new[] { "15", "点了吧", "投", "挂机", "泉水", "不玩了", "毁灭吧", "下一把", "浪费时间", "坐牢" },
            ["问号嘲讽"] = new[] { "?", "？", "666", "就这", "笑死", "哈哈", "小丑", "乐", "急了", "破防", "装什么", "叫什么" },
            ["祖安问候"] = new[] { "滚", "废", "垃圾", "sb", "fw", "nt" }
        };

        public static void SetLockedTarget(string speaker)
        {
            if (!string.IsNullOrWhiteSpace(speaker))
            {
                _lockedPrimaryTarget = speaker;
                _lastLockTime = DateTime.Now;
            }
        }

        public static LolChatParseResult Parse(string rawOcrText, IEnumerable<LolPlayerInfo> roster = null)
        {
            var result = new LolChatParseResult
            {
                RawOcrText = rawOcrText ?? ""
            };

            if (string.IsNullOrWhiteSpace(rawOcrText))
                return result;

            var lines = rawOcrText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count == 0)
                return result;

            var candidateLines = new List<string>();
            var distinctMaliciousSpeakers = new List<string>();

            foreach (var line in lines)
            {
                if (SystemNoisePatterns.Any(noise => line.Contains(noise)))
                    continue;

                string cleaned = CleanSingleLine(line, out string spk, out bool isAllChannel);

                // 利用官方 2999 接口排除玩家自己发的聊天行
                if (roster != null && !string.IsNullOrWhiteSpace(spk))
                {
                    var matchedPlayer = LiveClientDataService.MatchSpeakerFromRoster(spk, roster);
                    if (matchedPlayer != null && matchedPlayer.IsSelf)
                        continue;
                }

                candidateLines.Add(line);

                if (DetectMaliciousIntent(cleaned, out string cat, out string kw))
                {
                    if (!string.IsNullOrWhiteSpace(spk) && !distinctMaliciousSpeakers.Contains(spk))
                        distinctMaliciousSpeakers.Add(spk);

                    result.Speaker = spk;
                    result.CleanMessage = cleaned;
                    result.IsMalicious = true;
                    result.IntentCategory = cat;
                    result.MatchedKeyword = kw;
                    if (isAllChannel) result.IsEnemySpeaker = true;
                }
            }

            if (candidateLines.Count == 0)
                return result;

            if (!result.IsMalicious)
            {
                string targetLine = candidateLines.Last();
                result.CleanMessage = CleanSingleLine(targetLine, out string speaker, out bool isAll);
                result.Speaker = speaker;
                result.IsMalicious = DetectMaliciousIntent(result.CleanMessage, out string category, out string keyword);
                result.IntentCategory = category;
                result.MatchedKeyword = keyword;
                if (isAll) result.IsEnemySpeaker = true;
            }

            // 结合官方 2999 阵容判定发言人是队友还是对手
            if (roster != null && !string.IsNullOrWhiteSpace(result.Speaker))
            {
                var matched = LiveClientDataService.MatchSpeakerFromRoster(result.Speaker, roster);
                if (matched != null)
                {
                    result.IsEnemySpeaker = !matched.IsTeammate && !matched.IsSelf;
                }
            }

            // 判定是否属于“他人帮腔 / 双排抱团护短”：
            // 情况 A：单次截图中同时出现 >= 2 名不同玩家针对玩家发言
            if (distinctMaliciousSpeakers.Count >= 2)
            {
                result.IsAccompliceDefending = true;
                result.LockedPrimaryTarget = distinctMaliciousSpeakers[0];
                result.AccompliceSpeaker = distinctMaliciousSpeakers.Last();
                result.IntentCategory = "抱团帮腔";
            }
            // 情况 B：3 分钟内已锁定主目标 _lockedPrimaryTarget，此时另一名玩家跳出来帮腔或夹击
            else if (!string.IsNullOrWhiteSpace(_lockedPrimaryTarget) &&
                     (DateTime.Now - _lastLockTime).TotalMinutes < 3 &&
                     !string.IsNullOrWhiteSpace(result.Speaker) &&
                     !string.Equals(result.Speaker, _lockedPrimaryTarget, StringComparison.OrdinalIgnoreCase) &&
                     (result.IsMalicious || result.IntentCategory == "抱团帮腔"))
            {
                result.IsAccompliceDefending = true;
                result.LockedPrimaryTarget = _lockedPrimaryTarget;
                result.AccompliceSpeaker = result.Speaker;
                result.IntentCategory = "抱团帮腔";
            }
            else if (result.IsMalicious && !string.IsNullOrWhiteSpace(result.Speaker))
            {
                SetLockedTarget(result.Speaker);
                result.LockedPrimaryTarget = result.Speaker;
            }

            return result;
        }

        private static string CleanSingleLine(string line, out string speaker, out bool isAllChannel)
        {
            speaker = "";
            isAllChannel = false;
            if (string.IsNullOrWhiteSpace(line)) return "";

            string work = TimeStampRegex.Replace(line, "").Trim();
            var chMatch = ChannelRegex.Match(work);
            if (chMatch.Success)
            {
                string tag = chMatch.Groups[1].Value;
                isAllChannel = tag.Equals("所有人", StringComparison.OrdinalIgnoreCase) ||
                               tag.Equals("All", StringComparison.OrdinalIgnoreCase);
                work = ChannelRegex.Replace(work, "").Trim();
            }

            int colonIdx = work.IndexOf(':');
            int cnColonIdx = work.IndexOf('：');
            int splitIdx = -1;

            if (colonIdx > 0 && cnColonIdx > 0)
                splitIdx = Math.Min(colonIdx, cnColonIdx);
            else if (colonIdx > 0)
                splitIdx = colonIdx;
            else if (cnColonIdx > 0)
                splitIdx = cnColonIdx;

            if (splitIdx > 0 && splitIdx < 35)
            {
                speaker = work.Substring(0, splitIdx).Trim();
                var match = Regex.Match(speaker, @"\(([^)]+)\)|（([^）]+)）");
                if (match.Success)
                {
                    speaker = !string.IsNullOrWhiteSpace(match.Groups[1].Value)
                        ? match.Groups[1].Value
                        : match.Groups[2].Value;
                }
                work = work.Substring(splitIdx + 1).Trim();
            }

            return work;
        }

        private static bool DetectMaliciousIntent(string message, out string category, out string matchedKeyword)
        {
            category = "通用嘲讽";
            matchedKeyword = "";

            if (string.IsNullOrWhiteSpace(message))
                return false;

            string lower = message.ToLowerInvariant();
            foreach (var kvp in MaliciousIntentKeywords)
            {
                foreach (var kw in kvp.Value)
                {
                    if (lower.Contains(kw.ToLowerInvariant()))
                    {
                        category = kvp.Key;
                        matchedKeyword = kw;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
