using System;
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
        private static readonly string ConfigFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlipKit");

        private static readonly string ConfigPath = Path.Combine(ConfigFolder, "config.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly HttpClient _httpClient;

        public JsonSettingsService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public AppSettings Load()
        {
            if (!File.Exists(ConfigPath))
                return new AppSettings();

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }

        public void Save(AppSettings settings)
        {
            Directory.CreateDirectory(ConfigFolder);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(ConfigPath, json);
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
    }
}
