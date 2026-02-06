using System.Threading.Tasks;

namespace CardLister.Services
{
    public interface IFileDialogService
    {
        Task<string?> OpenImageFileAsync();
        Task<string?> SaveCsvFileAsync(string defaultFileName);
        Task<string?> OpenFileAsync(string title, string[] extensions);
        Task<string?> SaveFileAsync(string title, string defaultFileName, string[] extensions);
    }
}
