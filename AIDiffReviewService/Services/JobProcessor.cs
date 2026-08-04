using AIDiffReviewService.Configurations;
using AIDiffReviewService.Domain;

namespace AIDiffReviewService.Services
{
    public class JobProcessor : BackgroundService
    {
        private readonly JobQueue _queue;
        private readonly JobStore _store;

        public JobProcessor(JobQueue queue, JobStore store)
        {
            _queue = queue;
            _store = store;
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
                job.Findings = new();
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
