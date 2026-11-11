using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CanyonEcho_Wpf.Helper;
using Point = System.Windows.Point;

namespace CanyonEcho_Wpf.Views
{
    /// <summary>
    /// 手动框选截图窗口（支持高分屏 DPI 物理像素换算）
    /// </summary>
    public partial class SnippingWindow : Window
    {
        private bool _isDragging;
        private Point _startPoint;

        /// <summary>
        /// 截取到的位图
        /// </summary>
        public Bitmap CroppedBitmap { get; private set; }

        /// <summary>
        /// 截取的屏幕物理像素矩形区域（可用于记忆聊天框坐标）
        /// </summary>
        public Rectangle SelectedPhysicalRegion { get; private set; }

        public SnippingWindow()
        {
            InitializeComponent();

            // 覆盖整个虚拟屏幕（支持多显示器）
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _startPoint = e.GetPosition(DragCanvas);

            Canvas.SetLeft(SelectionBorder, _startPoint.X);
            Canvas.SetTop(SelectionBorder, _startPoint.Y);
            SelectionBorder.Width = 0;
            SelectionBorder.Height = 0;
            SelectionBorder.Visibility = Visibility.Visible;
            SizeTipBorder.Visibility = Visibility.Visible;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            var currentPoint = e.GetPosition(DragCanvas);
            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double w = Math.Abs(_startPoint.X - currentPoint.X);
            double h = Math.Abs(_startPoint.Y - currentPoint.Y);

            Canvas.SetLeft(SelectionBorder, x);
            Canvas.SetTop(SelectionBorder, y);
            SelectionBorder.Width = w;
            SelectionBorder.Height = h;

            Canvas.SetLeft(SizeTipBorder, x);
            Canvas.SetTop(SizeTipBorder, Math.Max(0, y - 24));
            SizeTipText.Text = $"{(int)w} × {(int)h}";
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;
            _isDragging = false;

            var endPoint = e.GetPosition(DragCanvas);
            double x = Math.Min(_startPoint.X, endPoint.X);
            double y = Math.Min(_startPoint.Y, endPoint.Y);
            double w = Math.Abs(_startPoint.X - endPoint.X);
            double h = Math.Abs(_startPoint.Y - endPoint.Y);

            if (w < 12 || h < 10)
            {
                DialogResult = false;
                Close();
                return;
            }

            // 计算 WPF 逻辑坐标到屏幕物理像素的 DPI 缩放比例
            double dpiScaleX = 1.0;
            double dpiScaleY = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                Matrix m = source.CompositionTarget.TransformToDevice;
                dpiScaleX = m.M11;
                dpiScaleY = m.M22;
            }

            int physX = (int)((Left + x) * dpiScaleX);
            int physY = (int)((Top + y) * dpiScaleY);
            int physW = (int)(w * dpiScaleX);
            int physH = (int)(h * dpiScaleY);

            // 先隐藏遮罩窗口，避免把半透明绿框截进去
            Hide();
            System.Threading.Thread.Sleep(60);

            SelectedPhysicalRegion = new Rectangle(physX, physY, physW, physH);
            CroppedBitmap = OcrHelper.CaptureScreenRegion(SelectedPhysicalRegion);

            DialogResult = CroppedBitmap != null;
            Close();
        }
    }
}
