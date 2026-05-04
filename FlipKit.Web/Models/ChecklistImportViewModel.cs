using FlipKit.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FlipKit.Web.Models
{
    public class ChecklistImportViewModel
    {
        public IFormFile? UploadedFile { get; set; }

        public int? Year { get; set; }
        public string? Sport { get; set; }
        public string? Manufacturer { get; set; }
        public string? Brand { get; set; }
        public string? SetName { get; set; }

        public ChecklistImportPreview? Preview { get; set; }
        public ChecklistImportCommitResult? CommitResult { get; set; }

        public string? StatusMessage { get; set; }
        public bool IsIosClient { get; set; }
    }
}
