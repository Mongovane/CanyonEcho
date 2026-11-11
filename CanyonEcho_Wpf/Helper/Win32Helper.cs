using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using CanyonEcho_Wpf.Constants;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// Win32 API 辅助类（支持前台唤醒与游戏内鼠标穿透无焦点透明悬浮窗）
    /// </summary>
    public static class Win32Helper
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_CLOSE = 0xF060;

        public static bool SetWindowForeground(string winTitle)
        {
            var hWnd = FindWindowEx(IntPtr.Zero, IntPtr.Zero, null, winTitle);
            if (hWnd != IntPtr.Zero)
            {
                SetForegroundWindow(hWnd);
                return true;
            }
            return false;
        }

        public static bool SetMainWindowForeground()
        {
            return SetWindowForeground(Titles.Main);
        }

        public static bool CloseWindow(string winTitle)
        {
            var hWnd = FindWindowEx(IntPtr.Zero, IntPtr.Zero, null, winTitle);
            if (hWnd != IntPtr.Zero)
            {
                SendMessage(hWnd, WM_SYSCOMMAND, SC_CLOSE, 0);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 将 WPF 窗口设置为鼠标穿透、不在 Alt+Tab 中显示、不抢占游戏焦点的外部安全 HUD 悬浮窗
        /// </summary>
        public static void MakeWindowClickThrough(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        }

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr handleParent, IntPtr handleChild, string className, string WindowName);

        [DllImport("User32.dll")]
        private static extern IntPtr SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
    }
}
