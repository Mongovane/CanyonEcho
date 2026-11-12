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
    /// 4. v4.2.0 脏话隐形: 发送前先用 SwearObfuscator 把原形脏话换成同音/变体字,
    ///    并只在脏话词内插零宽, 免得硬骂被聊天框吞字。F8 全句零宽和它二选一,
    ///    隐形开了就不做全句零宽, 不然长度翻倍还容易顶爆字数上限。
    /// </summary>
    public static class GameSendHelper
    {
        private const char ZeroWidthSpace = '\u200B';

        /// <summary>
        /// 在字符串相邻字符间穿插不可见零宽空格，绕过游戏连续词组正则屏蔽
        /// </summary>
        private static bool IsCjk(char c)
        {
            return (c >= '\u4E00' && c <= '\u9FFF') || (c >= '\u3400' && c <= '\u4DBF');
        }

        public static string ApplyZeroWidthAntiFilter(string input)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= 1) return input;

            // 修复: 原版在每个字符间都插 \u200B, 连空格/ASCII/标点都不放过, 长度翻倍还
            // 可能顶爆 LoL 聊天字数上限。修复后只在相邻两个汉字之间插, 空格和 ASCII 不动。
            var sb = new StringBuilder(input.Length * 2);
            for (int i = 0; i < input.Length; i++)
            {
                sb.Append(input[i]);
                if (i < input.Length - 1 && IsCjk(input[i]) && IsCjk(input[i + 1]))
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
            bool useZeroWidthAntiFilter,
            bool useSwearObfuscation = true)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // v4.2.0: 先隐形脏话, 再决定要不要全句零宽。隐形本身已在脏话词内插了零宽,
            // 全句零宽就没必要了, 两个都开只会把长度翻倍, 更容易被吞。
            if (useSwearObfuscation)
            {
                text = SwearObfuscator.Obfuscate(text);
            }

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

            string payload = (useZeroWidthAntiFilter && !useSwearObfuscation) ? ApplyZeroWidthAntiFilter(text) : text;
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

                // 给游戏 120ms 吃掉 Ctrl+V, 再恢复剪贴板; 且只有剪贴板还等于我们
                // 刚粘贴的内容时才恢复, 避免覆盖用户在这 120ms 内自己复制的新内容。
                await Task.Delay(120);
                if (backupClipboard != null)
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        try
                        {
                            if (Clipboard.ContainsText() && Clipboard.GetText() == fullLine)
                                Clipboard.SetText(backupClipboard);
                        }
                        catch { }
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
