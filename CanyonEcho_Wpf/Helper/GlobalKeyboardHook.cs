using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// 全局低级键盘钩子（修复版：实现 IDisposable 确定性释放句柄，防止系统钩子泄露与超时失灵）
    /// </summary>
    public class GlobalKeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 256;
        private const int WM_KEYUP = 257;
        private const int WM_SYSKEYDOWN = 260;
        private const int WM_SYSKEYUP = 261;

        private readonly LLKeyboardHook _llkh;
        public List<Keys> HookedKeys = new List<Keys>();
        private IntPtr _hookHandle = IntPtr.Zero;
        private bool _disposed;

        public GlobalKeyboardHook()
        {
            _llkh = new LLKeyboardHook(HookProc);
        }

        ~GlobalKeyboardHook()
        {
            Dispose(false);
        }

        public void hook()
        {
            if (_hookHandle != IntPtr.Zero) return;
            IntPtr hInstance = LoadLibrary("User32");
            _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _llkh, hInstance, 0);
        }

        public void unhook()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            unhook();
            _disposed = true;
        }

        public int HookProc(int code, int wParam, ref keyBoardHookStruct lParam)
        {
            if (code >= 0)
            {
                Keys key = (Keys)lParam.vkCode;
                if (HookedKeys.Contains(key))
                {
                    KeyEventArgs kArg = new KeyEventArgs(key);
                    if ((wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN) && KeyDown != null)
                    {
                        KeyDown(this, kArg);
                    }
                    else if ((wParam == WM_KEYUP || wParam == WM_SYSKEYUP) && KeyUp != null)
                    {
                        KeyUp(this, kArg);
                    }
                    if (kArg.Handled)
                    {
                        return 1;
                    }
                }
            }
            return CallNextHookEx(_hookHandle, code, wParam, ref lParam);
        }

        [DllImport("user32.dll", CharSet = CharSet.None, ExactSpelling = false)]
        private static extern int CallNextHookEx(IntPtr hhk, int code, int wParam, ref keyBoardHookStruct lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.None, ExactSpelling = false)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("user32.dll", CharSet = CharSet.None, ExactSpelling = false)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LLKeyboardHook callback, IntPtr hInstance, uint threadID);

        [DllImport("user32.dll", CharSet = CharSet.None, ExactSpelling = false)]
        private static extern bool UnhookWindowsHookEx(IntPtr hInstance);

        public event KeyEventHandler KeyDown;
        public event KeyEventHandler KeyUp;

        public struct keyBoardHookStruct
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public int dwExtraInfo;
        }

        public delegate int LLKeyboardHook(int code, int wParam, ref keyBoardHookStruct lParam);
    }
}
