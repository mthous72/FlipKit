using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Services;

namespace FlipKit.Web.Services
{
    /// <summary>
    /// JSON-based settings service for web application.
    /// Reads/writes settings to %LOCALAPPDATA%\FlipKit\config.json
    /// (shared with desktop app).
    /// NOTE: This is duplicated from Desktop - should be moved to Core in future refactor.
    /// </summary>
    public class JsonSettingsService : ISettingsService
    {
        private static readonly string ConfigFolder;
        private static readonly string ConfigPath;

        static JsonSettingsService()
        {
            // Support Docker: check for FLIPKIT_SETTINGS_PATH environment variable
            var envPath = Environment.GetEnvironmentVariable("FLIPKIT_SETTINGS_PATH");
            if (!string.IsNullOrEmpty(envPath))
            {
                ConfigPath = envPath;
                ConfigFolder = Path.GetDirectoryName(envPath) ?? "/data";
            }
            else
            {
                ConfigFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FlipKit");
                ConfigPath = Path.Combine(ConfigFolder, "config.json");
            }
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly HttpClient _httpClient;
        private readonly ISecretEncryption _encryption;

        public JsonSettingsService(HttpClient httpClient, ISecretEncryption encryption)
        {
            _httpClient = httpClient;
            _encryption = encryption;
        }

        public AppSettings Load()
        {
            if (!File.Exists(ConfigPath))
                return new AppSettings();

            var json = File.ReadAllText(ConfigPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            DecryptSecrets(settings);
            return settings;
        }

        public void Save(AppSettings settings)
        {
            Directory.CreateDirectory(ConfigFolder);
            var encrypted = EncryptedCopy(settings);
            var json = JsonSerializer.Serialize(encrypted, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }

        private AppSettings EncryptedCopy(AppSettings src)
        {
            var json = JsonSerializer.Serialize(src, JsonOptions);
            var copy = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)!;
            copy.OpenRouterApiKey  = _encryption.Protect(src.OpenRouterApiKey);
            copy.ImgBBApiKey       = _encryption.Protect(src.ImgBBApiKey);
            copy.XimilarApiKey     = _encryption.Protect(src.XimilarApiKey);
            copy.EbayClientId      = _encryption.Protect(src.EbayClientId);
            copy.EbayClientSecret  = _encryption.Protect(src.EbayClientSecret);
            copy.EbayAccessToken   = _encryption.Protect(src.EbayAccessToken);
            copy.EbayRefreshToken  = _encryption.Protect(src.EbayRefreshToken);
            copy.EbayRuName        = _encryption.Protect(src.EbayRuName);
            return copy;
        }

        private void DecryptSecrets(AppSettings s)
        {
            s.OpenRouterApiKey = _encryption.Unprotect(s.OpenRouterApiKey);
            s.ImgBBApiKey      = _encryption.Unprotect(s.ImgBBApiKey);
            s.XimilarApiKey    = _encryption.Unprotect(s.XimilarApiKey);
            s.EbayClientId     = _encryption.Unprotect(s.EbayClientId);
            s.EbayClientSecret = _encryption.Unprotect(s.EbayClientSecret);
            s.EbayAccessToken  = _encryption.Unprotect(s.EbayAccessToken);
            s.EbayRefreshToken = _encryption.Unprotect(s.EbayRefreshToken);
            s.EbayRuName       = _encryption.Unprotect(s.EbayRuName);
        }

        public bool HasValidConfig()
        {
            var settings = Load();
            return !string.IsNullOrWhiteSpace(settings.OpenRouterApiKey);
        }

        public async Task<bool> TestOpenRouterConnectionAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return false;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/models");
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> TestImgBBConnectionAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return false;

            try
            {
                // ImgBB doesn't have a dedicated test endpoint, so we check for a valid key
                // by making a minimal request. A 400 with "No image" means the key is valid.
                var response = await _httpClient.PostAsync(
                    $"https://api.imgbb.com/1/upload?key={apiKey}",
                    new StringContent(string.Empty));

                // 400 = key valid but no image provided; 403 = invalid key
                return response.StatusCode != System.Net.HttpStatusCode.Forbidden;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> TestXimilarConnectionAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return false;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ximilar.com/account/v2/details/");
                request.Headers.Add("Authorization", $"Token {apiKey}");

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> TestEbayConnectionAsync(string clientId, string clientSecret)
        {
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return false;

            try
            {
                var credentials = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ebay.com/identity/v1/oauth2/token");
                request.Headers.Add("Authorization", $"Basic {credentials}");
                request.Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("scope", "https://api.ebay.com/oauth/api_scope")
                });

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
