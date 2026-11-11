using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace CanyonEcho_Wpf.Helper
{
    /// <summary>
    /// 安全加固版 IPC 进程间通信（修复原版 LPStr 编码乱码、未校验消息令牌及未释放非托管内存漏洞）
    /// </summary>
    public class IPCHelper
    {
        public const int WM_COPYDATA = 0x004A;
        private static readonly IntPtr MAGIC_TOKEN = new IntPtr(0x43454348); // "CECH" 签名校验令牌
        private const int MAX_IPC_BYTES = 512;

        public static void SendMessage(string message, IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || string.IsNullOrEmpty(message)) return;

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(message);
            if (utf8Bytes.Length > MAX_IPC_BYTES) return;

            IntPtr unmanagedPtr = Marshal.AllocHGlobal(utf8Bytes.Length);
            try
            {
                Marshal.Copy(utf8Bytes, 0, unmanagedPtr, utf8Bytes.Length);
                CopyDataStruct cds = new CopyDataStruct
                {
                    dwData = MAGIC_TOKEN,
                    cbData = utf8Bytes.Length,
                    lpData = unmanagedPtr
                };
                SendMessage(hWnd, WM_COPYDATA, IntPtr.Zero, ref cds);
            }
            finally
            {
                Marshal.FreeHGlobal(unmanagedPtr);
            }
        }

        private static Action<string> _handler;

        public static void ObserveMessage(Window window, Action<string> handler)
        {
            _handler = handler;
            if (PresentationSource.FromVisual(window) is HwndSource hwndSource)
            {
                hwndSource.AddHook(WndProc);
            }
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_COPYDATA && lParam != IntPtr.Zero)
            {
                try
                {
                    CopyDataStruct cds = (CopyDataStruct)Marshal.PtrToStructure(lParam, typeof(CopyDataStruct));
                    if (cds.dwData == MAGIC_TOKEN && cds.cbData > 0 && cds.cbData <= MAX_IPC_BYTES && cds.lpData != IntPtr.Zero)
                    {
                        byte[] buffer = new byte[cds.cbData];
                        Marshal.Copy(cds.lpData, buffer, 0, cds.cbData);
                        string text = Encoding.UTF8.GetString(buffer);
                        handled = true;
                        _handler?.Invoke(text);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Log.Warn("拦截到异常格式的 WM_COPYDATA 消息", ex);
                }
            }
            return IntPtr.Zero;
        }

        [DllImport("User32.dll", EntryPoint = "SendMessage")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, ref CopyDataStruct lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CopyDataStruct
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }
}
