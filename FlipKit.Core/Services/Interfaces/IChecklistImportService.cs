using System.IO;
using System.Threading.Tasks;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    public interface IChecklistImportService
    {
        ChecklistImportPreview Parse(Stream xlsxStream, string fileName);
        Task<ChecklistImportCommitResult> CommitAsync(ChecklistImportPreview preview, bool replaceExisting = true);
    }
}
