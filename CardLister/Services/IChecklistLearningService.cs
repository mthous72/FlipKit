using System.Collections.Generic;
using System.Threading.Tasks;
using CardLister.Models;

namespace CardLister.Services
{
    public interface IChecklistLearningService
    {
        Task LearnFromCardAsync(Card card);
        Task<ChecklistImportResult> ImportChecklistAsync(string filePath);
        Task ExportChecklistAsync(int checklistId, string outputPath);
        Task<List<SetChecklist>> GetAllChecklistsAsync();
        Task<SetChecklist?> GetChecklistByIdAsync(int id);
        Task<List<MissingChecklist>> GetMissingChecklistsAsync();
        Task DeleteChecklistAsync(int id);
    }
}
