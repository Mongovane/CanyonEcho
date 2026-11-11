using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;
using CanyonEcho_Wpf.Api.Dtos;
using CanyonEcho_Wpf.Helper;

namespace CanyonEcho_Wpf.Api
{
    public class Apis
    {
        readonly RestClient _client;

        public static Apis GetInstance() => instance;
        private static readonly Apis instance = new Apis();

        private Apis()
        {
            try
            {
                _client = ConstructClient(EnvConfigHelper.Env.UseApi);
            }
            catch (Exception ex)
            {
                LogHelper.Log.Warn("初始化 Apis 客户端异常", ex);
            }
        }

        private RestClient ConstructClient(string baseUrl)
        {
            var client = new RestClient(baseUrl);
            client.Options.MaxTimeout = 10000;
            client.Options.ThrowOnAnyError = false;

            client.UseNewtonsoftJson(new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DateFormatString = "yyyy-MM-dd HH:mm:ss"
            });
            client.AddDefaultHeader("Accept", "application/json");
            client.AddDefaultHeader("Content-Type", "application/json");
            return client;
        }

        class ResultWrapper<T>
        {
            public int? Code { get; set; }
            public string Msg { get; set; }
            public T Data { get; set; }
        }

        private T HandleResponseOrError<T>(RestResponse<ResultWrapper<T>> response, RestClient client)
        {
            var url = (client?.Options?.BaseUrl + response?.Request?.Resource).Replace("//", "/");

            if (response != null && response.IsSuccessful && response.Data != null)
            {
                if (response.Data.Code == 200)
                {
                    return response.Data.Data;
                }
                throw new ApiException($"code：{response.Data.Code}\nmessage：{response.Data.Msg}", new Exception($"url：{url}"));
            }
            throw new ApiException($"请求远端服务失败 ({response?.StatusCode})", new Exception($"url：{url}"));
        }

        public async Task<VersionDto> GetLatestVersion()
        {
            if (_client == null) return null;
            var request = new RestRequest("/api/versions/latest", Method.Get);
            var response = await _client.ExecuteAsync<ResultWrapper<VersionDto>>(request);
            return HandleResponseOrError(response, _client);
        }

        /// <summary>
        /// 安全加固版文件下载：强制校验 HTTPS 协议与可选 SHA-256 哈希，杜绝中间人劫持 RCE
        /// </summary>
        public string DownloadFile(string dir, string fileName, string fileUrl, string expectedSha256 = null)
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
                using (var writer = new ProgressFileStream(filePath, false))
                {
                    var request = new RestRequest(fileUrl);
                    var stream = _client.DownloadStream(request);
                    if (stream == null) throw new IOException("下载流为空");
                    stream.CopyTo(writer);
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
