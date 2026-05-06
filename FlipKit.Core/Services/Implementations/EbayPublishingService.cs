using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;
using FlipKit.Core.Models;
using FlipKit.Core.Services.Export;
using FlipKit.Core.Services.Interfaces;

namespace FlipKit.Core.Services
{
    public class EbayPublishingService : IEbayPublishingService
    {
        private const string TokenEndpoint = "https://api.ebay.com/identity/v1/oauth2/token";
        private const string AuthEndpoint  = "https://auth.ebay.com/oauth2/authorize";
        private const string InventoryBase = "https://api.ebay.com/sell/inventory/v1";
        private const string AccountBase   = "https://api.ebay.com/sell/account/v1";
        private const string Scopes        = "https://api.ebay.com/oauth/api_scope/sell.inventory https://api.ebay.com/oauth/api_scope/sell.account.readonly";

        private static readonly JsonSerializerOptions ApiJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _http;
        private readonly ISettingsService _settings;
        private readonly TitleTemplateService _titleService;

        public EbayPublishingService(HttpClient http, ISettingsService settings, TitleTemplateService titleService)
        {
            _http = http;
            _settings = settings;
            _titleService = titleService;
        }

        public bool IsAuthorized
        {
            get
            {
                var s = _settings.Load();
                return !string.IsNullOrEmpty(s.EbayAccessToken)
                    && s.EbayTokenExpiry.HasValue
                    && s.EbayTokenExpiry.Value > DateTime.UtcNow.AddMinutes(5);
            }
        }

        public string BuildAuthorizationUrl()
        {
            var s = _settings.Load();
            if (string.IsNullOrEmpty(s.EbayClientId) || string.IsNullOrEmpty(s.EbayRuName))
                throw new InvalidOperationException("Client ID and RuName must be set before connecting.");

            var q = HttpUtility.ParseQueryString(string.Empty);
            q["client_id"]     = s.EbayClientId;
            q["redirect_uri"]  = s.EbayRuName;
            q["response_type"] = "code";
            q["scope"]         = Scopes;
            return $"{AuthEndpoint}?{q}";
        }

        public async Task ExchangeCodeForTokensAsync(string authCode)
        {
            var s = _settings.Load();
            if (string.IsNullOrEmpty(s.EbayClientId) || string.IsNullOrEmpty(s.EbayClientSecret) || string.IsNullOrEmpty(s.EbayRuName))
                throw new InvalidOperationException("Client ID, Client Secret, and RuName must be set.");

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{s.EbayClientId}:{s.EbayClientSecret}"));

            using var req = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            req.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type",    "authorization_code"),
                new KeyValuePair<string,string>("code",          authCode),
                new KeyValuePair<string,string>("redirect_uri",  s.EbayRuName),
            });

            var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"eBay token exchange failed ({resp.StatusCode}): {body}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            s.EbayAccessToken  = root.GetProperty("access_token").GetString();
            s.EbayRefreshToken = root.TryGetProperty("refresh_token", out var rte) ? rte.GetString() : null;

            if (root.TryGetProperty("expires_in", out var expiresIn))
                s.EbayTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn.GetInt32());

            _settings.Save(s);
        }

        public async Task<EbayPublishResult> PublishListingAsync(Card card)
        {
            try
            {
                await EnsureValidTokenAsync();

                var s = _settings.Load();
                var title       = _titleService.GenerateTitle(card, s.EbayTitleTemplate);
                var description = BuildDescription(card);
                var sku         = card.Sku ?? $"FK-{card.Id}";

                // 1. Upsert inventory item
                var itemReq = EbayListingMapper.BuildInventoryItemRequest(card, title, description);
                var itemJson = JsonSerializer.Serialize(itemReq, BuildInventoryItemSerializerOptions());
                await PutJsonAsync($"{InventoryBase}/inventory_item/{Uri.EscapeDataString(sku)}", itemJson, s);

                // 2. Check for existing offer
                string? offerId = await FindExistingOfferIdAsync(sku, s);

                // 3. Create or update offer
                var offerReq = EbayListingMapper.BuildOfferRequest(card, description, s);
                var offerJson = JsonSerializer.Serialize(offerReq, ApiJsonOptions);

                if (offerId == null)
                {
                    offerId = await CreateOfferAsync(offerJson, s);
                }
                else
                {
                    await PutJsonAsync($"{InventoryBase}/offer/{offerId}", offerJson, s);
                }

                // 4. Publish the offer
                var listingId = await PublishOfferAsync(offerId, s);

                return new EbayPublishResult
                {
                    Success = true,
                    ListingId = listingId,
                    ListingUrl = $"https://www.ebay.com/itm/{listingId}"
                };
            }
            catch (Exception ex)
            {
                return new EbayPublishResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<bool> FetchAndStorePoliciesAsync()
        {
            try
            {
                await EnsureValidTokenAsync();
                var s = _settings.Load();

                s.EbayFulfillmentPolicyId = await FetchFirstPolicyIdAsync("fulfillment_policy", s);
                s.EbayPaymentPolicyId     = await FetchFirstPolicyIdAsync("payment_policy", s);
                s.EbayReturnPolicyId      = await FetchFirstPolicyIdAsync("return_policy", s);

                _settings.Save(s);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // === token management ===

        private async Task EnsureValidTokenAsync()
        {
            var s = _settings.Load();
            if (!string.IsNullOrEmpty(s.EbayAccessToken)
                && s.EbayTokenExpiry.HasValue
                && s.EbayTokenExpiry.Value > DateTime.UtcNow.AddMinutes(5))
                return;

            if (string.IsNullOrEmpty(s.EbayRefreshToken))
                throw new InvalidOperationException("eBay account not connected. Please authorize in Settings.");

            await RefreshAccessTokenAsync(s);
        }

        private async Task RefreshAccessTokenAsync(AppSettings s)
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{s.EbayClientId}:{s.EbayClientSecret}"));

            using var req = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            req.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type",    "refresh_token"),
                new KeyValuePair<string,string>("refresh_token", s.EbayRefreshToken!),
                new KeyValuePair<string,string>("scope",         Scopes),
            });

            var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"eBay token refresh failed ({resp.StatusCode}): {body}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            s.EbayAccessToken = root.GetProperty("access_token").GetString();
            if (root.TryGetProperty("expires_in", out var expiresIn))
                s.EbayTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn.GetInt32());

            _settings.Save(s);
        }

        // === API helpers ===

        private async Task PutJsonAsync(string url, string json, AppSettings s)
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.EbayAccessToken);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"eBay PUT {url} failed ({resp.StatusCode}): {err}");
            }
        }

        private async Task<string?> FindExistingOfferIdAsync(string sku, AppSettings s)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"{InventoryBase}/offer?sku={Uri.EscapeDataString(sku)}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.EbayAccessToken);

            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("offers", out var offers) && offers.GetArrayLength() > 0)
                return offers[0].TryGetProperty("offerId", out var id) ? id.GetString() : null;

            return null;
        }

        private async Task<string> CreateOfferAsync(string offerJson, AppSettings s)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{InventoryBase}/offer");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.EbayAccessToken);
            req.Content = new StringContent(offerJson, Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"eBay create offer failed ({resp.StatusCode}): {body}");

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("offerId").GetString()
                   ?? throw new InvalidOperationException("eBay did not return an offerId.");
        }

        private async Task<string> PublishOfferAsync(string offerId, AppSettings s)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{InventoryBase}/offer/{offerId}/publish");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.EbayAccessToken);
            req.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"eBay publish offer failed ({resp.StatusCode}): {body}");

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("listingId").GetString()
                   ?? throw new InvalidOperationException("eBay did not return a listingId.");
        }

        private async Task<string?> FetchFirstPolicyIdAsync(string policyType, AppSettings s)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"{AccountBase}/{policyType}?marketplace_id=EBAY_US");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.EbayAccessToken);

            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Response key is e.g. "fulfillmentPolicies", "paymentPolicies", "returnPolicies"
            var camelKey = policyType.Replace("_p", "P").Replace("_", string.Empty) + "s";
            if (root.TryGetProperty(camelKey, out var arr) && arr.GetArrayLength() > 0)
            {
                var idKey = policyType.Replace("_policy", "PolicyId")
                                      .Replace("_", string.Empty)
                                      .Replace("fulfillmentpolicyId","fulfillmentPolicyId")
                                      .Replace("paymentpolicyId","paymentPolicyId")
                                      .Replace("returnpolicyId","returnPolicyId");
                // eBay uses fulfillmentPolicyId, paymentPolicyId, returnPolicyId as id keys
                foreach (var candidate in new[] { "fulfillmentPolicyId", "paymentPolicyId", "returnPolicyId" })
                {
                    if (arr[0].TryGetProperty(candidate, out var idProp))
                        return idProp.GetString();
                }
            }

            return null;
        }

        private static string BuildDescription(Card card)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(card.PlayerName)) sb.AppendLine(card.PlayerName);
            if (card.Year.HasValue) sb.AppendLine($"{card.Year} {card.Brand ?? card.Manufacturer}");
            if (!string.IsNullOrEmpty(card.SetName))    sb.AppendLine(card.SetName);
            if (!string.IsNullOrEmpty(card.ParallelName)) sb.AppendLine(card.ParallelName);
            if (!string.IsNullOrEmpty(card.CardNumber)) sb.AppendLine($"Card #{card.CardNumber}");
            if (!string.IsNullOrEmpty(card.SerialNumbered)) sb.AppendLine($"Serial Numbered {card.SerialNumbered}");
            if (card.IsGraded && !string.IsNullOrEmpty(card.GradeCompany))
                sb.AppendLine($"Graded {EbayListingMapper.MapGraderToEbayLabel(card.GradeCompany)} {card.GradeValue}");
            else if (!string.IsNullOrEmpty(card.Condition))
                sb.AppendLine($"Condition: {card.Condition}");
            return sb.ToString().Trim();
        }

        // The eBay Inventory Item API uses PascalCase for "availability" sub-properties
        // and camelCase everywhere else. We handle this with a custom options instance.
        private static JsonSerializerOptions BuildInventoryItemSerializerOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }
    }
}
