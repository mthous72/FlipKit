using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    public interface IChecklistFileMetadataExtractor
    {
        ChecklistImportMetadata Extract(string fileName);
    }
}
