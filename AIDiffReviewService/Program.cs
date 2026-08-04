using AIDiffReviewService.Middleware;
using AIDiffReviewService.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

var startTime = DateTimeOffset.UtcNow;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

builder.Services.AddSingleton<JobStore>();

builder.Services.AddSingleton<JobQueue>();

builder.Services.AddSingleton<IReviewProvider, MockProvider>();

builder.Services.AddSingleton(new UptimeProvider(startTime));

builder.Services.AddHostedService<JobProcessor>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseMiddleware<BearerAuthMiddleware>();

app.MapControllers();

app.Run();
