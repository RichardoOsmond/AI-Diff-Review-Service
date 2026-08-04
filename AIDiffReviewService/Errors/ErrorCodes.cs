namespace AIDiffReviewService.Errors
{
    // Defined once to avoid typos
    public static class ErrorCodes
    {
        public const string Unauthorized = "unauthorized";
        public const string PayloadTooLarge = "payload_too_large";
        public const string InvalidJson = "invalid_json";
        public const string InvalidDiff = "invalid_diff";
        public const string IdempotencyConflict = "idempotency_conflict";
        public const string NotFound = "not_found";
        public const string RateLimited = "rate_limited";
        public const string Internal = "internal";
    }
}