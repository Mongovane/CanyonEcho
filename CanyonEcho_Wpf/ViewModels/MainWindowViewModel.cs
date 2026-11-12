using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using CanyonEcho_Wpf.Api;
using CanyonEcho_Wpf.Constants;
using CanyonEcho_Wpf.Helper;
using CanyonEcho_Wpf.Views;
using Application = System.Windows.Application;

namespace CanyonEcho_Wpf.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        private readonly IDialogService _dialogService;
        private readonly Apis _apis = Apis.GetInstance();
        private GlobalKeyboardHook _hook;
        private InGameOverlayWindow _overlayWindow;
        private bool _isProcessingOcr;
        private DateTime _lastTriggerTime = DateTime.MinValue;
        // v4.3.0: OCR 自动 roast 限流器, 一局最多 12 条、两次之间至少 5 秒
        // (AiConfig 可调)。手动 F2/F3/F6 不走这里。
        private readonly RoastLimiter _roastLimiter = new RoastLimiter();

        public WordsLibrary Library { get; set; }
        public AiConfig AiSetting { get; set; }

        #region 绑定属性
        private bool _IsPerWord = false;
        /// <summary>
        /// 是否逐字纵向发送 (F12)
        /// </summary>
        public bool IsPerWord
        {
            get => _IsPerWord;
            set => SetProperty(ref _IsPerWord, value);
        }

        private bool _IsAll = false;
        /// <summary>
        /// 是否发送所有人消息 (F11)
        /// </summary>
        public bool IsAll
        {
            get => _IsAll;
            set => SetProperty(ref _IsAll, value);
        }

        private bool _IsZeroWidthAntiFilter = false;
        /// <summary>
        /// 是否开启单行零宽字符防屏蔽穿插 (F8)
        /// </summary>
        public bool IsZeroWidthAntiFilter
        {
            get => _IsZeroWidthAntiFilter;
            set
            {
                if (SetProperty(ref _IsZeroWidthAntiFilter, value) && AiSetting != null)
                {
                    AiSetting.UseZeroWidthAntiFilter = value;
                    AiSetting.Save();
                }
            }
        }

        private bool _IsOverlayVisible = true;
        /// <summary>
        /// 是否显示游戏内安全穿透悬浮窗 HUD (F7)
        /// </summary>
        public bool IsOverlayVisible
        {
            get => _IsOverlayVisible;
            set
            {
                if (SetProperty(ref _IsOverlayVisible, value))
                {
                    UpdateOverlayVisibility();
                    if (AiSetting != null)
                    {
                        AiSetting.ShowInGameOverlay = value;
                        AiSetting.Save();
                    }
                }
            }
        }

        private bool _IsNotifyIconBlink;
        public bool IsNotifyIconBlink
        {
            get => _IsNotifyIconBlink;
            set => SetProperty(ref _IsNotifyIconBlink, value);
        }

        private bool _IsNotifyIconShow = true;
        public bool IsNotifyIconShow
        {
            get => _IsNotifyIconShow;
            set => SetProperty(ref _IsNotifyIconShow, value);
        }

        private string _Version;
        public string Version
        {
            get => _Version;
            set => SetProperty(ref _Version, value);
        }

        private bool _NeedUpdate;
        public bool NeedUpdate
        {
            get => _NeedUpdate;
            set => SetProperty(ref _NeedUpdate, value);
        }

        private string _LastOcrText = "等待截图识别（F4 手动框选聊天框 | F5 自动静默识别聊天框）";
        public string LastOcrText
        {
            get => _LastOcrText;
            set => SetProperty(ref _LastOcrText, value);
        }

        private string _LastAiReply = "准备就绪（支持 LLM 大模型与内置高情商阴阳怪气引擎）";
        public string LastAiReply
        {
            get => _LastAiReply;
            set => SetProperty(ref _LastAiReply, value);
        }
        #endregion

        public MainWindowViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        #region 命令
        private DelegateCommand _LoadedCommand;
        public DelegateCommand LoadedCommand => _LoadedCommand ?? (_LoadedCommand = new DelegateCommand(ExecuteLoadedCommand));
        async void ExecuteLoadedCommand()
        {
            try
            {
                AiSetting = AiConfig.Load();
                _IsZeroWidthAntiFilter = AiSetting.UseZeroWidthAntiFilter;
                RaisePropertyChanged(nameof(IsZeroWidthAntiFilter));
                _IsOverlayVisible = AiSetting.ShowInGameOverlay;
                RaisePropertyChanged(nameof(IsOverlayVisible));

                LoadWordsLibrary();
                HookKeys();
                InitOverlayWindow();

                Application.Current.Exit += (s, ev) =>
                {
                    _hook?.Dispose();
                    _overlayWindow?.Close();
                };

                Version = "v" + Application.ResourceAssembly.GetName().Version.ToString(3);
                await InitNeedUpdate();
            }
            catch (Exception e)
            {
                e.Show();
            }
        }

        private void InitOverlayWindow()
        {
            if (_overlayWindow == null)
            {
                _overlayWindow = new InGameOverlayWindow
                {
                    DataContext = this
                };
            }
            UpdateOverlayVisibility();
        }

        private void UpdateOverlayVisibility()
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (_overlayWindow == null) return;
                if (IsOverlayVisible)
                    _overlayWindow.Show();
                else
                    _overlayWindow.Hide();
            });
        }

        private async Task InitNeedUpdate()
        {
            try
            {
                var latestVersion = await VersionHelper.GetLatestVersion();
                NeedUpdate = !VersionHelper.IsNewestVersion(latestVersion);
            }
            catch
            {
                NeedUpdate = false;
            }
        }

        private void LoadWordsLibrary()
        {
            try
            {
                if (!File.Exists(LocalConfigHelper.WordsLibraryPath))
                {
                    var dir = Path.GetDirectoryName(LocalConfigHelper.WordsLibraryPath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    var manifestStream = ManifestHelper.GetManifestStream("wordsLibrary.json");
                    using (var stream = File.Create(LocalConfigHelper.WordsLibraryPath))
                    {
                        manifestStream.CopyTo(stream);
                    }
                }

                Library = JsonHelper.DeserializeWordsLibrary();
                EnsureSnarkyCategoryExists();
            }
            catch (Exception e)
            {
                // v4.3.0: 原来读坏就 File.Delete 删用户词库再 Shutdown, 词库
                // 是用户攒了很久的东西, 删了就找不回来了。改成: 坏文件改名
                // 成 .corrupt-时间戳 留着给用户手动抢救, 再从内置清单重建
                // 一个默认词库继续跑, 程序不退出。
                LogHelper.Log.Error("读取词库失败, 坏文件已改名保留, 正在用内置默认词库重建", e);
                try
                {
                    string corruptPath = LocalConfigHelper.WordsLibraryPath
                        + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    if (File.Exists(LocalConfigHelper.WordsLibraryPath))
                        File.Move(LocalConfigHelper.WordsLibraryPath, corruptPath);
                }
                catch (Exception moveEx)
                {
                    LogHelper.Log.Warn("损坏词库改名失败", moveEx);
                }
                try
                {
                    Library = JsonHelper.DeserializeManifestJson<WordsLibrary>("wordsLibrary.json");
                    EnsureSnarkyCategoryExists();
                    JsonHelper.SerializeWordsLibrary(Library);
                    MessageHelper.Info("词库文件已损坏, 坏文件已改名保留 (.corrupt-时间戳), 已用内置默认词库重建, 你的旧词条可以从坏文件里手动找回。");
                }
                catch (Exception rebuildEx)
                {
                    rebuildEx.Show("词库损坏且内置默认词库也读不出来！");
                    Application.Current.Shutdown();
                }
            }
        }

        private void EnsureSnarkyCategoryExists()
        {
            if (Library?.Categories == null) return;
            foreach (var c in Library.Categories)
            {
                if (c.CategoryName == "阴阳怪气词库") return;
            }

            var defaultLib = JsonHelper.DeserializeManifestJson<WordsLibrary>("wordsLibrary.json");
            if (defaultLib?.Categories != null)
            {
                foreach (var c in defaultLib.Categories)
                {
                    if (c.CategoryName == "阴阳怪气词库")
                    {
                        c.Library = Library;
                        foreach (var w in c.Words) w.Category = c;
                        Library.Categories.Add(c);
                        JsonHelper.SerializeWordsLibrary(Library);
                        break;
                    }
                }
            }
        }

        private DelegateCommand _VisitGitHubCommand;
        public DelegateCommand VisitGitHubCommand => _VisitGitHubCommand ?? (_VisitGitHubCommand = new DelegateCommand(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://github.com/Mongovane/ZuAnBot") { UseShellExecute = true });
            }
            catch (Exception e)
            {
                e.Show();
            }
        }));

        private DelegateCommand _SetCommand;
        public DelegateCommand SetCommand => _SetCommand ?? (_SetCommand = new DelegateCommand(() =>
        {
            try
            {
                var parameters = new DialogParameters { { Params.Library, Library } };
                IDialogResult r = null;
                _dialogService.ShowDialog(nameof(WordsLibrarySet), parameters, result => r = result);

                if (r != null && r.Result == ButtonResult.OK)
                    JsonHelper.SerializeWordsLibrary(Library);
                else
                    LoadWordsLibrary();
            }
            catch (Exception e)
            {
                e.Show();
            }
        }));

        private DelegateCommand _AiSetCommand;
        public DelegateCommand AiSetCommand => _AiSetCommand ?? (_AiSetCommand = new DelegateCommand(() =>
        {
            try
            {
                var parameters = new DialogParameters { { Params.AiConfig, AiSetting } };
                _dialogService.ShowDialog(nameof(AiSettingsView), parameters, result =>
                {
                    if (result != null && result.Result == ButtonResult.OK)
                        AiSetting = result.Parameters.GetValue<AiConfig>(Params.AiConfig) ?? AiConfig.Load();
                    else
                        AiSetting = AiConfig.Load();

                    IsZeroWidthAntiFilter = AiSetting.UseZeroWidthAntiFilter;
                    IsOverlayVisible = AiSetting.ShowInGameOverlay;
                });
            }
            catch (Exception e)
            {
                e.Show();
            }
        }));

        private DelegateCommand _ManualSnipOcrCommand;
        public DelegateCommand ManualSnipOcrCommand =>
            _ManualSnipOcrCommand ?? (_ManualSnipOcrCommand = new DelegateCommand(async () => await ExecuteManualSnipOcrAsync()));

        private DelegateCommand _QuickChatOcrCommand;
        public DelegateCommand QuickChatOcrCommand =>
            _QuickChatOcrCommand ?? (_QuickChatOcrCommand = new DelegateCommand(async () => await ExecuteQuickChatOcrAsync()));

        private bool _UpdateEnabled = true;
        public bool UpdateEnabled
        {
            get => _UpdateEnabled;
            set => SetProperty(ref _UpdateEnabled, value);
        }

        private DelegateCommand _UpdateCommand;
        public DelegateCommand UpdateCommand => _UpdateCommand ?? (_UpdateCommand = new DelegateCommand(ExecuteUpdateCommand).ObservesCanExecute(() => UpdateEnabled));
        async void ExecuteUpdateCommand()
        {
            try
            {
                UpdateEnabled = false;
                var latestVersion = await VersionHelper.GetLatestVersion();
                if (VersionHelper.IsNewestVersion(latestVersion))
                {
                    MessageHelper.Info("当前版本已经是最新版本");
                }
                else
                {
                    var result = MessageHelper.Question($"当前版本为{VersionHelper.GetCurrentVersionName()}，最新版本为{latestVersion.VersionName}，是否安全更新？");
                    if (result)
                    {
                        var tempDir = Path.GetTempPath();
                        var file = await _apis.DownloadFileAsync(tempDir, latestVersion.FileName, latestVersion.Url, latestVersion.Sha256);

                        var assembly = Assembly.GetExecutingAssembly();
                        var stream = assembly.GetManifestResourceStream("costura.canyonechoupdate.exe");
                        if (stream == null)
                        {
                            MessageHelper.Error("未找到内嵌更新组件");
                            return;
                        }
                        var tempPath = Path.Combine(Path.GetTempPath(), "CanyonEchoUpdate.exe");
                        using (var tempStream = File.Create(tempPath))
                        {
                            stream.CopyTo(tempStream);
                        }

                        // 修复: 原版 Sha256 为空时传 "SKIP" 哨兵, 更新器见到 SKIP 就跳过校验。
                        // 哨兵字符串一旦被服务端 / 中间人塞进 VersionDto 就能绕过校验。
                        // 修复后直接传空串, 更新器空串 = 不校验 (并在日志里警告), 没有魔法字符串。
                        string expectedHash = latestVersion.Sha256 ?? "";
                        int currentPid = Process.GetCurrentProcess().Id;
                        var startInfo = new ProcessStartInfo(tempPath, $""{file}" "{assembly.Location}" "{expectedHash}" {currentPid}")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        Process.Start(startInfo);
                    }
                }
            }
            catch (Exception e)
            {
                e.Show();
            }
            finally
            {
                UpdateEnabled = true;
            }
        }
        #endregion

        private void HookKeys()
        {
            _hook = new GlobalKeyboardHook();
            _hook.KeyUp += Hook_KeyUp;
            _hook.HookedKeys.Add(Keys.F2);
            _hook.HookedKeys.Add(Keys.F3);
            _hook.HookedKeys.Add(Keys.F4);
            _hook.HookedKeys.Add(Keys.F5);
            _hook.HookedKeys.Add(Keys.F6);
            _hook.HookedKeys.Add(Keys.F7);
            _hook.HookedKeys.Add(Keys.F8);
            _hook.HookedKeys.Add(Keys.F11);
            _hook.HookedKeys.Add(Keys.F12);
            _hook.hook();
        }

        private async void Hook_KeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                // 300ms 防抖保护，避免机械键盘连击导致重复触发
                if ((DateTime.Now - _lastTriggerTime).TotalMilliseconds < 300)
                    return;
                _lastTriggerTime = DateTime.Now;

                if (e.KeyCode == Keys.F11)
                {
                    IsAll = !IsAll;
                    return;
                }
                if (e.KeyCode == Keys.F12)
                {
                    IsPerWord = !IsPerWord;
                    return;
                }
                if (e.KeyCode == Keys.F7)
                {
                    IsOverlayVisible = !IsOverlayVisible;
                    return;
                }
                if (e.KeyCode == Keys.F8)
                {
                    IsZeroWidthAntiFilter = !IsZeroWidthAntiFilter;
                    return;
                }
                if (e.KeyCode == Keys.F4)
                {
                    await ExecuteManualSnipOcrAsync();
                    return;
                }
                if (e.KeyCode == Keys.F5)
                {
                    await ExecuteQuickChatOcrAsync();
                    return;
                }

                string word = "";
                if (e.KeyCode == Keys.F2)
                    word = Library.GetLoacalWord("默认词库");
                else if (e.KeyCode == Keys.F3)
                    word = Library.GetLoacalWord("自定义词库");
                else if (e.KeyCode == Keys.F6)
                    word = Library.GetLoacalWord("阴阳怪气词库");
                else
                    return;

                LastAiReply = $"[词库快捷发送] {word}";
                await Task.Run(() => SendWordToGameAsync(word));
            }
            catch (ArgumentOutOfRangeException ex)
            {
                MessageHelper.Error(string.IsNullOrWhiteSpace(ex.Message) ? "词库为空" : ex.Message);
            }
            catch (Exception ex)
            {
                ex.Show();
            }
        }

        private async Task ExecuteManualSnipOcrAsync()
        {
            if (_isProcessingOcr) return;
            _isProcessingOcr = true;

            try
            {
                Bitmap captured = null;
                Rectangle selectedRect = Rectangle.Empty;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var snipWin = new SnippingWindow();
                    if (snipWin.ShowDialog() == true && snipWin.CroppedBitmap != null)
                    {
                        captured = snipWin.CroppedBitmap;
                        selectedRect = snipWin.SelectedPhysicalRegion;
                    }
                });

                if (captured == null) return;

                using (captured)
                {
                    if (AiSetting != null && AiSetting.RememberLastSnipRegion && selectedRect.Width > 20)
                    {
                        AiSetting.ChatBoxX = selectedRect.X;
                        AiSetting.ChatBoxY = selectedRect.Y;
                        AiSetting.ChatBoxWidth = selectedRect.Width;
                        AiSetting.ChatBoxHeight = selectedRect.Height;
                        AiSetting.Save();
                    }

                    await ProcessCapturedChatBitmapAsync(captured);
                }
            }
            finally
            {
                _isProcessingOcr = false;
            }
        }

        private async Task ExecuteQuickChatOcrAsync()
        {
            if (_isProcessingOcr) return;
            _isProcessingOcr = true;

            try
            {
                var region = OcrHelper.GetLolChatBoxRegion(AiSetting);
                using (var captured = OcrHelper.CaptureScreenRegion(region))
                {
                    if (captured == null) return;
                    await ProcessCapturedChatBitmapAsync(captured);
                }
            }
            finally
            {
                _isProcessingOcr = false;
            }
        }

        private async Task ProcessCapturedChatBitmapAsync(Bitmap chatBitmap)
        {
            LastOcrText = "正在进行 OCR 识别...";
            string ocrText = await OcrHelper.RecognizeTextAsync(chatBitmap);

            if (string.IsNullOrWhiteSpace(ocrText))
            {
                LastOcrText = "未识别到聊天框文字，请按 F4 框选聊天框文字区域";
                return;
            }

            LastOcrText = ocrText.Replace("
", " ").Replace("
", " | ");
            LastAiReply = "正在生成阴阳怪气反击...";

            var (reply, sourceDesc, parseInfo) = await AiReplyService.GenerateCounterReplyAsync(ocrText, AiSetting, Library);
            if (parseInfo?.IsEnemySpeaker != null)
            {
                IsAll = parseInfo.IsEnemySpeaker.Value;
            }

            if (string.IsNullOrWhiteSpace(reply))
            {
                LastAiReply = sourceDesc;
                return;
            }

            LastAiReply = $"[{sourceDesc}] {reply}";

            if (AiSetting == null || AiSetting.AutoSendToGame)
            {
                // v4.3.0: roast 模式才限流, 干净模式不烧 API 也不招举报, 不限。
                // 超上限/冷却中就不发, 只在界面上写原因, 免得一局喷太多被禁言。
                if (AiSetting != null && AiSetting.UseRoastMode)
                {
                    _roastLimiter.CooldownSeconds = AiSetting.RoastCooldownSeconds;
                    _roastLimiter.MaxPerMatch = AiSetting.RoastMaxPerMatch;
                    if (!_roastLimiter.TryAllowRoast(DateTime.Now, out string denyReason))
                    {
                        LastAiReply = $"[限流未发送] {denyReason} (已生成但没发: {reply})";
                        return;
                    }
                }
                await Task.Delay(100);
                await SendWordToGameAsync(reply);
            }
        }

        private async Task SendWordToGameAsync(string word)
        {
            bool useClipboard = AiSetting == null || AiSetting.UseFastClipboardSend;
            await GameSendHelper.SendToLolChatAsync(
                word,
                IsAll,
                IsPerWord,
                useClipboard,
                IsZeroWidthAntiFilter,
                AiSetting == null || AiSetting.UseSwearObfuscation);
        }
    }
}
