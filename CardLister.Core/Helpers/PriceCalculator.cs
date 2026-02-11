using System;

namespace FlipKit.Core.Helpers
{
    public static class PriceCalculator
    {
        /// <summary>
        /// Calculate net payout after platform fees.
        /// Default Whatnot: 8% commission + 2.9% processing + $0.30
        /// </summary>
        public static decimal CalculateNet(decimal salePrice, decimal feePercent = 11m)
        {
            var feeRate = 1m - (feePercent / 100m);
            return Math.Max(0, salePrice * feeRate - 0.30m);
        }

        /// <summary>
        /// Calculate minimum price to break even given cost basis.
        /// </summary>
        public static decimal CalculateBreakEven(decimal costBasis, decimal feePercent = 11m)
        {
            var feeRate = 1m - (feePercent / 100m);
            if (feeRate <= 0) return costBasis;
            return Math.Ceiling((costBasis + 0.30m) / feeRate * 100) / 100;
        }

        /// <summary>
        /// Calculate total fees on a sale.
        /// </summary>
        public static decimal CalculateFees(decimal salePrice, decimal feePercent = 11m)
        {
            return salePrice * (feePercent / 100m) + 0.30m;
        }
    }
}
