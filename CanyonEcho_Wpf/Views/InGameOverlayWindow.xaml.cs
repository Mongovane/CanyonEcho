using System;
using System.Windows;
using CanyonEcho_Wpf.Helper;

namespace CanyonEcho_Wpf.Views
{
    /// <summary>
    /// 外部安全游戏内透明悬浮窗 HUD（采用 WS_EX_TRANSPARENT | WS_EX_NOACTIVATE 鼠标穿透，不注入游戏进程、零封号风险）
    /// </summary>
    public partial class InGameOverlayWindow : Window
    {
        public InGameOverlayWindow()
        {
            InitializeComponent();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            Win32Helper.MakeWindowClickThrough(this);
        }
    }
}
