using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// 全局低级键盘钩子（修复版：实现 IDisposable 确定性释放句柄，防止系统钩子泄露与超时失灵）
    /// v4.2.1 修了截图里确认的 Bug 2:
    /// 原版 keyBoardHookStruct.dwExtraInfo 声明成 int (4 字节), 而原生 KBDLLHOOKSTRUCT
    /// 里它是 ULONG_PTR, 64 位下 8 字节, 结构体一共应该是 24 字节不是 20 字节。
    /// 现在 dwExtraInfo 改成 UIntPtr, wParam 改成 IntPtr, 结构体显式标
    /// [StructLayout(LayoutKind.Sequential)], HookProc / CallNextHookEx 的签名也按
    /// 原生 LowLevelKeyboardProc / CallNextHookEx 对齐 (nCode int, wParam WPARAM,
    /// 返回 LRESULT)。现在只读 vkCode (偏移 0) 所以碰巧没出事, 但回传结构体
    /// 或者读后面字段时旧写法一定错。
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

        public IntPtr HookProc(int code, IntPtr wParam, ref keyBoardHookStruct lParam)
        {
            if (code >= 0)
            {
                Keys key = (Keys)lParam.vkCode;
                if (HookedKeys.Contains(key))
                {
                    KeyEventArgs kArg = new KeyEventArgs(key);
                    if ((wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN) && KeyDown != null)
                    {
                        KeyDown(this, kArg);
                    }
                    else if ((wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP) && KeyUp != null)
                    {
                        KeyUp(this, kArg);
                    }
                    if (kArg.Handled)
                    {
                        return (IntPtr)1;
                    }
                }
            }
            return CallNextHookEx(_hookHandle, code, wParam, ref lParam);
        }

        [DllImport("user32.dll", CharSet = CharSet.None, ExactSpelling = false)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int code, IntPtr wParam, ref keyBoardHookStruct lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.None, ExactSpelling = false)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("user32.dll", CharSet = CharSet.None, ExactSpelling = false)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LLKeyboardHook callback, IntPtr hInstance, uint threadID);

        [DllImport("user32.dll", CharSet = CharSet.None, ExactSpelling = false)]
        private static extern bool UnhookWindowsHookEx(IntPtr hInstance);

        public event KeyEventHandler KeyDown;
        public event KeyEventHandler KeyUp;

        [StructLayout(LayoutKind.Sequential)]
        public struct keyBoardHookStruct
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public UIntPtr dwExtraInfo;
        }

        public delegate IntPtr LLKeyboardHook(int code, IntPtr wParam, ref keyBoardHookStruct lParam);
    }
}
