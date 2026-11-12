using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// 英雄联盟聊天框截图与本地/云端 OCR 识别工具类
    /// </summary>
    public static class OcrHelper
    {
        /// <summary>
        /// 截取指定屏幕物理矩形区域的图像
        /// </summary>
        public static Bitmap CaptureScreenRegion(Rectangle region)
        {
            if (region.Width <= 5 || region.Height <= 5)
                return null;

            var bmp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(region.Left, region.Top, 0, 0, region.Size, CopyPixelOperation.SourceCopy);
            }
            return bmp;
        }

        /// <summary>
        /// 获取英雄联盟默认左下角聊天框区域（或用户已标定的区域）
        /// </summary>
        public static Rectangle GetLolChatBoxRegion(AiConfig config)
        {
            if (config != null && config.ChatBoxWidth > 20 && config.ChatBoxHeight > 15)
            {
                return new Rectangle(config.ChatBoxX, config.ChatBoxY, config.ChatBoxWidth, config.ChatBoxHeight);
            }

            // 默认根据主屏幕分辨率推算英雄联盟左下角聊天框位置
            // 1920x1080 下 LOL 聊天框通常位于左下方 X: 15~520, Y: 580~860
            var bounds = Screen.PrimaryScreen.Bounds;
            int x = (int)(bounds.Width * 0.008);
            int y = (int)(bounds.Height * 0.54);
            int w = (int)(bounds.Width * 0.27);
            int h = (int)(bounds.Height * 0.25);
            return new Rectangle(x, y, w, h);
        }

        /// <summary>
        /// 针对英雄联盟半透明深色背景 + 浅色聊天文字进行图像增强预处理
        /// （2倍双三次插值放大 + 灰度对比度拉伸 + 反色白底黑字增强）
        /// </summary>
        public static Bitmap PreprocessForLolChat(Bitmap source)
        {
            if (source == null) return null;

            int scale = 2;
            int width = source.Width * scale;
            int height = source.Height * scale;

            var scaled = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, width, height));
            }

            // 锁定位图内存进行快速对比度增强与反色（将深色背景浅色字转为浅色背景深色字，提高 Windows OCR 准确率）
            var rect = new Rectangle(0, 0, width, height);
            BitmapData data = scaled.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            try
            {
                int stride = data.Stride;
                int bytes = Math.Abs(stride) * height;
                byte[] rgbValues = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, rgbValues, 0, bytes);

                // 统计平均亮度，判断是否为深色背景（LOL 聊天框通常平均亮度 < 128，与下方阈值一致）
                long lumSum = 0;
                int pixelCount = width * height;
                for (int y = 0; y < height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        int idx = row + x * 3;
                        int b = rgbValues[idx];
                        int g = rgbValues[idx + 1];
                        int r = rgbValues[idx + 2];
                        int lum = (r * 299 + g * 587 + b * 114) / 1000;
                        lumSum += lum;
                    }
                }

                bool isDarkBackground = (lumSum / Math.Max(1, pixelCount)) < 128;

                for (int y = 0; y < height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        int idx = row + x * 3;
                        int b = rgbValues[idx];
                        int g = rgbValues[idx + 1];
                        int r = rgbValues[idx + 2];
                        int lum = (r * 299 + g * 587 + b * 114) / 1000;

                        if (isDarkBackground)
                        {
                            // 增强高亮文字并反色为黑字白底
                            int enhanced = lum > 115 ? Math.Min(255, (lum - 90) * 2) : 0;
                            byte inverted = (byte)(255 - enhanced);
                            rgbValues[idx] = inverted;
                            rgbValues[idx + 1] = inverted;
                            rgbValues[idx + 2] = inverted;
                        }
                        else
                        {
                            // 普通白底黑字对比度增强
                            byte val = (byte)Math.Max(0, Math.Min(255, (lum - 128) * 1.5 + 128));
                            rgbValues[idx] = val;
                            rgbValues[idx + 1] = val;
                            rgbValues[idx + 2] = val;
                        }
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, data.Scan0, bytes);
            }
            finally
            {
                scaled.UnlockBits(data);
            }

            return scaled;
        }

        /// <summary>
        /// 调用 Windows 10/11 内置原生离线 OCR (Windows.Media.Ocr.OcrEngine) 识别图像文字
        /// 兼容 .NET Framework 4.7.2 与 .NET 6/8，无需安装任何第三方 OCR 软件包
        /// </summary>
        public static async Task<string> RecognizeTextAsync(Bitmap rawImage)
        {
            if (rawImage == null) return string.Empty;

            string tempRawPath = Path.Combine(Path.GetTempPath(), $"canyonecho_ocr_raw_{Guid.NewGuid():N}.png");
            string tempPrePath = Path.Combine(Path.GetTempPath(), $"canyonecho_ocr_pre_{Guid.NewGuid():N}.png");

            try
            {
                // 先对预处理增强后的图像做 OCR；若为空再对原图做一次兜底识别
                using (var preprocessed = PreprocessForLolChat(rawImage))
                {
                    preprocessed.Save(tempPrePath, ImageFormat.Png);
                }
                rawImage.Save(tempRawPath, ImageFormat.Png);

                string text = await RunWindowsNativeOcrViaWinRtAsync(tempPrePath);
                if (string.IsNullOrWhiteSpace(text))
                {
                    text = await RunWindowsNativeOcrViaWinRtAsync(tempRawPath);
                }

                return text?.Trim() ?? string.Empty;
            }
            catch (Exception ex)
            {
                LogHelper.Log.Error("本地 OCR 识别异常", ex);
                return string.Empty;
            }
            finally
            {
                TryDeleteFile(tempPrePath);
                TryDeleteFile(tempRawPath);
            }
        }

        /// <summary>
        /// 通过 Windows 内置 WinRT OcrEngine 执行快速离线识别（支持 zh-Hans-CN / 系统当前语言）
        /// </summary>
        private static Task<string> RunWindowsNativeOcrViaWinRtAsync(string imagePath)
        {
            return Task.Run(() =>
            {
                // 通过内置 PowerShell 调用 Windows 10/11 原生 WinRT Windows.Media.Ocr API
                // 优势：在 net472 下无需额外配置 Windows 10 WinMD SDK 引用即可 100% 跨机器编译运行
                string escapedPath = imagePath.Replace("'", "''");
                string psScript = $@"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Foundation, ContentType = WindowsRuntime]
$null = [Windows.Globalization.Language, Windows.Foundation, ContentType = WindowsRuntime]

$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {{ $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' }})[0]
Function Await($WinRtTask, $ResultType) {{
    $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
    $netTask = $asTask.Invoke($null, @($WinRtTask))
    $netTask.Wait(-1) | Out-Null
    $netTask.Result
}}

$file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync('{escapedPath}')) ([Windows.Storage.StorageFile])
$stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
$decoder = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
$bitmap = Await ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])

$lang = New-Object Windows.Globalization.Language('zh-Hans-CN')
$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($lang)
if ($null -eq $engine) {{
    $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
}}
if ($null -ne $engine) {{
    $result = Await ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
    foreach ($line in $result.Lines) {{
        Write-Output (($line.Text -replace '\s+', ' ').Trim())
    }}
}}
$stream.Dispose()
";
                string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(psScript));
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand {encodedCommand}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return string.Empty;
                    // 修复: 原版先 ReadToEnd() 再 WaitForExit(8000), ReadToEnd 会一直堵到进程
                    // 退出, 8000ms 超时根本没用; 进程挂了就永远卡住 OCR。修复后用带超时的
                    // 异步读, 超时直接 Kill, 再读 stderr 记日志。
                    var outputTask = proc.StandardOutput.ReadToEndAsync();
                    if (!outputTask.Wait(8000))
                    {
                        try { proc.Kill(); } catch { }
                        try { proc.WaitForExit(2000); } catch { }
                        LogHelper.Log.Warn("本地 OCR PowerShell 进程超时, 已强制结束");
                        return string.Empty;
                    }
                    if (!proc.WaitForExit(2000))
                    {
                        try { proc.Kill(); } catch { }
                        return string.Empty;
                    }
                    if (proc.ExitCode != 0)
                    {
                        string err = string.Empty;
                        try { err = proc.StandardError.ReadToEnd(); } catch { }
                        LogHelper.Log.Warn($"本地 OCR PowerShell 退出码 {proc.ExitCode}: {err}");
                    }
                    return outputTask.Result?.Trim() ?? string.Empty;
                }
            });
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }
}
