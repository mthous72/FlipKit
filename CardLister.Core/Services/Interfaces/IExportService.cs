using System.Collections.Generic;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Services
{
    public interface IExportService
    {
        string GenerateTitle(Card card);
        string GenerateDescription(Card card);
        Task ExportCsvAsync(List<Card> cards, string outputPath);
        Task ExportCsvAsync(List<Card> cards, string outputPath, ExportPlatform platform);
        List<string> ValidateCardForExport(Card card);
        Task ExportTaxCsvAsync(List<Card> soldCards, string outputPath);
    }
}
