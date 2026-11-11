using System;
using System.Net.Http;
using CanyonEcho_Wpf.Api;

namespace CanyonEcho_Wpf.Helper
{
    public static class ExceptionExtension
    {
        public static string GetDescription(this Exception ex)
        {
            return GetDescriptionInternal(ex);
        }

        private static string GetDescriptionInternal(Exception ex, int n = 1)
        {
            if (ex == null) return string.Empty;
            string innerError = "";
            if (ex.InnerException != null && n < 5)
            {
                var innerDesc = GetDescriptionInternal(ex.InnerException, ++n);
                innerError = $"\n--- 内部错误 ---\n：{innerDesc}";
            }

            return $"{ex.GetSourceDesc()}{ex.Message}\n[{ex.GetType().FullName}]调用堆栈：\n{ex.StackTrace}{innerError}";
        }

        public static string GetSourceDesc(this Exception ex)
        {
            if (ex is ApiException || ex is HttpRequestException)
                return "[远端服务异常] ";
            return "[客户端异常] ";
        }

        public static void Show(this Exception e, string msg = "", bool showDetail = false)
        {
            if (e == null) return;
            if (!string.IsNullOrWhiteSpace(msg))
                msg += "\n";

            string fullLog = msg + e.GetDescription();
            LogHelper.Log.Error(fullLog);

            string userMsg = showDetail ? fullLog : (msg + e.Message);
            MessageHelper.Error(userMsg);
        }
    }
}
