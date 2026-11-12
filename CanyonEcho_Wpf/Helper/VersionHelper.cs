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
            // 修复: 原版 CompareVersion 非法格式返回 0, IsNewestVersion 会把脏数据
            // 当成"已是最新"悄悄吞掉。修复后非法格式直接视为不更新, 并记日志。
            int? cmp = CompareVersion(currentVersionName, latestVersion.VersionName);
            if (cmp == null)
            {
                LogHelper.Log.Warn($"版本号格式非法, 放弃更新判断: 当前={currentVersionName} 最新={latestVersion.VersionName}");
                return true;
            }
            return cmp.Value >= 0;
        }

        /// <summary>合法返回 -1/0/1, 任一格式非法返回 null (不再返回 0 冒充相等)。</summary>
        public static int? CompareVersion(string v1, string v2)
        {
            if (string.IsNullOrWhiteSpace(v1) || string.IsNullOrWhiteSpace(v2)
                || !Regex.IsMatch(v1, _pattern) || !Regex.IsMatch(v2, _pattern))
            {
                return null;
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
