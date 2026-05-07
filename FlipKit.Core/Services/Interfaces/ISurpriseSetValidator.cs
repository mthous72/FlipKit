using System.Collections.Generic;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services
{
    public enum IssueSeverity { Warning, Error }

    public record SurpriseSetIssue(
        string Code,
        string Message,
        IssueSeverity Severity,
        int? CardId = null,
        string? Field = null);

    public interface ISurpriseSetValidator
    {
        /// <summary>
        /// Validates the set and its cards against all compliance rules.
        /// Returns an empty list when there are no issues.
        /// </summary>
        IList<SurpriseSetIssue> Validate(SurpriseSet set, IList<Card> cards);
    }
}
