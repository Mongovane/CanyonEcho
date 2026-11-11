using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CanyonEcho_Wpf.Api;
using CanyonEcho_Wpf.Api.Dtos;

namespace CanyonEcho_Wpf.Helper
{
    public static class VersionHelper
    {
        private static readonly Apis _apis = Apis.GetInstance();
        private static readonly string _pattern = @"^[0-9]{1,2}\.[0-9]{1,2}\.[0-9]{1,2}$";

        public static async Task<VersionDto> GetLatestVersion()
        {
            try
            {
                return await _apis.GetLatestVersion();
            }
            catch
            {
                return null;
            }
        }

        public static string GetCurrentVersionName()
        {
            return System.Windows.Application.ResourceAssembly.GetName().Version.ToString(3);
        }

        public static bool IsNewestVersion(VersionDto latestVersion)
        {
            if (latestVersion == null || string.IsNullOrWhiteSpace(latestVersion.VersionName)) return true;
            var currentVersionName = GetCurrentVersionName();
            return CompareVersion(currentVersionName, latestVersion.VersionName) >= 0;
        }

        public static int CompareVersion(string v1, string v2)
        {
            if (!Regex.IsMatch(v1, _pattern) || !Regex.IsMatch(v2, _pattern))
            {
                return 0;
            }

            var version1 = v1.Split('.').Select(int.Parse).ToArray();
            var version2 = v2.Split('.').Select(int.Parse).ToArray();

            for (int i = 0; i < 3; i++)
            {
                if (version1[i] > version2[i]) return 1;
                if (version1[i] < version2[i]) return -1;
            }
            return 0;
        }
    }
}
