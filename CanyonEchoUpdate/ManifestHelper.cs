using System.IO;
using System.Reflection;

namespace CanyonEchoUpdate
{
    /// <summary>
    /// 清单资源工具类
    /// </summary>
    public static class ManifestHelper
    {
        public static Stream GetManifestStream(string resName)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream($"CanyonEchoUpdate.Assets.{resName}");
        }
    }
}
