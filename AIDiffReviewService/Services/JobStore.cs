using AIDiffReviewService.Domain;
using System.Collections.Concurrent;

namespace AIDiffReviewService.Services
{
    public class JobStore
    {
        private readonly ConcurrentDictionary<string, Job> _jobs = new();

        public void AddJob(Job job)
        {
            _jobs[job.Id] = job;
        }
        public bool TryGet(string id, out Job? job)
        {
            return _jobs.TryGetValue(id, out job);
        }
        public static string CreateId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
