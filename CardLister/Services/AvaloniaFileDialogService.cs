using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using FlipKit.Core.Services;

namespace FlipKit.Desktop.Services
{
    public class AvaloniaFileDialogService : IFileDialogService
    {
        private IStorageProvider? GetStorageProvider()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow?.StorageProvider;
            return null;
        }

        public async Task<string?> OpenImageFileAsync()
        {
            var sp = GetStorageProvider();
            if (sp == null) return null;

            var result = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Card Image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image Files")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.webp", "*.bmp" }
                    }
                }
            });

            return result.FirstOrDefault()?.Path.LocalPath;
        }

        public async Task<List<string>> OpenImageFilesAsync()
        {
            var sp = GetStorageProvider();
            if (sp == null) return new List<string>();

            var result = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Card Images",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image Files")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.webp", "*.bmp" }
                    }
                }
            });

            return result.Select(f => f.Path.LocalPath).ToList();
        }

        public async Task<string?> SaveCsvFileAsync(string defaultFileName)
        {
            var sp = GetStorageProvider();
            if (sp == null) return null;

            var result = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save CSV File",
                DefaultExtension = "csv",
                SuggestedFileName = defaultFileName,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("CSV Files")
                    {
                        Patterns = new[] { "*.csv" }
                    }
                }
            });

            return result?.Path.LocalPath;
        }

        public async Task<string?> OpenFileAsync(string title, string[] extensions)
        {
            var sp = GetStorageProvider();
            if (sp == null) return null;

            var patterns = extensions.Select(e => $"*.{e}").ToArray();
            var result = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Supported Files")
                    {
                        Patterns = patterns
                    }
                }
            });

            return result.FirstOrDefault()?.Path.LocalPath;
        }

        public async Task<string?> SaveFileAsync(string title, string defaultFileName, string[] extensions)
        {
            var sp = GetStorageProvider();
            if (sp == null) return null;

            var ext = extensions.FirstOrDefault() ?? "json";
            var patterns = extensions.Select(e => $"*.{e}").ToArray();
            var result = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                DefaultExtension = ext,
                SuggestedFileName = defaultFileName,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Supported Files")
                    {
                        Patterns = patterns
                    }
                }
            });

            return result?.Path.LocalPath;
        }
    }
}
