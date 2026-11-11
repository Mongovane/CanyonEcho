using System;

namespace CanyonEcho_Wpf.Helper
{
    public class WordsHelper
    {
        public static void EnsureValidContent(string value)
        {
            if (!IsVaileContent(value, out string msg))
            {
                throw new ArgumentOutOfRangeException(msg, innerException: null);
            }
        }

        private static bool IsVaileContent(string value, out string msg)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                msg = "词条内容不能为空";
                return false;
            }
            if (value.Length > 200)
            {
                msg = "词条内容不能超过200个字符";
                return false;
            }

            msg = "";
            return true;
        }

        public static bool IsVaileContent(string value)
        {
            return IsVaileContent(value, out _);
        }
    }
}
