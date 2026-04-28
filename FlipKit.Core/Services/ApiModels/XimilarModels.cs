using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlipKit.Core.Services.ApiModels
{
    public class XimilarRequest
    {
        [JsonPropertyName("records")]
        public List<XimilarRecord> Records { get; set; } = new();
    }

    public class XimilarRecord
    {
        [JsonPropertyName("_base64")]
        public string? Base64 { get; set; }

        [JsonPropertyName("_url")]
        public string? Url { get; set; }
    }

    public class XimilarResponse
    {
        [JsonPropertyName("records")]
        public List<XimilarResponseRecord>? Records { get; set; }

        [JsonPropertyName("status")]
        public XimilarStatus? Status { get; set; }
    }

    public class XimilarStatus
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    public class XimilarResponseRecord
    {
        [JsonPropertyName("_status")]
        public XimilarRecordStatus? Status { get; set; }

        [JsonPropertyName("_objects")]
        public List<XimilarObject>? Objects { get; set; }

        [JsonPropertyName("best")]
        public XimilarBestMatch? Best { get; set; }
    }

    public class XimilarRecordStatus
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    public class XimilarObject
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("prob")]
        public double Probability { get; set; }

        [JsonPropertyName("bound_box")]
        public List<int>? BoundBox { get; set; }

        [JsonPropertyName("labels")]
        public List<XimilarLabel>? Labels { get; set; }
    }

    public class XimilarLabel
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("prob")]
        public double Probability { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    public class XimilarBestMatch
    {
        [JsonPropertyName("_id")]
        public string? Id { get; set; }

        [JsonPropertyName("_url")]
        public string? Url { get; set; }

        [JsonPropertyName("_score")]
        public double Score { get; set; }

        [JsonPropertyName("player_name")]
        public string? PlayerName { get; set; }

        [JsonPropertyName("year")]
        public string? Year { get; set; }

        [JsonPropertyName("brand")]
        public string? Brand { get; set; }

        [JsonPropertyName("manufacturer")]
        public string? Manufacturer { get; set; }

        [JsonPropertyName("card_number")]
        public string? CardNumber { get; set; }

        [JsonPropertyName("team")]
        public string? Team { get; set; }

        [JsonPropertyName("sport")]
        public string? Sport { get; set; }

        [JsonPropertyName("parallel")]
        public string? Parallel { get; set; }

        [JsonPropertyName("variation")]
        public string? Variation { get; set; }

        [JsonPropertyName("serial_numbered")]
        public string? SerialNumbered { get; set; }

        [JsonPropertyName("is_rookie")]
        public bool? IsRookie { get; set; }

        [JsonPropertyName("is_auto")]
        public bool? IsAuto { get; set; }

        [JsonPropertyName("is_relic")]
        public bool? IsRelic { get; set; }

        [JsonPropertyName("ebay_url")]
        public string? EbayUrl { get; set; }

        [JsonPropertyName("price")]
        public decimal? Price { get; set; }
    }
}
