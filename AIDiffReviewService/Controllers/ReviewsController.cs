using AIDiffReviewService.Configurations;
using AIDiffReviewService.Domain;
using AIDiffReviewService.Dtos;
using AIDiffReviewService.Errors;
using AIDiffReviewService.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace AIDiffReviewService.Controllers
{
    [ApiController]
    [Route("v1/reviews")]
    public class ReviewsController : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
        private readonly JobStore _store;
        private readonly JobQueue _queue;

        public ReviewsController(JobStore store, JobQueue queue)
        {
            _store = store;
            _queue = queue;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CancellationToken ct)
        {
            if (Request.ContentLength is long len && len > ServiceLimits.MaxPayloadBytes)
            {
                return Error(413, ErrorCodes.PayloadTooLarge, "Payload exceeds the 1 MiB limit.");
            }

            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync(ct);
            }

            if (Encoding.UTF8.GetByteCount(body) > ServiceLimits.MaxPayloadBytes)
            {
                return Error(413, ErrorCodes.PayloadTooLarge, "Payload exceeds the 1 MiB limit.");
            }

            ReviewRequest? req;
            try
            {
                req = JsonSerializer.Deserialize<ReviewRequest>(body, JsonOpts);
            } catch (JsonException)
            {
                return Error(400, ErrorCodes.InvalidJson, "Request body is not a valid JSON.");
            }

            if (req is null || string.IsNullOrWhiteSpace(req.Diff) || !LooksLikeUnifiedDiff(req.Diff))
            {
                return Error(422, ErrorCodes.InvalidDiff, "Diff is missing, empty, or not a unified diff.");
            }

            var provider = req.Options?.Provider ?? "mock";
            var maxFindings = req.Options?.MaxFindings ?? 100;

            var job = new Job
            {
                Id = JobStore.CreateId(),
                Diff = req.Diff,
                Provider = provider,
                MaxFindings = maxFindings,
                InputBytes = Encoding.UTF8.GetByteCount(req.Diff)
            };

            _store.AddJob(job);
            _queue.Enqueue(job.Id);
            return Accepted(new ReviewCreated(job.Id, job.Status));
        }

        [HttpGet("{jobId}")]
        public IActionResult GetJob(string jobId)
        {
            if (!_store.TryGet(jobId, out var job) || job is null)
            {
                return Error(404, ErrorCodes.NotFound, "No job with that id.");
            }

            return Ok(new JobStatusResponse(
                job.Id,
                job.Status,
                job.Findings,
                new Usage(job.InputBytes, job.Chunks, job.CacheHit)));
        }

        // Error Helper
        private IActionResult Error(int status, string code, string message) =>
            StatusCode(status, new ErrorEnvelope(new ErrorBody(code, message)));

        // Temporary
        private static bool LooksLikeUnifiedDiff(string diff) =>
            diff.Contains("@@") || diff.Contains("diff --git") || (diff.Contains("--- ") && diff.Contains("+++ "));
    }
}
