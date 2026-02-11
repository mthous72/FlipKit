using System.Diagnostics;
using FlipKit.Core.Services;

namespace FlipKit.Desktop.Services
{
    public class SystemBrowserService : IBrowserService
    {
        public void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }
}
