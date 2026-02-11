using System.Threading.Tasks;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    public interface ISettingsService
    {
        AppSettings Load();
        void Save(AppSettings settings);
        bool HasValidConfig();
        Task<bool> TestOpenRouterConnectionAsync(string apiKey);
        Task<bool> TestImgBBConnectionAsync(string apiKey);
    }
}
