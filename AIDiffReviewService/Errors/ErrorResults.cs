using System.Text.Json;
using AIDiffReviewService.Dtos;

namespace AIDiffReviewService.Errors
{
    public static class ErrorResults
    {
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        public static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(new ErrorEnvelope(new ErrorBody(code, message)), JsonOpts);
            await context.Response.WriteAsync(json);
        }
    }
}