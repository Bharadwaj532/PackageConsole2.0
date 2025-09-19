using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PackageConsole
{
    public class FeedbackEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string User { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime Time { get; set; } = DateTime.UtcNow;
        public string Message { get; set; } = string.Empty;
        public string Screenshot { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public string Status { get; set; } = "In Progress";
        public string Severity { get; set; } = string.Empty;
        public string ScreenshotPath { get; set; } = string.Empty;
    }
}
