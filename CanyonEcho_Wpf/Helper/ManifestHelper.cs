using System.IO;
using System.Reflection;

namespace CanyonEcho_Wpf.Helper
{
    public static class ManifestHelper
    {
        public static Stream GetManifestStream(string jsonName)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream($"CanyonEcho_Wpf.Assets.{jsonName}");
        }
    }
}
