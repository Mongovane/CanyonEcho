using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Text;
using CanyonEcho_Wpf.ViewModels;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// JSON 持久化工具类（修复原版 Path.GetTempFileName 未清理导致磁盘临时文件堆积的问题）
    /// v4.2.1 修了截图里确认的 Bug 3:
    /// 原版先写 %TEMP% 里的临时文件再 File.Copy 覆盖, 写到一半崩了词库就剩一半了,
    /// 而且 GetTempFileName() 写在 try 外面, 临时目录满了直接抛, 调用方有的地方还没包 try。
    /// 现在改成同目录 .tmp + Flush(true) 落盘 + File.Replace 原子替换 + .bak 备份,
    /// 读的时候主文件坏了自动回退到 .bak。
    /// </summary>
    public static class JsonHelper
    {
        public static T DeserializeManifestJson<T>(string jsonName)
        {
            using (var stream = ManifestHelper.GetManifestStream(jsonName))
            {
                return DeserializeStream<T>(stream);
            }
        }

        public static WordsLibrary DeserializeWordsLibrary()
        {
            string path = LocalConfigHelper.WordsLibraryPath;
            try
            {
                return DeserializeWordsLibraryFrom(path);
            }
            catch (Exception ex)
            {
                // 主文件坏了 (上次保存被打断), 回退到 .bak, 再坏才抛给调用方。
                LogHelper.Log.Warn("词库主文件读取失败, 尝试回退到 .bak 备份", ex);
                string backupPath = path + ".bak";
                if (File.Exists(backupPath))
                {
                    return DeserializeWordsLibraryFrom(backupPath);
                }
                throw;
            }
        }

        private static WordsLibrary DeserializeWordsLibraryFrom(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var library = DeserializeStream<WordsLibrary>(stream);
                if (library?.Categories != null)
                {
                    foreach (var category in library.Categories)
                    {
                        category.TargetCategories = library.Categories.Where(x => x != category).ToList();
                        category.Library = library;
                        if (category.Words != null)
                        {
                            foreach (var word in category.Words)
                            {
                                word.Category = category;
                            }
                        }
                    }
                }
                return library;
            }
        }

        public static void SerializeWordsLibrary(WordsLibrary library)
        {
            if (library == null) return;

            string destPath = LocalConfigHelper.WordsLibraryPath;
            string directory = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // v4.2.1: 临时文件必须跟目标在同一个目录 (同一个盘), File.Replace 才是原子的。
            // 不再用 Path.GetTempFileName(), 它在 %TEMP% 里, 还会在临时目录满了的时候直接抛。
            string tempPath = Path.Combine(directory ?? ".",
                Path.GetFileName(destPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            string backupPath = destPath + ".bak";
            bool replaced = false;
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var streamWriter = new StreamWriter(stream, Encoding.UTF8))
                {
                    JsonSerializer serializer = new JsonSerializer { Formatting = Formatting.Indented };
                    serializer.Serialize(streamWriter, library);
                    streamWriter.Flush();
                    // 落盘, 不是只写到系统缓存, 断电也不丢一半。
                    stream.Flush(true);
                }

                if (File.Exists(destPath))
                {
                    // 原子替换: 旧文件 -> .bak, 临时文件 -> 目标, 中间崩了也能从 .bak 恢复。
                    File.Replace(tempPath, destPath, backupPath);
                }
                else
                {
                    File.Move(tempPath, destPath);
                }
                replaced = true;
            }
            finally
            {
                // 只有替换失败才需要清临时文件; 成功时临时文件已经被 Replace/Move 吃掉了。
                if (!replaced)
                {
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                }
            }
        }

        private static T DeserializeStream<T>(Stream stream)
        {
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
            {
                JsonSerializer serializer = new JsonSerializer();
                return (T)serializer.Deserialize(reader, typeof(T));
            }
        }
    }
}
