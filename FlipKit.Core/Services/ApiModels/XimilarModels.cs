using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlipKit.Core.Services.ApiModels
{
    // Request models
    public class XimilarRequest
    {
        [JsonPropertyName("records")]
        public List<XimilarRecord> Records { get; set; } = new();

        /// <summary>
        /// When true, uses extra tokens to identify newer cards and short prints
        /// that may not be in the standard database.
        /// </summary>
        [JsonPropertyName("magic_ai")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool MagicAi { get; set; }
    }

    public class XimilarRecord
    {
        [JsonPropertyName("_base64")]
        public string? Base64 { get; set; }

        [JsonPropertyName("_url")]
        public string? Url { get; set; }
    }

    // Response models
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

        [JsonPropertyName("Category")]
        public string? Category { get; set; }
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

        [JsonPropertyName("_tags")]
        public XimilarTags? Tags { get; set; }

        [JsonPropertyName("_tags_simple")]
        public List<string>? TagsSimple { get; set; }

        [JsonPropertyName("_identification")]
        public XimilarIdentification? Identification { get; set; }
    }

    public class XimilarTags
    {
        [JsonPropertyName("Category")]
        public List<XimilarTagValue>? Category { get; set; }

        [JsonPropertyName("Side")]
        public List<XimilarTagValue>? Side { get; set; }

        [JsonPropertyName("Subcategory")]
        public List<XimilarTagValue>? Subcategory { get; set; }

        [JsonPropertyName("Autograph")]
        public List<XimilarTagValue>? Autograph { get; set; }

        [JsonPropertyName("Foil/Holo")]
        public List<XimilarTagValue>? FoilHolo { get; set; }

        [JsonPropertyName("Graded")]
        public List<XimilarTagValue>? Graded { get; set; }
    }

    public class XimilarTagValue
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("prob")]
        public double Probability { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    public class XimilarIdentification
    {
        [JsonPropertyName("best_match")]
        public XimilarBestMatch? BestMatch { get; set; }

        [JsonPropertyName("alternatives")]
        public List<XimilarBestMatch>? Alternatives { get; set; }

        [JsonPropertyName("distances")]
        public List<double>? Distances { get; set; }
    }

    public class XimilarBestMatch
    {
        [JsonPropertyName("year")]
        public string? Year { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }  // Player name

        [JsonPropertyName("set_name")]
        public string? SetName { get; set; }

        [JsonPropertyName("card_type")]
        public string? CardType { get; set; }  // e.g., "Rookie Card"

        [JsonPropertyName("card_number")]
        public string? CardNumber { get; set; }

        [JsonPropertyName("subcategory")]
        public string? Subcategory { get; set; }  // Sport (MMA, Football, etc.)

        [JsonPropertyName("sub_set")]
        public string? SubSet { get; set; }  // e.g., "UFC"

        [JsonPropertyName("company")]
        public string? Company { get; set; }  // Manufacturer (Panini, Topps)

        [JsonPropertyName("full_name")]
        public string? FullName { get; set; }

        [JsonPropertyName("links")]
        public XimilarLinks? Links { get; set; }
    }

    public class XimilarLinks
    {
        [JsonPropertyName("ebay.com")]
        public string? Ebay { get; set; }

        [JsonPropertyName("comc.com")]
        public string? Comc { get; set; }

        [JsonPropertyName("beckett.com")]
        public string? Beckett { get; set; }
    }
}
