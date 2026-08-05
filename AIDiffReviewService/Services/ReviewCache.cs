using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using AIDiffReviewService.Dtos;

namespace AIDiffReviewService.Services
{
    // Two responsibilities:
    //  - Idempotency: an Idempotency-Key maps to (body hash, jobId). Same key + same body -> same job;
    //    same key + different body -> caller gets a 409.
    //  - Result cache: a canonical {diff, options} hash maps to a finished result so identical
    //    submissions don't redo the work and report cacheHit = true.
    public class ReviewCache
    {
        public record IdempotencyEntry(string BodyHash, string JobId);
        public record CachedResult(List<Finding> Findings, int InputBytes, int Chunks);

        private readonly ConcurrentDictionary<string, IdempotencyEntry> _idempotency = new();
        private readonly ConcurrentDictionary<string, CachedResult> _results = new();

        public bool TryGetIdempotency(string key, out IdempotencyEntry? entry) =>
            _idempotency.TryGetValue(key, out entry);

        public void StoreIdempotency(string key, string bodyHash, string jobId) =>
            _idempotency[key] = new IdempotencyEntry(bodyHash, jobId);

        public bool TryGetResult(string cacheKey, out CachedResult? result) =>
            _results.TryGetValue(cacheKey, out result);

        public void StoreResult(string cacheKey, CachedResult result) =>
            _results[cacheKey] = result;

        public static string Hash(string input) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }
}
