using System.Text.Json;
using System.Text.Json.Serialization;
using AIDiffReviewService.Domain;
using AIDiffReviewService.Dtos;

namespace AIDiffReviewService.Services
{
    // Appends SSE events to a job's event log. Data is serialized once, here, so replaying
    // the stored events later produces byte-identical output to the live stream.
    public static class SseEmitter
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        public static void Emit(Job job, string ev, object data)
        {
            var json = JsonSerializer.Serialize(data, Json);
            lock (job.Events)
            {
                job.Events.Add(new SseEvent(ev, json));
            }
        }

        public static void EmitStatus(Job job) => Emit(job, "status", new { status = job.Status });

        public static void EmitFinding(Job job, Finding finding) => Emit(job, "finding", finding);

        public static void EmitDone(Job job) =>
            Emit(job, "done", new { total = job.Findings.Count, usage = new Usage(job.InputBytes, job.Chunks, job.CacheHit) });

        // Builds the full event log for a job that is already complete (e.g. a cache hit),
        // so its stream can be replayed just like a normally-processed job.
        public static void PopulateCompleted(Job job)
        {
            EmitStatus(job);
            foreach (var f in job.Findings) EmitFinding(job, f);
            EmitDone(job);
            job.EventsComplete = true;
        }
    }
}
