using Microsoft.AspNetCore.Mvc;
using AIDiffReviewService.Services;
using AIDiffReviewService.Configurations;

namespace AIDiffReviewService.Controllers
{
    [ApiController]
    public class MetaController : ControllerBase
    {
        private readonly UptimeProvider _uptimeProvider;

        public MetaController(UptimeProvider uptimeProvider) { _uptimeProvider = uptimeProvider; }

        [HttpGet("health")]
        public IActionResult GetHealth()
        {
            return Ok(new {status = "ok", version = "1.0.0", uptimeSeconds = (long) _uptimeProvider.Seconds});
        }

        [HttpGet("spec")]
        public IActionResult GetSpec()
        {
            return Ok(new
            {
                specVersion = "1.0",
                providers = new[] {"mock", "llm"},
                limits = new {
                    maxPayloadBytes = ServiceLimits.MaxPayloadBytes,
                    chunkBytes = ServiceLimits.ChunkBytes,
                    maxConcurrentJobs = ServiceLimits.MaxConcurrentJobs,
                    rateLimitPerMinute = ServiceLimits.RateLimitPerMinute
                }
            });
        }
    }
}
