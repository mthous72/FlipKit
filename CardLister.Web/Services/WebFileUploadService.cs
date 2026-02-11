using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FlipKit.Core.Services;

namespace FlipKit.Web.Services
{
    /// <summary>
    /// Web implementation of IFileDialogService.
    /// In web applications, file uploads are handled via IFormFile in controllers,
    /// not through a dialog service. This implementation provides guidance.
    /// </summary>
    public class WebFileUploadService : IFileDialogService
    {
        public Task<string?> OpenImageFileAsync()
        {
            throw new NotSupportedException(
                "File upload dialogs are not supported in web applications. " +
                "Use <input type=\"file\" accept=\"image/*\"> in your view and IFormFile in controller.");
        }

        public Task<List<string>> OpenImageFilesAsync()
        {
            throw new NotSupportedException(
                "File upload dialogs are not supported in web applications. " +
                "Use <input type=\"file\" accept=\"image/*\" multiple> in your view and IFormFileCollection in controller.");
        }

        public Task<string?> SaveCsvFileAsync(string defaultFileName)
        {
            throw new NotSupportedException(
                "File save dialogs are not supported in web applications. " +
                "Return File(bytes, contentType, fileName) from controller for downloads.");
        }

        public Task<string?> OpenFileAsync(string title, string[] extensions)
        {
            throw new NotSupportedException(
                "File open dialogs are not supported in web applications. " +
                "Use <input type=\"file\"> in your view and IFormFile in controller.");
        }

        public Task<string?> SaveFileAsync(string title, string defaultFileName, string[] extensions)
        {
            throw new NotSupportedException(
                "File save dialogs are not supported in web applications. " +
                "Return File(bytes, contentType, fileName) from controller for downloads.");
        }
    }
}
