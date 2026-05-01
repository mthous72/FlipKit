using System.Collections.Generic;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services.Export;

namespace FlipKit.Core.Services
{
    public interface IExportService
    {
        string GenerateTitle(Card card);
        string GenerateDescription(Card card);
        Task ExportCsvAsync(List<Card> cards, string outputPath);
        Task ExportCsvAsync(List<Card> cards, string outputPath, ExportPlatform platform);
        List<string> ValidateCardForExport(Card card);

        /// <summary>
        /// Runs the full pre-flight validator over the batch and returns structured errors
        /// (with severity). Use this in preference to <see cref="ValidateCardForExport"/>
        /// when the caller wants to render per-row details rather than human-readable strings.
        /// </summary>
        IReadOnlyList<ExportRowError> ValidateBatch(IList<Card> cards, ExportPlatform platform);

        Task ExportTaxCsvAsync(List<Card> soldCards, string outputPath);
    }
}
