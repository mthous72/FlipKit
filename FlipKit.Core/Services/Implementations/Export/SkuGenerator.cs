using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace FlipKit.Core.Services.Export
{
    public class SkuGenerator : ISkuGenerator
    {
        private readonly FlipKitDbContext _db;
        private readonly ISettingsService _settings;

        public SkuGenerator(FlipKitDbContext db, ISettingsService settings)
        {
            _db = db;
            _settings = settings;
        }

        public async Task<string> GenerateNextSkuAsync()
        {
            var s = _settings.Load();
            var prefix = s.SkuPrefix ?? "FK-";
            var padWidth = s.SkuPadWidth > 0 ? s.SkuPadWidth : 6;

            // Pull all existing SKUs matching the prefix into memory and find the max numeric
            // suffix in C#. SQLite GLOB can't restrict to a digits-only suffix cleanly, and a
            // few thousand short strings is trivial to scan. Using MAX (not COUNT) so that
            // deleted SKUs are never reused.
            var existing = await _db.Cards
                .Where(c => c.Sku != null && c.Sku.StartsWith(prefix))
                .Select(c => c.Sku!)
                .ToListAsync();

            int max = 0;
            foreach (var sku in existing)
            {
                var suffix = sku.Substring(prefix.Length);
                if (int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n > max)
                    max = n;
            }

            var next = (max + 1).ToString("D" + padWidth, CultureInfo.InvariantCulture);
            return prefix + next;
        }

        public async Task<bool> IsSkuAvailableAsync(string sku, int? excludeCardId = null)
        {
            if (string.IsNullOrWhiteSpace(sku))
                return false;

            var query = _db.Cards.Where(c => c.Sku == sku);
            if (excludeCardId.HasValue)
                query = query.Where(c => c.Id != excludeCardId.Value);

            return !await query.AnyAsync();
        }
    }
}
