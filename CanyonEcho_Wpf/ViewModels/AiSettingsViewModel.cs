using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Windows;
using CanyonEcho_Wpf.Constants;
using CanyonEcho_Wpf.Helper;
using CanyonEcho_Wpf.Views;

namespace CanyonEcho_Wpf.ViewModels
{
    /// <summary>
    /// AI 与 OCR 反击设置对话框 ViewModel
    /// </summary>
    public class AiSettingsViewModel : BindableBase, IDialogAware
    {
        private AiConfig _Config;
        public AiConfig Config
        {
            get => _Config;
            set => SetProperty(ref _Config, value);
        }

        private string _TestInputText = "[所有人] 疾风剑豪(亚索): 打野你会不会玩？0-5还在刷？";
        public string TestInputText
        {
            get => _TestInputText;
            set => SetProperty(ref _TestInputText, value);
        }

        private string _TestResultText = "点击【测试生成】查看 AI 或内置引擎的阴阳怪气回复效果";
        public string TestResultText
        {
            get => _TestResultText;
            set => SetProperty(ref _TestResultText, value);
        }

        public string Title => "AI 粗口 Roast 骂人 & OCR 识别设置";

        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters != null && parameters.ContainsKey(Params.AiConfig))
            {
                Config = parameters.GetValue<AiConfig>(Params.AiConfig);
            }
            else
            {
                Config = AiConfig.Load();
            }
        }

        // 一键套用主流 LLM 服务商配置预设
        private DelegateCommand<string> _ApplyPresetCommand;
        public DelegateCommand<string> ApplyPresetCommand =>
            _ApplyPresetCommand ?? (_ApplyPresetCommand = new DelegateCommand<string>(ExecuteApplyPreset));

        private void ExecuteApplyPreset(string provider)
        {
            if (Config == null) return;
            switch (provider)
            {
                case "DeepSeek":
                    Config.ApiBaseUrl = "https://api.deepseek.com/v1";
                    Config.ModelName = "deepseek-chat";
                    break;
                case "Grok":
                    Config.ApiBaseUrl = "https://api.x.ai/v1";
                    Config.ModelName = "grok-3-fast";
                    break;
                case "OpenAI":
                    Config.ApiBaseUrl = "https://api.openai.com/v1";
                    Config.ModelName = "gpt-4o-mini";
                    break;
                case "Qwen":
                    Config.ApiBaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1";
                    Config.ModelName = "qwen-plus";
                    break;
                case "Kimi":
                    Config.ApiBaseUrl = "https://api.moonshot.cn/v1";
                    Config.ModelName = "moonshot-v1-8k";
                    break;
                case "Ollama":
                    Config.ApiBaseUrl = "http://localhost:11434/v1";
                    Config.ModelName = "qwen2.5:7b";
                    break;
            }
        }

        // 标定英雄联盟聊天框坐标
        private DelegateCommand _CalibrateRegionCommand;
        public DelegateCommand CalibrateRegionCommand =>
            _CalibrateRegionCommand ?? (_CalibrateRegionCommand = new DelegateCommand(ExecuteCalibrateRegion));

        private void ExecuteCalibrateRegion()
        {
            try
            {
                var snip = new SnippingWindow();
                if (snip.ShowDialog() == true)
                {
                    var rect = snip.SelectedPhysicalRegion;
                    Config.ChatBoxX = rect.X;
                    Config.ChatBoxY = rect.Y;
                    Config.ChatBoxWidth = rect.Width;
                    Config.ChatBoxHeight = rect.Height;
                    snip.CroppedBitmap?.Dispose();
                }
            }
            catch (Exception ex)
            {
                ex.Show();
            }
        }

        // 重置为默认聊天框区域
        private DelegateCommand _ResetRegionCommand;
        public DelegateCommand ResetRegionCommand =>
            _ResetRegionCommand ?? (_ResetRegionCommand = new DelegateCommand(() =>
            {
                Config.ChatBoxX = 0;
                Config.ChatBoxY = 0;
                Config.ChatBoxWidth = 0;
                Config.ChatBoxHeight = 0;
            }));

        // 测试生成阴阳怪气反击
        private DelegateCommand _TestGenerateCommand;
        public DelegateCommand TestGenerateCommand =>
            _TestGenerateCommand ?? (_TestGenerateCommand = new DelegateCommand(ExecuteTestGenerate));

        private async void ExecuteTestGenerate()
        {
            try
            {
                TestResultText = "正在生成阴阳怪气反击...";
                var (reply, sourceDesc, parseInfo) = await AiReplyService.GenerateCounterReplyAsync(TestInputText, Config);
                if (string.IsNullOrWhiteSpace(reply))
                {
                    TestResultText = $"[未触发] {sourceDesc}";
                }
                else
                {
                    TestResultText = $"【{sourceDesc}】{reply}
（提取发言人：{parseInfo.Speaker} | 清洗内容：{parseInfo.CleanMessage}）";
                }
            }
            catch (Exception ex)
            {
                TestResultText = $"生成出错：{ex.Message}";
            }
        }

        private DelegateCommand _SaveCommand;
        public DelegateCommand SaveCommand =>
            _SaveCommand ?? (_SaveCommand = new DelegateCommand(() =>
            {
                Config?.Save();
                var p = new DialogParameters { { Params.AiConfig, Config } };
                RequestClose?.Invoke(new DialogResult(ButtonResult.OK, p));
            }));

        private DelegateCommand _CancelCommand;
        public DelegateCommand CancelCommand =>
            _CancelCommand ?? (_CancelCommand = new DelegateCommand(() =>
            {
                RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
            }));
    }
}
