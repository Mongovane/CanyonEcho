using System;
using System.IO;

namespace CanyonEcho_Wpf.Helper
{
    public class LocalConfigHelper
    {
        private static readonly string wordsLibraryFile = "wordsLibrary.json";
        private static readonly string aiConfigFile = "aiConfig.json";

        /// <summary>
        /// 配置文件目录 (%LocalAppData%\CanyonEcho)
        /// </summary>
        public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CanyonEcho");

        public static string WordsLibraryPath => GetPath(wordsLibraryFile);

        public static string AiConfigPath => GetPath(aiConfigFile);

        public static string GetPath(string configFileName)
        {
            if (!Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
            }
            return Path.Combine(Dir, configFileName);
        }
    }
}
