using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FlipKit.Core.Services.ApiModels;

namespace FlipKit.Core.Services
{
    public class ImgBBUploadService : IImageUploadService
    {
        private const string UploadUrl = "https://api.imgbb.com/1/upload";

        private readonly HttpClient _httpClient;
        private readonly ISettingsService _settingsService;

        public ImgBBUploadService(HttpClient httpClient, ISettingsService settingsService)
        {
            _httpClient = httpClient;
            _settingsService = settingsService;
        }

        public async Task<string> UploadImageAsync(string imagePath, string? name = null)
        {
            var settings = _settingsService.Load();
            var apiKey = settings.ImgBBApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("ImgBB API key is not configured. Go to Settings to enter your key.");

            if (!File.Exists(imagePath))
                throw new FileNotFoundException("Image file not found.", imagePath);

            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            var base64 = Convert.ToBase64String(imageBytes);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["key"] = apiKey,
                ["image"] = base64,
                ["name"] = name ?? Path.GetFileNameWithoutExtension(imagePath)
            });

            var response = await _httpClient.PostAsync(UploadUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"ImgBB upload failed ({response.StatusCode}): {responseBody}");

            var result = JsonSerializer.Deserialize<ImgBBResponse>(responseBody);

            if (result?.Success != true || result.Data == null)
                throw new InvalidOperationException("ImgBB upload returned unsuccessful response.");

            return result.Data.Url;
        }

        public async Task<(string? url1, string? url2)> UploadCardImagesAsync(string frontPath, string? backPath = null)
        {
            string? url1 = null;
            string? url2 = null;

            if (!string.IsNullOrEmpty(frontPath) && File.Exists(frontPath))
                url1 = await UploadImageAsync(frontPath);

            if (!string.IsNullOrEmpty(backPath) && File.Exists(backPath))
                url2 = await UploadImageAsync(backPath);

            return (url1, url2);
        }
    }
}
