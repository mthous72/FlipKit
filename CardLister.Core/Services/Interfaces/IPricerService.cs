using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    public interface IPricerService
    {
        string BuildTerapeakUrl(Card card);
        string BuildEbaySoldUrl(Card card);
        decimal SuggestPrice(decimal estimatedValue, Card card);
        decimal CalculateNet(decimal salePrice, decimal feePercent = 11m);
    }
}
