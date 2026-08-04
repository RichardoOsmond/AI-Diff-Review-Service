using AIDiffReviewService.Configurations;
using AIDiffReviewService.Domain;

namespace AIDiffReviewService.Services
{
    public class JobProcessor : BackgroundService
    {
        private readonly JobQueue _queue;
        private readonly JobStore _store;
        private readonly IReadOnlyDictionary<string, IReviewProvider> _providers;

        public JobProcessor(JobQueue queue, JobStore store, IEnumerable<IReviewProvider> providers)
        {
            _queue = queue;
            _store = store;
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

                if (!_providers.TryGetValue(job.Provider, out var provider))
                {
                    throw new InvalidOperationException($"Unknown provider '{job.Provider}'.");
                }

                var raw = await provider.ReviewAsync(job.Diff, ct);
                job.Findings = FindingSet.Normalize(raw, job.MaxFindings);
                job.Chunks = 1;
                job.CacheHit = false;

                job.Status = JobStatus.Done;
            } catch (Exception ex)
            {
                job.Status = JobStatus.Failed;
                job.Error = ex.Message;
            }
        }
    }
}
