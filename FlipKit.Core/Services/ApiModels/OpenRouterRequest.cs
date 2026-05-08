using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlipKit.Core.Services.ApiModels
{
    public class OpenRouterRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OpenRouterMessage> Messages { get; set; } = new();

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; } = 1024;

        // Tells OpenRouter to wait up to this many seconds for the model provider.
        // Without it, OpenRouter uses a short default that causes premature 524s on slow free/paid models.
        [JsonPropertyName("timeout")]
        public int Timeout { get; set; } = 270;

        // Strict structured-output gate. When set, OpenRouter forwards a json_schema
        // constraint to the model provider so the response is grammar-bound to the
        // schema (parallel_name, variation_type, etc. emitted only as enum values).
        // Omitted from the wire when null so callers that don't opt in (legacy
        // scanner paths, ebay title enricher) keep their request shape unchanged.
        [JsonPropertyName("response_format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? ResponseFormat { get; set; }
    }

    public class OpenRouterMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("content")]
        public object Content { get; set; } = string.Empty;
    }

    public class OpenRouterContentPart
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; set; }

        [JsonPropertyName("image_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OpenRouterImageUrl? ImageUrl { get; set; }
    }

    public class OpenRouterImageUrl
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }
}
