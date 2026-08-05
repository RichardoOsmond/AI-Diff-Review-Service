using AIDiffReviewService.Configurations;
using AIDiffReviewService.Domain;
using AIDiffReviewService.Dtos;

namespace AIDiffReviewService.Services
{
    public class JobProcessor : BackgroundService
    {
        private readonly JobQueue _queue;
        private readonly JobStore _store;
        private readonly ReviewCache _cache;
        private readonly IReadOnlyDictionary<string, IReviewProvider> _providers;

        public JobProcessor(JobQueue queue, JobStore store, ReviewCache cache, IEnumerable<IReviewProvider> providers)
        {
            _queue = queue;
            _store = store;
            _cache = cache;
            _providers = providers.ToDictionary(p => p.Name);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var workers = new List<Task>();
            for (int i = 0; i < ServiceLimits.MaxConcurrentJobs; i++)
            {
                workers.Add(WorkerLoopAsync(stoppingToken));
            }
            return Task.WhenAll(workers);
        }

        private async Task WorkerLoopAsync(CancellationToken ct)
        {
            await foreach(var jobId in _queue.Reader.ReadAllAsync(ct))
            {
                await ProcessAsync(jobId, ct);
            }
        }

        private async Task ProcessAsync(string jobId, CancellationToken ct)
        {
            if (!_store.TryGet(jobId, out var job) || job is null) { return; }
            try
            {
                job.Status = JobStatus.Running;
                SseEmitter.EmitStatus(job);

                if (!_providers.TryGetValue(job.Provider, out var provider))
                {
                    throw new InvalidOperationException($"Unknown provider '{job.Provider}'.");
                }

                var chunks = DiffChunker.Split(job.Diff);
                var raw = new List<Finding>();
                
                foreach (var chunk in chunks)
                {
                    raw.AddRange(await provider.ReviewAsync(chunk, ct));
                }

                job.Findings = FindingSet.Normalize(raw, job.MaxFindings);
                job.Chunks = chunks.Count;
                job.CacheHit = false;

                foreach (var finding in job.Findings)
                {
                    SseEmitter.EmitFinding(job, finding);
                }

                job.Status = JobStatus.Done;
                SseEmitter.EmitDone(job);
                job.EventsComplete = true;

                if (!string.IsNullOrEmpty(job.CacheKey))
                {
                    _cache.StoreResult(job.CacheKey,
                        new ReviewCache.CachedResult(job.Findings, job.InputBytes, job.Chunks));
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Service is shutting down; leave the job as-is rather than marking it failed.
            }
            catch (Exception ex)
            {
                job.Status = JobStatus.Failed;
                job.Error = ex.Message;
                SseEmitter.EmitStatus(job);
                job.EventsComplete = true;
            }
        }
    }
}
