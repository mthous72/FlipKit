using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlipKit.Core.Services.ApiModels
{
    public class ScannedCardData
    {
        [JsonPropertyName("player_name")]
        public string? PlayerName { get; set; }

        [JsonPropertyName("card_number")]
        public string? CardNumber { get; set; }

        [JsonPropertyName("year")]
        public int? Year { get; set; }

        [JsonPropertyName("sport")]
        public string? Sport { get; set; }

        [JsonPropertyName("manufacturer")]
        public string? Manufacturer { get; set; }

        [JsonPropertyName("brand")]
        public string? Brand { get; set; }

        [JsonPropertyName("set_name")]
        public string? SetName { get; set; }

        [JsonPropertyName("team")]
        public string? Team { get; set; }

        [JsonPropertyName("variation_type")]
        public string? VariationType { get; set; }

        [JsonPropertyName("parallel_name")]
        public string? ParallelName { get; set; }

        [JsonPropertyName("serial_numbered")]
        public string? SerialNumbered { get; set; }

        [JsonPropertyName("is_rookie")]
        public bool? IsRookie { get; set; }

        [JsonPropertyName("is_auto")]
        public bool? IsAuto { get; set; }

        [JsonPropertyName("is_relic")]
        public bool? IsRelic { get; set; }

        [JsonPropertyName("is_short_print")]
        public bool? IsShortPrint { get; set; }

        [JsonPropertyName("is_graded")]
        public bool? IsGraded { get; set; }

        [JsonPropertyName("grade_company")]
        public string? GradeCompany { get; set; }

        [JsonPropertyName("grade_value")]
        public string? GradeValue { get; set; }

        [JsonPropertyName("auto_grade")]
        public string? AutoGrade { get; set; }

        [JsonPropertyName("cert_number")]
        public string? CertNumber { get; set; }

        [JsonPropertyName("condition_notes")]
        public string? ConditionNotes { get; set; }

        [JsonPropertyName("visual_cues")]
        public ScannedVisualCues? VisualCues { get; set; }

        [JsonPropertyName("all_visible_text")]
        public List<string>? AllVisibleText { get; set; }

        [JsonPropertyName("confidence")]
        public Dictionary<string, string>? Confidence { get; set; }
    }

    public class ScannedVisualCues
    {
        [JsonPropertyName("border_color")]
        public string? BorderColor { get; set; }

        [JsonPropertyName("card_finish")]
        public string? CardFinish { get; set; }

        [JsonPropertyName("has_foil")]
        public bool? HasFoil { get; set; }

        [JsonPropertyName("has_refractor_pattern")]
        public bool? HasRefractorPattern { get; set; }

        [JsonPropertyName("has_serial_number")]
        public bool? HasSerialNumber { get; set; }

        [JsonPropertyName("serial_number_location")]
        public string? SerialNumberLocation { get; set; }

        [JsonPropertyName("background_pattern")]
        public string? BackgroundPattern { get; set; }

        [JsonPropertyName("text_color")]
        public string? TextColor { get; set; }

        [JsonPropertyName("has_rookie_logo")]
        public bool? HasRookieLogo { get; set; }

        [JsonPropertyName("has_auto_sticker")]
        public bool? HasAutoSticker { get; set; }

        [JsonPropertyName("has_relic_swatch")]
        public bool? HasRelicSwatch { get; set; }
    }
}
