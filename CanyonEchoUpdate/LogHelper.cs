using log4net;
using System;
using System.IO;

namespace CanyonEchoUpdate
{
    public class LogHelper
    {
        private ILog _logger = null;
        public static string logPath;

        public LogHelper(string logDirName = null, string loggerName = "CanyonEcho")
        {
            InitLogger(logDirName ?? "CanyonEcho", loggerName);
        }

        public ILog Logger4 => _logger;

        private void InitLogger(string logDirName, string loggerName)
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
            _logger.Info("==================初始化 CanyonEchoUpdate Logger====================");
        }
    }
}
