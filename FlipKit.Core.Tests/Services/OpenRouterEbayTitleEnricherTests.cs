using FlipKit.Core.Services.Implementations;

namespace FlipKit.Core.Tests.Services;

// Pure parse + prompt-shape tests. The HTTP path is exercised manually
// against the live OpenRouter API; here we just lock the format-handling
// behaviour so model output drift surfaces in CI rather than in production.
public class OpenRouterEbayTitleEnricherTests
{
    [Fact]
    public void BuildPrompt_IncludesAllInputTitles_AndAskJsonOnly()
    {
        var prompt = OpenRouterEbayTitleEnricher.BuildPrompt(new[] { "Mahomes Prizm", "Lamar Donruss" });

        Assert.Contains("1. Mahomes Prizm", prompt);
        Assert.Contains("2. Lamar Donruss", prompt);
        Assert.Contains("JSON array", prompt);
        Assert.Contains("playerName", prompt);
    }

    [Fact]
    public void ParseResponse_ParsesArrayOfObjects()
    {
        var raw = "[{\"playerName\":\"Patrick Mahomes\",\"brand\":\"Prizm\",\"setName\":null,\"parallelName\":\"Silver\",\"team\":\"Chiefs\"}]";
        var parsed = OpenRouterEbayTitleEnricher.ParseResponse(raw, expectedCount: 1);

        var item = Assert.Single(parsed);
        Assert.Equal("Patrick Mahomes", item.PlayerName);
        Assert.Equal("Prizm", item.Brand);
        Assert.Null(item.SetName);
        Assert.Equal("Silver", item.ParallelName);
        Assert.Equal("Chiefs", item.Team);
    }

    [Fact]
    public void ParseResponse_StripsMarkdownCodeFence()
    {
        var raw = "```json\n[{\"playerName\":\"X\",\"brand\":null,\"setName\":null,\"parallelName\":null,\"team\":null}]\n```";
        var parsed = OpenRouterEbayTitleEnricher.ParseResponse(raw, expectedCount: 1);
        Assert.Equal("X", Assert.Single(parsed).PlayerName);
    }

    [Fact]
    public void ParseResponse_TolerantOfLeadingProse()
    {
        var raw = "Sure, here you go:\n[{\"playerName\":\"Y\",\"brand\":null,\"setName\":null,\"parallelName\":null,\"team\":null}]";
        var parsed = OpenRouterEbayTitleEnricher.ParseResponse(raw, expectedCount: 1);
        Assert.Equal("Y", Assert.Single(parsed).PlayerName);
    }

    [Fact]
    public void ParseResponse_WrapsBareObject_IntoSingletonArray()
    {
        var raw = "{\"playerName\":\"Z\",\"brand\":null,\"setName\":null,\"parallelName\":null,\"team\":null}";
        var parsed = OpenRouterEbayTitleEnricher.ParseResponse(raw, expectedCount: 1);
        Assert.Equal("Z", Assert.Single(parsed).PlayerName);
    }

    [Fact]
    public void ParseResponse_PadsShortResponse_WithEmptyEnrichments()
    {
        var raw = "[{\"playerName\":\"A\",\"brand\":null,\"setName\":null,\"parallelName\":null,\"team\":null}]";
        var parsed = OpenRouterEbayTitleEnricher.ParseResponse(raw, expectedCount: 3);

        Assert.Equal(3, parsed.Count);
        Assert.Equal("A", parsed[0].PlayerName);
        Assert.Null(parsed[1].PlayerName);
        Assert.Null(parsed[2].PlayerName);
    }

    [Fact]
    public void ParseResponse_TreatsEmptyStrings_AsNull()
    {
        var raw = "[{\"playerName\":\"\",\"brand\":\"   \",\"setName\":null,\"parallelName\":\"Silver\",\"team\":\"Chiefs\"}]";
        var parsed = OpenRouterEbayTitleEnricher.ParseResponse(raw, expectedCount: 1);

        var item = Assert.Single(parsed);
        Assert.Null(item.PlayerName);
        Assert.Null(item.Brand);
        Assert.Equal("Silver", item.ParallelName);
    }

    [Fact]
    public void ParseResponse_Throws_When_NoJsonInContent()
    {
        Assert.Throws<InvalidOperationException>(() =>
            OpenRouterEbayTitleEnricher.ParseResponse("nothing useful here", 1));
    }
}
