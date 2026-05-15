using System;

namespace FlipKit.Core.Services
{
    public enum CardsightFailureReason
    {
        NoMatch,
        LowConfidence,
        NotConfigured,
        InvalidKey,
        QuotaExceeded,
        RateLimited,
        BadRequest,
        Transient,
        Unknown
    }

    // Any of these (except user-cancellation) signals "fall through to OpenRouter".
    // CompositeScannerService catches and logs; call sites never see them.
    public sealed class CardsightException : Exception
    {
        public CardsightFailureReason Reason { get; }
        public string? ResponseBody { get; }
        public int? StatusCode { get; }

        public CardsightException(CardsightFailureReason reason, string message, int? statusCode = null, string? responseBody = null, Exception? inner = null)
            : base(message, inner)
        {
            Reason = reason;
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }
}
