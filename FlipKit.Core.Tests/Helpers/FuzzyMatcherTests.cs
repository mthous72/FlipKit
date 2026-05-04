using FlipKit.Core.Helpers;

namespace FlipKit.Core.Tests.Helpers;

public class FuzzyMatcherTests
{
    // Match: Levenshtein-based ratio in [0, 1]. 1.0 = identical (after normalization).

    [Fact]
    public void Should_ReturnOne_When_StringsAreIdentical()
    {
        Assert.Equal(1.0, FuzzyMatcher.Match("Bowman Chrome", "Bowman Chrome"));
    }

    [Fact]
    public void Should_ReturnOne_When_StringsDifferOnlyInCase()
    {
        // Match normalizes (lowercases) before comparing.
        Assert.Equal(1.0, FuzzyMatcher.Match("BOWMAN CHROME", "bowman chrome"));
    }

    [Fact]
    public void Should_ReturnOne_When_StringsDifferOnlyInWhitespace()
    {
        // Multi-space and surrounding whitespace get collapsed/trimmed by Normalize.
        Assert.Equal(1.0, FuzzyMatcher.Match("  Bowman   Chrome  ", "Bowman Chrome"));
    }

    [Fact]
    public void Should_ReturnZero_When_EitherInputIsNullOrWhitespace()
    {
        Assert.Equal(0.0, FuzzyMatcher.Match(null!, "Bowman"));
        Assert.Equal(0.0, FuzzyMatcher.Match("Bowman", null!));
        Assert.Equal(0.0, FuzzyMatcher.Match("", "Bowman"));
        Assert.Equal(0.0, FuzzyMatcher.Match("   ", "Bowman"));
    }

    [Fact]
    public void Should_ReturnLowerScore_When_StringsAreDissimilar()
    {
        // Different short strings should be well below the typical 0.85+ match threshold.
        var score = FuzzyMatcher.Match("Bowman", "Topps");
        Assert.True(score < 0.5, $"Expected dissimilar score, got {score}");
    }

    [Fact]
    public void Should_ReturnHighScore_When_StringsHaveSingleCharTypo()
    {
        // One-char edit distance over a ~13-char string ≈ 0.92 ratio.
        var score = FuzzyMatcher.Match("Bowman Chrome", "Bowman Chrme");
        Assert.True(score > 0.85, $"Expected near-match score, got {score}");
    }

    // Normalize: lowercase + strip non-word/space/slash + collapse whitespace + trim.

    [Fact]
    public void Should_LowercaseAllCharacters_When_Normalizing()
    {
        Assert.Equal("bowman chrome", FuzzyMatcher.Normalize("Bowman Chrome"));
    }

    [Fact]
    public void Should_StripSpecialCharacters_When_Normalizing()
    {
        // Punctuation, currency, brackets all gone; word chars + spaces stay.
        Assert.Equal("bowmans 1", FuzzyMatcher.Normalize("Bowman's #1!"));
    }

    [Fact]
    public void Should_PreserveForwardSlash_When_Normalizing()
    {
        // Forward slash matters for serial numbers like "/199" — explicitly kept.
        Assert.Equal("1/199", FuzzyMatcher.Normalize("1/199"));
    }

    [Fact]
    public void Should_CollapseMultipleSpacesIntoOne_When_Normalizing()
    {
        Assert.Equal("a b c", FuzzyMatcher.Normalize("a    b\t\tc"));
    }

    [Fact]
    public void Should_ReturnEmptyString_When_NormalizingNullOrWhitespace()
    {
        Assert.Equal(string.Empty, FuzzyMatcher.Normalize(null!));
        Assert.Equal(string.Empty, FuzzyMatcher.Normalize("   "));
    }

    // NormalizeCardNumber: strip leading '#' and leading zeros; preserve "0".

    [Fact]
    public void Should_StripLeadingHash_When_NormalizingCardNumber()
    {
        Assert.Equal("42", FuzzyMatcher.NormalizeCardNumber("#42"));
    }

    [Fact]
    public void Should_StripLeadingZeros_When_NormalizingCardNumber()
    {
        Assert.Equal("42", FuzzyMatcher.NormalizeCardNumber("0042"));
    }

    [Fact]
    public void Should_StripBothHashAndLeadingZeros_When_NormalizingCardNumber()
    {
        Assert.Equal("1", FuzzyMatcher.NormalizeCardNumber("#001"));
    }

    [Fact]
    public void Should_PreserveZero_When_AllDigitsAreZero()
    {
        // Stripping all leading zeros from "000" leaves nothing; should return "0", not "".
        Assert.Equal("0", FuzzyMatcher.NormalizeCardNumber("000"));
    }

    [Fact]
    public void Should_ReturnEmptyString_When_CardNumberIsNullOrWhitespace()
    {
        Assert.Equal(string.Empty, FuzzyMatcher.NormalizeCardNumber(null!));
        Assert.Equal(string.Empty, FuzzyMatcher.NormalizeCardNumber("   "));
    }

    // NormalizeParallelName: normalize then look up in alias dict (twice — normalized + original).

    [Fact]
    public void Should_MapKnownAlias_When_NormalizingParallelName()
    {
        // "RR" → "rated rookie" (alias lookup hit on normalized form).
        Assert.Equal("rated rookie", FuzzyMatcher.NormalizeParallelName("RR"));
    }

    [Fact]
    public void Should_MapAliasCaseInsensitively_When_NormalizingParallelName()
    {
        // Alias dict uses OrdinalIgnoreCase, but Normalize lowercases anyway — both paths covered.
        Assert.Equal("super short print", FuzzyMatcher.NormalizeParallelName("SSP"));
    }

    [Fact]
    public void Should_FallBackToOriginalInputLookup_When_NormalizedFormIsNotAKey()
    {
        // "red white & blue" normalizes to "red white blue" (the value, not a key).
        // The fallback `TryGetValue(input.Trim(), ...)` path catches it via the original key.
        Assert.Equal("red white blue", FuzzyMatcher.NormalizeParallelName("red white & blue"));
    }

    [Fact]
    public void Should_ReturnNormalizedInput_When_ParallelNameHasNoAlias()
    {
        // Unknown parallel — no alias hit, so just the normalized form comes back.
        Assert.Equal("rainbow foil", FuzzyMatcher.NormalizeParallelName("Rainbow Foil"));
    }

    [Fact]
    public void Should_ReturnEmptyString_When_ParallelNameIsNullOrWhitespace()
    {
        Assert.Equal(string.Empty, FuzzyMatcher.NormalizeParallelName(null!));
        Assert.Equal(string.Empty, FuzzyMatcher.NormalizeParallelName("   "));
    }
}
