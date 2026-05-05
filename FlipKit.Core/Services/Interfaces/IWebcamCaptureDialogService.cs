using System.Threading.Tasks;

namespace FlipKit.Core.Services
{
    public interface IWebcamCaptureDialogService
    {
        Task<string?> CaptureAsync();
    }
}
