using System.IO;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    public interface IExcelChecklistImporter
    {
        ChecklistImportPreview Parse(Stream xlsxStream, string fileName);
    }
}
