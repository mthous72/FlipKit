using System.Threading.Tasks;
using CardLister.Models;

namespace CardLister.Services
{
    public interface IChecklistLearningService
    {
        Task LearnFromCardAsync(Card card);
        Task<ChecklistImportResult> ImportChecklistAsync(string filePath);
        Task ExportChecklistAsync(int checklistId, string outputPath);
    }
}
