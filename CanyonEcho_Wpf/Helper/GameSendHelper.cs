using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WindowsInput;
using WindowsInput.Events;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// 升级版英雄联盟聊天框消息发送引擎：
    /// 1. 支持极速剪贴板粘贴发送 (Ctrl+V)，彻底解决原版模拟打字被中文输入法截留/吞字的问题
    ///    v4.3.0 修正: 原来注释写的那个八十毫秒是错的, 代码里 Enter 60ms +
    ///    Ctrl+V 60ms + Enter 60ms + 恢复前等 120ms, 理论最少 300ms, 剪贴板
    ///    重试 3 次只会更长。那个数没有任何实测依据, 改成实话。
    /// 2. 支持单行零宽字符穿插防屏蔽 (Zero-Width Anti-Filter)，无需多行刷屏即可绕过敏感词过滤
    /// 3. 完整保留原版逐字纵向刷屏 (IsPerWord) 与所有人 (/all) 模式
    /// 4. v4.2.0 脏话隐形: 发送前先用 SwearObfuscator 把原形脏话换成同音/变体字,
    ///    并只在脏话词内插零宽, 免得硬骂被聊天框吞字。F8 全句零宽和它二选一,
    ///    隐形开了就不做全句零宽, 不然长度翻倍还容易顶爆字数上限。
    /// 5. v4.2.1 修了截图里确认的 2 个 bug:
    ///    - Bug 1 剪贴板: SetText 失败 (剪贴板被别的程序占用, CLIPBRD_E_CANT_OPEN)
    ///      再也不能空 catch 吞掉然后照样 Ctrl+V, 那会把用户原来的东西发进聊天框。
    ///      现在重试 3 次, 写完校验 GetText() == 要发的内容, 写不进去直接中止发送
    ///      并记日志, 备份改成 IDataObject, 图片/文件也不会丢了。
    ///    - Bug 5 代理对: foreach (var ch in text) 会把 emoji / 生僻字 (一个代理对,
    ///      两个 char) 拆成两半分别发, 游戏里就是乱码。现在按字素簇切分
    ///      (StringInfo.GetTextElementEnumerator), 一个 emoji 算一个单元再发。
    ///      ApplyZeroWidthAntiFilter 也改成按字素簇走, 不在代理对中间插零宽。
    /// </summary>
    public static class GameSendHelper
    {
        private const char ZeroWidthSpace = '\u200B';
        private const int ClipboardRetryCount = 3;
        private const int ClipboardRetryDelayMs = 40;

        /// <summary>
        /// 在字符串相邻字符间穿插不可见零宽空格，绕过游戏连续词组正则屏蔽
        /// </summary>
        private static bool IsCjk(char c)
        {
            return (c >= '\u4E00' && c <= '\u9FFF') || (c >= '\u3400' && c <= '\u4DBF');
        }

        private static bool IsSingleCjkElement(string element)
        {
            return element != null && element.Length == 1 && IsCjk(element[0]);
        }

        /// <summary>
        /// 按字素簇切分: 一个 emoji / 一个带变音符号的字算一个单元, 绝不拆代理对。
        /// v4.2.1 新增, 供逐字发送和零宽插入共用。
        /// </summary>
        public static List<string> GetTextElements(string input)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(input)) return result;
            var enumerator = StringInfo.GetTextElementEnumerator(input);
            while (enumerator.MoveNext())
            {
                result.Add(enumerator.GetTextElement());
            }
            return result;
        }

        public static string ApplyZeroWidthAntiFilter(string input)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= 1) return input;

            // 修复: 原版在每个字符间都插 \u200B, 连空格/ASCII/标点都不放过, 长度翻倍还
            // 可能顶爆 LoL 聊天字数上限。修复后只在相邻两个汉字之间插, 空格和 ASCII 不动。
            // v4.2.1: 按字素簇走, emoji / 生僻字 (代理对) 不会被拆开, 零宽也不会插进代理对中间。
            var elements = GetTextElements(input);
            var sb = new StringBuilder(input.Length * 2);
            for (int i = 0; i < elements.Count; i++)
            {
                sb.Append(elements[i]);
                if (i < elements.Count - 1 && IsSingleCjkElement(elements[i]) && IsSingleCjkElement(elements[i + 1]))
                {
                    sb.Append(ZeroWidthSpace);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 在调用方已经处于 STA / UI 线程的前提下写剪贴板文本, 重试 3 次, 写完校验。
        /// 返回 false = 写不进去, 调用方必须中止发送, 绝不能照样 Ctrl+V。
        /// </summary>
        private static bool TrySetClipboardText(string text)
        {
            for (int attempt = 0; attempt < ClipboardRetryCount; attempt++)
            {
                try
                {
                    Clipboard.SetText(text);
                    // 校验: 真写进去了才算成功, 免得剪贴板被占用时静默失败。
                    if (Clipboard.ContainsText() && Clipboard.GetText() == text)
                        return true;
                }
                catch (Exception ex)
                {
                    LogHelper.Log.Warn("剪贴板写入失败, 重试中 (attempt " + (attempt + 1) + "/" + ClipboardRetryCount + ")", ex);
                }
                if (attempt < ClipboardRetryCount - 1)
                    Thread.Sleep(ClipboardRetryDelayMs);
            }
            return false;
        }

        /// <summary>
        /// 恢复剪贴板, 重试 3 次。备份是 IDataObject, 文本/图片/文件都能原样恢复。
        /// 失败只记日志, 不抛异常 (恢复失败不能反过来把聊天框搞坏)。
        /// </summary>
        private static bool TryRestoreClipboard(IDataObject backup)
        {
            if (backup == null) return false;
            for (int attempt = 0; attempt < ClipboardRetryCount; attempt++)
            {
                try
                {
                    Clipboard.SetDataObject(backup, true);
                    return true;
                }
                catch (Exception ex)
                {
                    LogHelper.Log.Warn("剪贴板恢复失败, 重试中 (attempt " + (attempt + 1) + "/" + ClipboardRetryCount + ")", ex);
                }
                if (attempt < ClipboardRetryCount - 1)
                    Thread.Sleep(ClipboardRetryDelayMs);
            }
            return false;
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
            // v4.2.1: 按字素簇发, 一个 emoji 算一个单元, 不再 foreach (var ch in text) 拆代理对。
            if (isPerWord)
            {
                var builder = Simulate.Events();
                foreach (var element in GetTextElements(text))
                {
                    builder = builder
                        .Click(KeyCode.Enter).Wait(85)
                        .Click(allPrefix + element).Wait(85)
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
                IDataObject backupClipboard = null;
                bool setOk = false;
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    try
                    {
                        // v4.2.1: 备份整个 IDataObject, 不只是文本, 用户原来复制的图片/文件才不会丢。
                        backupClipboard = Clipboard.GetDataObject();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Log.Warn("剪贴板备份失败", ex);
                    }
                    setOk = TrySetClipboardText(fullLine);
                });

                // v4.2.1 Bug 1 核心: 写不进去就中止, 绝不能照样 Ctrl+V 把用户原来的东西发出去。
                if (!setOk)
                {
                    LogHelper.Log.Warn("剪贴板写入失败 (重试 3 次后仍失败), 已中止发送, 没有按 Ctrl+V");
                    if (backupClipboard != null)
                    {
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            TryRestoreClipboard(backupClipboard);
                        });
                    }
                    return;
                }

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
                                TryRestoreClipboard(backupClipboard);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Log.Warn("剪贴板恢复前校验失败", ex);
                        }
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
