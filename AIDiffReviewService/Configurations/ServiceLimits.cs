namespace AIDiffReviewService.Configurations
{
    // Defined once so that it matches the spec
    public static class ServiceLimits
    {
        public const long MaxPayloadBytes = 1_048_576;
        public const int ChunkBytes = 65_536;
        public const int MaxConcurrentJobs = 4;
        public const int RateLimitPerMinute = 30;
    }
}
