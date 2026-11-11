using log4net;
using System;
using System.IO;

namespace CanyonEcho_Wpf.Helper
{
    public static class LogHelper
    {
        private static ILog _logger = null;
        public static string logPath;

        static LogHelper()
        {
            InitLogger("CanyonEcho", "ZuanBot");
        }

        public static ILog Log => _logger;

        private static void InitLogger(string logDirName, string loggerName)
        {
            logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TEMP", logDirName);
            if (!Directory.Exists(logPath)) Directory.CreateDirectory(logPath);
            log4net.GlobalContext.Properties["fname"] = Path.Combine(logPath, "log");

            var stream = ManifestHelper.GetManifestStream("log4net.config");
            if (stream != null)
            {
                log4net.Config.XmlConfigurator.Configure(stream);
                stream.Close();
            }
            _logger = LogManager.GetLogger(loggerName);
            _logger.Info("==================初始化 CanyonEcho Logger====================");
        }
    }
}
