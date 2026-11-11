using System;
using System.Collections.Generic;
using System.Linq;

namespace CanyonEcho_Wpf.Helper
{
    public class Env
    {
        public string Name { get; set; }
        public string UseApi { get; set; }
    }

    public class EnvConfigs
    {
        public string Env { get; set; }
        public List<Env> Envs { get; set; }
    }

    public static class EnvConfigHelper
    {
        private static readonly string JsonFile = "Url.json";

        public static Env Env
        {
            get
            {
                var configs = JsonHelper.DeserializeManifestJson<EnvConfigs>(JsonFile);
                return configs.Envs.FirstOrDefault(x => x.Name == configs.Env)
                       ?? throw new Exception($"不存在配置环境：{configs.Env}");
            }
        }
    }
}
