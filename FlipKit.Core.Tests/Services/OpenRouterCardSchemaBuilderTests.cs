using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlipKit.Core.Services.ApiModels;
using FlipKit.Core.Services.Implementations;

namespace FlipKit.Core.Tests.Services;

public class OpenRouterCardSchemaBuilderTests
{
    private static JsonElement Serialize(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Fact]
    public void Should_WrapInJsonSchemaEnvelope_When_BuildingResponseFormat()
    {
        var doc = Serialize(OpenRouterCardSchemaBuilder.BuildResponseFormat(new[] { "Refractor" }));

        Assert.Equal("json_schema", doc.GetProperty("type").GetString());
        var jsonSchema = doc.GetProperty("json_schema");
        Assert.Equal("card_extract", jsonSchema.GetProperty("name").GetString());
        Assert.True(jsonSchema.GetProperty("strict").GetBoolean());
        Assert.True(jsonSchema.TryGetProperty("schema", out _));
    }

    [Fact]
    public void Should_AddEnumOnParallelName_When_CandidatesProvided()
    {
        var candidates = new[] { "Refractor", "Wave", "Mojo" };

        var schema = Serialize(OpenRouterCardSchemaBuilder.BuildSchema(candidates));

        var parallel = schema.GetProperty("properties").GetProperty("parallel_name");
        var enumValues = parallel.GetProperty("enum").EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.Null ? null : e.GetString())
            .ToList();
        // Candidates plus an explicit null sentinel — the LLM must pick from
        // the list OR set null (with a description in condition_notes).
        Assert.Contains("Refractor", enumValues);
        Assert.Contains("Wave", enumValues);
        Assert.Contains("Mojo", enumValues);
        Assert.Contains(null, enumValues);
    }

    [Fact]
    public void Should_OmitParallelNameEnum_When_NoCandidates()
    {
        // No candidates -> no constraint on parallel_name (free-form). Still
        // nullable string so models can return null when nothing matches.
        var schema = Serialize(OpenRouterCardSchemaBuilder.BuildSchema(System.Array.Empty<string>()));

        var parallel = schema.GetProperty("properties").GetProperty("parallel_name");
        Assert.False(parallel.TryGetProperty("enum", out _));
    }

    [Fact]
    public void Should_DisallowAdditionalProperties_When_StrictModeEnabled()
    {
        var schema = Serialize(OpenRouterCardSchemaBuilder.BuildSchema(System.Array.Empty<string>()));

        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public void Should_RequireEveryDeclaredProperty_When_BuildingSchema()
    {
        var schema = Serialize(OpenRouterCardSchemaBuilder.BuildSchema(System.Array.Empty<string>()));

        var props = schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToHashSet();
        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToHashSet();

        // Every property is required so the model can't omit a field — it has
        // to fill or send null. Missing fields would fail json_schema validation.
        Assert.Equal(props, required!);
    }

    [Fact]
    public void Should_CoverEveryScannedCardDataField_When_DeclaringProperties()
    {
        // If someone adds a JsonPropertyName to ScannedCardData and forgets to
        // mirror it in the schema, the LLM will return data we silently drop.
        // This reflection sweep catches that drift at compile/test time.
        var dtoFields = typeof(ScannedCardData)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet();

        var schema = Serialize(OpenRouterCardSchemaBuilder.BuildSchema(System.Array.Empty<string>()));
        var schemaProps = schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToHashSet();

        var missing = dtoFields.Except(schemaProps).ToList();
        Assert.Empty(missing);
    }
}
