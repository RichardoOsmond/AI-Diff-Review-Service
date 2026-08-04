namespace AIDiffReviewService.Services
{
    public class UptimeProvider
    {
        private readonly DateTimeOffset _startTime;
        public double Seconds => (DateTimeOffset.UtcNow - _startTime).TotalSeconds;

        public UptimeProvider(DateTimeOffset startTime) { _startTime = startTime; }
    }
}
