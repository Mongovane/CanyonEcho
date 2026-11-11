using System;

namespace CanyonEcho_Wpf.Api.Dtos
{
    public class VersionDto
    {
        public string Url { get; set; } = "";
        public string FileName { get; set; } = "";
        public string VersionName { get; set; } = "";
        public string Description { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public DateTime Date { get; set; }
    }
}
