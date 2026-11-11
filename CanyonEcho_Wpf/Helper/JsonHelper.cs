using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Text;
using CanyonEcho_Wpf.ViewModels;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// JSON 持久化工具类（修复原版 Path.GetTempFileName 未清理导致磁盘临时文件堆积的问题）
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
            using (var stream = new FileStream(LocalConfigHelper.WordsLibraryPath, FileMode.Open, FileAccess.Read, FileShare.Read))
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

            var tempfile = Path.GetTempFileName();
            try
            {
                using (var streamWriter = new StreamWriter(tempfile, false, Encoding.UTF8))
                {
                    JsonSerializer serializer = new JsonSerializer { Formatting = Formatting.Indented };
                    serializer.Serialize(streamWriter, library);
                }
                File.Copy(tempfile, LocalConfigHelper.WordsLibraryPath, true);
            }
            finally
            {
                try { if (File.Exists(tempfile)) File.Delete(tempfile); } catch { }
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
