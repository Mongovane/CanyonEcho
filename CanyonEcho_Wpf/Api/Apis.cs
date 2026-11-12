using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CanyonEcho_Wpf.Api.Dtos;
using CanyonEcho_Wpf.Helper;

namespace CanyonEcho_Wpf.Api
{
    public class Apis
    {
        public static Apis GetInstance() => instance;
        private static readonly Apis instance = new Apis();

        private Apis() { }

        // 修复: 原版 prod 的 UseApi 是 https://api.github.com/repos/Mongovane/CanyonEcho,
        // GetLatestVersion 却去请求它下面的 /api/versions/latest, GitHub 根本没有这个接口,
        // 返回的也不是 {Code:200, Data:{VersionDto}} 这种形状, 所以每次都抛 ApiException,
        // VersionHelper 吞掉后返回 null, IsNewestVersion(null) 又返回 true,
        // 结果就是永远提示"已是最新", 自动更新是个摆设。实测 api.github.com 在本机
        // 还经常 403 rate limit (60次/小时共享 IP)。
        // 修复后: 版本清单改成仓库里的 version.json, 走 raw.githubusercontent.com
        // 直接拿 (200, 无限流), 形状就是 VersionDto 本身, 不再经过 GitHub API。
        public async Task<VersionDto> GetLatestVersion()
        {
            const string manifestUrl = "https://raw.githubusercontent.com/Mongovane/CanyonEcho/main/version.json";
            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
            {
                string json = await http.GetStringAsync(manifestUrl);
                var dto = JsonConvert.DeserializeObject<VersionDto>(json);
                if (dto == null || string.IsNullOrWhiteSpace(dto.VersionName))
                    throw new ApiException("版本清单格式错误: version.json 缺少 VersionName");
                return dto;
            }
        }

        /// <summary>
        /// 安全加固版文件下载：强制校验 HTTPS 协议与可选 SHA-256 哈希，杜绝中间人劫持 RCE
        /// </summary>
        public async Task<string> DownloadFileAsync(string dir, string fileName, string fileUrl, string expectedSha256 = null)
        {
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));
            if (string.IsNullOrWhiteSpace(dir)) throw new ArgumentNullException(nameof(dir));
            if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out Uri uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new SecurityException($"安全拦截：拒绝从非 HTTPS 链接下载可执行更新包 ({fileUrl})");
            }

            fileName = Legalize(fileName);
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));

            var filePath = Path.Combine(dir, fileName);

            try
            {
                // 修复: 原版用 _client.DownloadStream(new RestRequest(fileUrl)), _client 的
                // BaseUrl 是版本服务器, 绝对 URL 的行为依赖 RestSharp 版本, 容易被拼错。
                // 修复后用 HttpClient 直连 fileUrl (已在上方校验过必须是 https)。
                using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
                using (var resp = await http.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    resp.EnsureSuccessStatusCode();
                    using (var writer = new ProgressFileStream(filePath, false))
                    using (var stream = await resp.Content.ReadAsStreamAsync())
                    {
                        stream.CopyTo(writer);
                    }
                }

                if (!string.IsNullOrWhiteSpace(expectedSha256))
                {
                    using (var sha = SHA256.Create())
                    using (var fs = File.OpenRead(filePath))
                    {
                        string actual = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                        if (!string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            throw new SecurityException($"更新包 SHA-256 哈希校验不匹配！预期：{expectedSha256}，实际：{actual}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (File.Exists(filePath)) File.Delete(filePath);
                throw new Exception($"文件安全下载或校验失败！Url为：{fileUrl}", ex);
            }

            return filePath;
        }

        private string Legalize(string path)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                path = path.Replace(c.ToString(), "");
            }
            return path.Replace("..", "");
        }
    }

    [Serializable]
    public class ApiException : Exception
    {
        public ApiException() { }
        public ApiException(string message) : base(message) { }
        public ApiException(string message, Exception inner) : base(message, inner) { }
        protected ApiException(
         System.Runtime.Serialization.SerializationInfo info,
         System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    public class SecurityException : Exception
    {
        public SecurityException(string message) : base(message) { }
    }
}
