using System.Text.Json.Serialization;
using AIDiffReviewService.Domain;

namespace AIDiffReviewService.Dtos
{
    public record ReviewCreated(string JobId, JobStatus Status);
    public record Usage(int InputBytes, int Chunks, bool CacheHit);
    public record JobStatusResponse(
        string JobId,
        JobStatus Status,
        List<Finding> Findings,
        Usage Usage,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Error = null);
}
