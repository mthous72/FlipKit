using System.Collections.Generic;
using System.Threading.Tasks;

namespace FlipKit.Core.Services
{
    public interface IFileDialogService
    {
        Task<string?> OpenImageFileAsync();
        Task<List<string>> OpenImageFilesAsync();
        Task<string?> SaveCsvFileAsync(string defaultFileName);
        Task<string?> OpenFileAsync(string title, string[] extensions);
        Task<string?> SaveFileAsync(string title, string defaultFileName, string[] extensions);
    }
}
