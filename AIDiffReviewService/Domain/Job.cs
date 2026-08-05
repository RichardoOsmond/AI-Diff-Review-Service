using AIDiffReviewService.Dtos;

namespace AIDiffReviewService.Domain
{
    public class Job
    {
        public required string Id {  get; set; }
        public JobStatus Status { get; set; } = JobStatus.Queued;
        public required string Diff { get; init; }
        public required string Provider { get; init; }
        public int MaxFindings { get; init; } = 100;
        public List<Finding> Findings { get; set; } = new();
        public int InputBytes { get; init; }
        public int Chunks { get; set; }
        public bool CacheHit { get; set; }
        public string? CacheKey { get; set; }
        public string? Error { get; set; }

        // SSE event log — appended as the job processes, replayed by the stream endpoint.
        public List<SseEvent> Events { get; } = new();
        public bool EventsComplete { get; set; }
    }
}
