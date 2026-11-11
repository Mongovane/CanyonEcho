using System.Windows;

namespace CanyonEcho_Wpf.Helper
{
    public static class MessageHelper
    {
        public static void Error(string msg)
        {
            MessageBox.Show(msg, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static void Info(string msg)
        {
            MessageBox.Show(msg, "通知", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static bool Warning(string msg)
        {
            return MessageBox.Show(msg, "警告", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;
        }

        public static bool Question(string msg)
        {
            return MessageBox.Show(msg, "确认", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
        }
    }
}
