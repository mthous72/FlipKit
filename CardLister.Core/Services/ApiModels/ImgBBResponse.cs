using System.Text.Json.Serialization;

namespace FlipKit.Core.Services.ApiModels
{
    public class ImgBBResponse
    {
        [JsonPropertyName("data")]
        public ImgBBData? Data { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }

    public class ImgBBData
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("display_url")]
        public string DisplayUrl { get; set; } = string.Empty;

        [JsonPropertyName("delete_url")]
        public string DeleteUrl { get; set; } = string.Empty;
    }
}
