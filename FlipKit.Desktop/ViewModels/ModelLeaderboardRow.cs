using System;
using FlipKit.Core.Services;

namespace FlipKit.Desktop.ViewModels
{
    // Row in the Settings → Model Performance Leaderboard table. Pure display
    // shape — built from a ModelQuality snapshot when the panel loads. New row
    // is constructed each refresh so display strings stay in sync with the
    // latest scoreboard state.
    public sealed record ModelLeaderboardRow(
        string ModelId,
        string ScoreDisplay,
        int SampleCount,
        string SuccessRateDisplay,
        string CompletenessDisplay,
        string LastUsedDisplay)
    {
        public static ModelLeaderboardRow FromQuality(ModelQuality q)
        {
            var score = q.Score.HasValue
                ? $"{Math.Round(q.Score.Value)}%"
                : q.ConfidenceLabel; // "Untested" / "Tentative (n)"

            var successRate = q.SampleCount > 0
                ? $"{Math.Round(100.0 * q.SuccessCount / q.SampleCount)}%"
                : "—";

            var completeness = q.AverageCompleteness.HasValue
                ? $"{Math.Round(100m * q.AverageCompleteness.Value)}%"
                : "—";

            var lastUsed = q.LastUsedAt.HasValue
                ? FormatRelativeTime(q.LastUsedAt.Value)
                : "—";

            return new ModelLeaderboardRow(
                ModelId: q.ModelId,
                ScoreDisplay: score,
                SampleCount: q.SampleCount,
                SuccessRateDisplay: successRate,
                CompletenessDisplay: completeness,
                LastUsedDisplay: lastUsed);
        }

        private static string FormatRelativeTime(DateTime utc)
        {
            var delta = DateTime.UtcNow - utc;
            if (delta.TotalSeconds < 60) return "just now";
            if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
            if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
            if (delta.TotalDays < 30) return $"{(int)delta.TotalDays}d ago";
            return utc.ToLocalTime().ToString("yyyy-MM-dd");
        }
    }
}
