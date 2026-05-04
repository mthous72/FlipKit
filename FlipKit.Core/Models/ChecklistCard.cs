using System.Text.Json.Serialization;

namespace FlipKit.Core.Models
{
    public class ChecklistCard
    {
        [JsonPropertyName("card_number")]
        public string CardNumber { get; set; } = string.Empty;

        [JsonPropertyName("player_name")]
        public string PlayerName { get; set; } = string.Empty;

        [JsonPropertyName("team")]
        public string? Team { get; set; }

        [JsonPropertyName("is_rookie")]
        public bool IsRookie { get; set; }

        [JsonPropertyName("subset")]
        public string? Subset { get; set; }

        [JsonPropertyName("is_autograph")]
        public bool IsAutograph { get; set; }

        [JsonPropertyName("is_parallel")]
        public bool IsParallel { get; set; }

        [JsonPropertyName("is_insert")]
        public bool IsInsert { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = "seed";
    }
}
