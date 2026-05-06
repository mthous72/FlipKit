using System.Threading.Tasks;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services.Interfaces
{
    public interface IEbayPublishingService
    {
        bool IsAuthorized { get; }
        string BuildAuthorizationUrl();
        Task ExchangeCodeForTokensAsync(string authCode);
        Task<EbayPublishResult> PublishListingAsync(Card card);
        Task<bool> FetchAndStorePoliciesAsync();
    }
}
