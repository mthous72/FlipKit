using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    public interface IEbayListingImportService
    {
        /// <summary>
        /// Reads the eBay Seller Hub CSV, runs the rule-pass title parser on
        /// every row, sends all titles to the LLM enricher in batches, and
        /// builds a preview with proposed Card rows for the user to review.
        /// Does not touch the database.
        /// </summary>
        Task<EbayListingImportPreview> ParseAsync(
            Stream csvStream,
            string sourceFileName,
            CancellationToken ct = default);

        /// <summary>
        /// Persists the preview's non-skipped rows. Updates existing cards
        /// matched by <see cref="Card.EbayItemId"/>, inserts the rest.
        /// </summary>
        Task<EbayListingImportResult> CommitAsync(
            EbayListingImportPreview preview,
            CancellationToken ct = default);
    }
}
