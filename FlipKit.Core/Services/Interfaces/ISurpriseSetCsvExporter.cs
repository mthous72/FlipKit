using System.Threading.Tasks;
using FlipKit.Core.Services.Export;

namespace FlipKit.Core.Services
{
    public interface ISurpriseSetCsvExporter
    {
        /// <summary>
        /// Validates, exports a Surprise Set to a Whatnot-format CSV at
        /// <paramref name="outputPath"/>, and transitions state Draft → Exported.
        /// Returns a failure result (no file written) if any compliance errors are present.
        /// Re-export of an already-Exported set is allowed (idempotent, no state change).
        /// Throws <see cref="InvalidOperationException"/> for Live/Completed/Cancelled sets.
        /// </summary>
        Task<SurpriseSetExportResult> ExportAsync(int setId, string outputPath);
    }
}
