using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlipKit.Core.Services.ApiModels
{
    // Maps the relevant subset of CardSight's IdentifyCardResponse. The full schema
    // includes catalog UUIDs (segmentId, releaseId, etc.) and grading slab data —
    // we only deserialize what we map onto Card / ScanResult.

    public class CardsightIdentifyResponse
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("requestId")] public string? RequestId { get; set; }
        [JsonPropertyName("detections")] public List<CardsightDetection>? Detections { get; set; }
        [JsonPropertyName("processingTime")] public double? ProcessingTime { get; set; }
        [JsonPropertyName("messages")] public List<CardsightMessage>? Messages { get; set; }
    }

    public class CardsightDetection
    {
        // "High" | "Medium" | "Low"
        [JsonPropertyName("confidence")] public string? Confidence { get; set; }
        [JsonPropertyName("card")] public CardsightCardDetails? Card { get; set; }
        [JsonPropertyName("grading")] public CardsightGradingDetail? Grading { get; set; }
    }

    public class CardsightCardDetails
    {
        // Present only for exact card matches; set-level matches leave it null.
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("year")] public string? Year { get; set; }
        [JsonPropertyName("manufacturer")] public string? Manufacturer { get; set; }
        [JsonPropertyName("releaseName")] public string? ReleaseName { get; set; }
        [JsonPropertyName("setName")] public string? SetName { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("number")] public string? Number { get; set; }
        [JsonPropertyName("numberedTo")] public int? NumberedTo { get; set; }
        [JsonPropertyName("attributes")] public List<string>? Attributes { get; set; }
        [JsonPropertyName("parallel")] public CardsightParallelSummary? Parallel { get; set; }
    }

    public class CardsightParallelSummary
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("numberedTo")] public int? NumberedTo { get; set; }
    }

    public class CardsightGradingDetail
    {
        [JsonPropertyName("confidence")] public string? Confidence { get; set; }
        [JsonPropertyName("company")] public CardsightSlabCompany? Company { get; set; }
        [JsonPropertyName("grade")] public CardsightSlabGrade? Grade { get; set; }
        [JsonPropertyName("autoGrade")] public CardsightSlabGrade? AutoGrade { get; set; }
    }

    public class CardsightSlabCompany
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    public class CardsightSlabGrade
    {
        [JsonPropertyName("value")] public string? Value { get; set; }
        [JsonPropertyName("condition")] public string? Condition { get; set; }
    }

    public class CardsightMessage
    {
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }

    public class CardsightSubscriptionInfo
    {
        [JsonPropertyName("calls")] public int Calls { get; set; }
        [JsonPropertyName("api_keys")] public List<CardsightApiKeyUsage>? ApiKeys { get; set; }
    }

    public class CardsightApiKeyUsage
    {
        [JsonPropertyName("key")] public string? Key { get; set; }
        [JsonPropertyName("calls")] public int Calls { get; set; }
    }

    public class CardsightErrorResponse
    {
        [JsonPropertyName("error")] public string? Error { get; set; }
        [JsonPropertyName("code")] public string? Code { get; set; }
    }
}
