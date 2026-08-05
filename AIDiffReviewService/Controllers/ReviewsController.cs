using AIDiffReviewService.Configurations;
using AIDiffReviewService.Domain;
using AIDiffReviewService.Dtos;
using AIDiffReviewService.Errors;
using AIDiffReviewService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
        private readonly ReviewCache _cache;

        public ReviewsController(JobStore store, JobQueue queue, ReviewCache cache)
        {
            _store = store;
            _queue = queue;
            _cache = cache;
        }

        [HttpPost]
        [EnableRateLimiting("reviews")]
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

            var bodyHash = ReviewCache.Hash(body);
            var cacheKey = ReviewCache.Hash($"{req.Diff}\0{provider}\0{maxFindings}");

            // Idempotency-Key: same key + same body -> same job; same key + different body -> 409.
            var idemKey = Request.Headers["Idempotency-Key"].ToString();
            if (!string.IsNullOrEmpty(idemKey) && _cache.TryGetIdempotency(idemKey, out var existing) && existing is not null)
            {
                if (existing.BodyHash != bodyHash)
                {
                    return Error(409, ErrorCodes.IdempotencyConflict,
                        "Idempotency-Key already used with a different request body.");
                }

                var priorStatus = _store.TryGet(existing.JobId, out var prior) && prior is not null
                    ? prior.Status : JobStatus.Queued;
                return Accepted(new ReviewCreated(existing.JobId, priorStatus));
            }

            // Cache: byte-identical {diff, options} -> reuse the finished result, no rework.
            if (_cache.TryGetResult(cacheKey, out var cached) && cached is not null)
            {
                var cachedJob = new Job
                {
                    Id = JobStore.CreateId(),
                    Diff = req.Diff,
                    Provider = provider,
                    MaxFindings = maxFindings,
                    InputBytes = cached.InputBytes,
                    CacheKey = cacheKey,
                    Findings = cached.Findings,
                    Chunks = cached.Chunks,
                    CacheHit = true,
                    Status = JobStatus.Done
                };

                _store.AddJob(cachedJob);
                SseEmitter.PopulateCompleted(cachedJob);
                if (!string.IsNullOrEmpty(idemKey))
                {
                    _cache.StoreIdempotency(idemKey, bodyHash, cachedJob.Id);
                }
                return Accepted(new ReviewCreated(cachedJob.Id, cachedJob.Status));
            }

            var job = new Job
            {
                Id = JobStore.CreateId(),
                Diff = req.Diff,
                Provider = provider,
                MaxFindings = maxFindings,
                InputBytes = Encoding.UTF8.GetByteCount(req.Diff),
                CacheKey = cacheKey
            };

            _store.AddJob(job);
            if (!string.IsNullOrEmpty(idemKey))
            {
                _cache.StoreIdempotency(idemKey, bodyHash, job.Id);
            }
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
                new Usage(job.InputBytes, job.Chunks, job.CacheHit),
                job.Error));
        }

        [HttpGet("{jobId}/stream")]
        public async Task StreamJob(string jobId, CancellationToken ct)
        {
            if (!_store.TryGet(jobId, out var job) || job is null)
            {
                Response.StatusCode = 404;
                Response.ContentType = "application/json";
                await Response.WriteAsync(
                    JsonSerializer.Serialize(new ErrorEnvelope(new ErrorBody(ErrorCodes.NotFound, "No job with that id.")), JsonOpts), ct);
                return;
            }

            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";

            int sent = 0;
            while (!ct.IsCancellationRequested)
            {
                List<Domain.SseEvent> pending;
                lock (job.Events)
                {
                    pending = job.Events.Skip(sent).ToList();
                }

                foreach (var e in pending)
                {
                    await Response.WriteAsync($"event: {e.Event}\ndata: {e.Data}\n\n", ct);
                    sent++;
                }
                await Response.Body.FlushAsync(ct);

                if (job.EventsComplete)
                {
                    lock (job.Events)
                    {
                        pending = job.Events.Skip(sent).ToList();
                    }
                    foreach (var e in pending)
                    {
                        await Response.WriteAsync($"event: {e.Event}\ndata: {e.Data}\n\n", ct);
                        sent++;
                    }
                    await Response.Body.FlushAsync(ct);
                    break;
                }

                await Task.Delay(100, ct);
            }
        }

        // Error Helper
        private IActionResult Error(int status, string code, string message) =>
            StatusCode(status, new ErrorEnvelope(new ErrorBody(code, message)));

        // Temporary
        private static bool LooksLikeUnifiedDiff(string diff) =>
            diff.Contains("@@") || diff.Contains("diff --git") || (diff.Contains("--- ") && diff.Contains("+++ "));
    }
}
