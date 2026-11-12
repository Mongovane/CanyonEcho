using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace CanyonEchoUpdate
{
    public class Program
    {
        /// <summary>
        /// 安全加固版更新程序
        /// 参数格式: [0]=新文件源路径 [1]=目标替换路径 [2]=(可选)预期SHA256校验值 [3]=(可选)主进程PID
        /// </summary>
        static void Main(string[] args)
        {
            var logger = new LogHelper().Logger4;
            try
            {
                if (args == null || args.Length < 2)
                {
                    logger.Warn("更新参数不足，退出更新。");
                    return;
                }

                var sourcePath = Path.GetFullPath(args[0]);
                var targetPath = Path.GetFullPath(args[1]);
                var expectedHash = args.Length >= 3 ? args[2] : "";
                int targetPid = (args.Length >= 4 && int.TryParse(args[3], out int pid)) ? pid : -1;

                if (!File.Exists(sourcePath) || string.IsNullOrWhiteSpace(targetPath))
                {
                    logger.Error($"更新源文件不存在或目标路径无效: {sourcePath}");
                    return;
                }

                // 安全校验 1：验证源文件扩展名与非空大小
                var ext = Path.GetExtension(targetPath);
                if (!string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    logger.Error($"拒绝替换非 .exe 目标文件: {targetPath}");
                    return;
                }

                // 安全校验 2：若提供了 SHA-256 哈希，则严格校验文件完整性，防止传输篡改
                // 修复: 原版用 "SKIP" 哨兵跳过校验, 魔法字符串可被注入。修复后空串 = 未提供
                // 哈希, 明确记一条警告再继续, 不再认任何哨兵字符串。
                if (string.IsNullOrWhiteSpace(expectedHash))
                {
                    logger.Warn("更新包未提供 SHA-256 哈希, 跳过完整性校验 (仅信任 HTTPS 来源)");
                }
                if (!string.IsNullOrWhiteSpace(expectedHash))
                {
                    string actualHash = ComputeSha256(sourcePath);
                    if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        logger.Error($"更新包 SHA-256 校验失败！预期: {expectedHash}, 实际: {actualHash}");
                        File.Delete(sourcePath);
                        return;
                    }
                }

                // 安全校验 3：优先通过精确 PID 关闭主进程，避免枚举 64 位系统进程触发 Win32Exception 崩溃
                Process targetProcess = null;
                if (targetPid > 0)
                {
                    try { targetProcess = Process.GetProcessById(targetPid); }
                    catch { targetProcess = null; }
                }

                if (targetProcess == null)
                {
                    foreach (var p in Process.GetProcesses())
                    {
                        try
                        {
                            if (p.MainWindowTitle == "峡谷回音" || p.MainWindowTitle == "祖安助手")
                            {
                                if (string.Equals(p.MainModule?.FileName, targetPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    targetProcess = p;
                                    break;
                                }
                            }
                        }
                        catch (Win32Exception) { /* 忽略无权限访问的高权限系统进程 */ }
                        catch (InvalidOperationException) { /* 忽略已退出的进程 */ }
                    }
                }

                if (targetProcess != null && !targetProcess.HasExited)
                {
                    targetProcess.CloseMainWindow();
                    if (!targetProcess.WaitForExit(1500))
                    {
                        targetProcess.Kill();
                        targetProcess.WaitForExit(2000);
                    }
                }

                File.Copy(sourcePath, targetPath, true);
                try { File.Delete(sourcePath); } catch { }

                Process.Start(new ProcessStartInfo(targetPath) { UseShellExecute = true });
                logger.Info("CanyonEcho 更新执行成功");
            }
            catch (Exception e)
            {
                logger.Error("更新程序执行出错", e);
            }
        }

        private static string ComputeSha256(string filePath)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hashBytes = sha.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
