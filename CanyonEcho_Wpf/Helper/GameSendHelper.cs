using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using WindowsInput;
using WindowsInput.Events;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// 升级版英雄联盟聊天框消息发送引擎：
    /// 1. 支持极速剪贴板粘贴发送 (Ctrl+V)，耗时仅 80ms，彻底解决原版模拟打字被中文输入法截留/吞字的问题
    /// 2. 支持单行零宽字符穿插防屏蔽 (Zero-Width Anti-Filter)，无需多行刷屏即可绕过敏感词过滤
    /// 3. 完整保留原版逐字纵向刷屏 (IsPerWord) 与所有人 (/all) 模式
    /// </summary>
    public static class GameSendHelper
    {
        private const char ZeroWidthSpace = '\u200B';

        /// <summary>
        /// 在字符串相邻字符间穿插不可见零宽空格，绕过游戏连续词组正则屏蔽
        /// </summary>
        public static string ApplyZeroWidthAntiFilter(string input)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= 1) return input;

            var sb = new StringBuilder(input.Length * 2);
            for (int i = 0; i < input.Length; i++)
            {
                sb.Append(input[i]);
                if (i < input.Length - 1)
                {
                    sb.Append(ZeroWidthSpace);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 向英雄联盟游戏窗口发送聊天消息
        /// </summary>
        public static async Task SendToLolChatAsync(
            string text,
            bool isAll,
            bool isPerWord,
            bool useFastClipboard,
            bool useZeroWidthAntiFilter)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            string allPrefix = isAll ? "/all " : "";

            // 1. 若开启了原版逐字纵向发送 (F12)
            if (isPerWord)
            {
                var builder = Simulate.Events();
                foreach (var ch in text)
                {
                    builder = builder
                        .Click(KeyCode.Enter).Wait(85)
                        .Click(allPrefix + ch).Wait(85)
                        .Click(KeyCode.Enter).Wait(85);
                }
                await builder.Invoke();
                return;
            }

            string payload = useZeroWidthAntiFilter ? ApplyZeroWidthAntiFilter(text) : text;
            string fullLine = allPrefix + payload;

            // 2. 极速剪贴板粘贴发送模式（推荐：快且不吞中文字）
            if (useFastClipboard)
            {
                string backupClipboard = null;
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    try
                    {
                        if (Clipboard.ContainsText())
                            backupClipboard = Clipboard.GetText();
                        Clipboard.SetText(fullLine);
                    }
                    catch { }
                });

                var pasteBuilder = Simulate.Events()
                    .Click(KeyCode.Enter).Wait(60)
                    .ClickChord(KeyCode.Control, KeyCode.V).Wait(60)
                    .Click(KeyCode.Enter).Wait(60);

                await pasteBuilder.Invoke();

                // 恢复用户原本的剪贴板内容
                if (backupClipboard != null)
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        try { Clipboard.SetText(backupClipboard); } catch { }
                    });
                }
                return;
            }

            // 3. 原版模拟键盘直接输入模式
            var legacyBuilder = Simulate.Events()
                .Click(KeyCode.Enter).Wait(100)
                .Click(fullLine).Wait(100)
                .Click(KeyCode.Enter).Wait(100);

            await legacyBuilder.Invoke();
        }
    }
}
