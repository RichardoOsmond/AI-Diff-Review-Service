using AIDiffReviewService.Configurations;
using AIDiffReviewService.Dtos;
using AIDiffReviewService.Errors;
using AIDiffReviewService.Middleware;
using AIDiffReviewService.Services;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var startTime = DateTimeOffset.UtcNow;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

builder.Services.AddSingleton<JobStore>();

builder.Services.AddSingleton<JobQueue>();

builder.Services.AddSingleton<ReviewCache>();

builder.Services.AddSingleton<IReviewProvider, MockProvider>();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IReviewProvider, LlmProvider>();

builder.Services.AddSingleton(new UptimeProvider(startTime));

builder.Services.AddHostedService<JobProcessor>();

builder.Services.AddRateLimiter(options =>
{
    // Fixed window: RateLimitPerMinute (30) submissions per minute, POST /v1/reviews only.
    options.AddFixedWindowLimiter("reviews", o =>
    {
        o.PermitLimit = ServiceLimits.RateLimitPerMinute;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });

    // Reject with 429 + Retry-After + our error envelope (never a 5xx).
    options.OnRejected = async (context, ct) =>
    {
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ra)
            ? ((int)ra.TotalSeconds).ToString()
            : "60";

        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.Headers.RetryAfter = retryAfter;
        context.HttpContext.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(
            new ErrorEnvelope(new ErrorBody(ErrorCodes.RateLimited, "Too many requests. Try again later.")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await context.HttpContext.Response.WriteAsync(json, ct);
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseMiddleware<BearerAuthMiddleware>();

app.UseRateLimiter();

app.MapControllers();

app.Run();
