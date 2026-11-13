using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace CanyonEcho_Wpf.Helper
{
    public class LolPlayerInfo
    {
        public string SummonerName { get; set; } = "";
        public string ChampionName { get; set; } = "";
        public string Team { get; set; } = ""; // ORDER or CHAOS
        public string Position { get; set; } = "";
        public bool IsSelf { get; set; }
        public bool IsTeammate { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public int CreepScore { get; set; }
        public int Level { get; set; }
        public bool IsDead { get; set; }

        public string FormatStatsSummary()
        {
            string side = IsSelf ? "自己" : (IsTeammate ? "队友" : "敌方");
            string pos = string.IsNullOrWhiteSpace(Position) ? "" : $" {Position}";
            string deadTag = IsDead ? " 阵亡黑屏中" : "";
            return $"{side}{pos} 英雄:{ChampionName} 战绩:{Kills}-{Deaths}-{Assists} 补刀:{CreepScore} 等级:{Level}{deadTag}";
        }
    }

    /// <summary>
    /// 官方英雄联盟本地只读接口服务 (https://127.0.0.1:2999/liveclientdata)
    /// 采用 .NET Framework 4.7.2 100% 原生兼容的 ServicePointManager 处理本地自签名证书与 TLS 1.2
    /// </summary>
    public static class LiveClientDataService
    {
        private static readonly HttpClient _httpClient;
        private static List<LolPlayerInfo> _cachedRoster = new List<LolPlayerInfo>();
        private static DateTime _lastFetchTime = DateTime.MinValue;

        static LiveClientDataService()
        {
            // 修复: 原版 ServicePointManager.ServerCertificateValidationCallback += _ => true
            // 是全局的, 一旦注册, 进程里所有 HTTPS (包括 LLM 的 DeepSeek/OpenAI 调用)
            // 都会接受任意证书, 中间人随便看 API Key。修复后只对 127.0.0.1:2999
            // 这个 LoL 本地自签名接口放行, 且只用本类的 HttpClientHandler, 不碰全局。
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (request, cert, chain, sslPolicyErrors) =>
                {
                    string host = request?.RequestUri?.Host ?? "";
                    if (host == "127.0.0.1" || host == "localhost")
                        return true;
                    return sslPolicyErrors == System.Net.Security.SslPolicyErrors.None;
                }
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(1200)
            };
        }

        public static async Task<List<LolPlayerInfo>> GetCurrentMatchRosterAsync()
        {
            if ((DateTime.Now - _lastFetchTime).TotalSeconds < 5 && _cachedRoster.Count > 0)
            {
                return _cachedRoster;
            }

            try
            {
                string activeNameJson = await _httpClient.GetStringAsync("https://127.0.0.1:2999/liveclientdata/activeplayername");
                string activeName = activeNameJson.Trim().Trim('"');
                if (activeName.Contains("#"))
                    activeName = activeName.Split('#')[0];

                string playerListJson = await _httpClient.GetStringAsync("https://127.0.0.1:2999/liveclientdata/playerlist");
                var jArr = JArray.Parse(playerListJson);

                var roster = new List<LolPlayerInfo>();
                string myTeam = "";

                foreach (var item in jArr)
                {
                    string sName = item["summonerName"]?.ToString() ?? "";
                    string shortName = sName.Contains("#") ? sName.Split('#')[0] : sName;
                    string cName = item["championName"]?.ToString() ?? "";
                    string team = item["team"]?.ToString() ?? "";
                    string pos = item["position"]?.ToString() ?? "";

                    bool isSelf = !string.IsNullOrEmpty(activeName) &&
                                  (sName.Equals(activeName, StringComparison.OrdinalIgnoreCase) ||
                                   shortName.Equals(activeName, StringComparison.OrdinalIgnoreCase));
                    if (isSelf) myTeam = team;

                    var scores = item["scores"];
                    int kills = scores?["kills"]?.Value<int?>() ?? 0;
                    int deaths = scores?["deaths"]?.Value<int?>() ?? 0;
                    int assists = scores?["assists"]?.Value<int?>() ?? 0;
                    int cs = scores?["creepScore"]?.Value<int?>() ?? 0;
                    int level = item["level"]?.Value<int?>() ?? 0;
                    bool isDead = item["isDead"]?.Value<bool?>() ?? false;

                    roster.Add(new LolPlayerInfo
                    {
                        SummonerName = shortName,
                        ChampionName = cName,
                        Team = team,
                        Position = pos,
                        IsSelf = isSelf,
                        Kills = kills,
                        Deaths = deaths,
                        Assists = assists,
                        CreepScore = cs,
                        Level = level,
                        IsDead = isDead
                    });
                }

                foreach (var p in roster)
                {
                    p.IsTeammate = !p.IsSelf && !string.IsNullOrEmpty(myTeam) && p.Team == myTeam;
                }

                _cachedRoster = roster;
                _lastFetchTime = DateTime.Now;
                return roster;
            }
            catch
            {
                return _cachedRoster;
            }
        }

        public static LolPlayerInfo MatchSpeakerFromRoster(string ocrSpeaker, IEnumerable<LolPlayerInfo> roster)
        {
            if (string.IsNullOrWhiteSpace(ocrSpeaker) || roster == null) return null;
            foreach (var p in roster)
            {
                if ((!string.IsNullOrEmpty(p.ChampionName) && ocrSpeaker.Contains(p.ChampionName)) ||
                    (!string.IsNullOrEmpty(p.SummonerName) && ocrSpeaker.Contains(p.SummonerName)))
                {
                    return p;
                }
            }
            return null;
        }
    }
}
