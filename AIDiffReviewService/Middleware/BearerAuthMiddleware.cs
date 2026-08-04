using AIDiffReviewService.Errors;

namespace AIDiffReviewService.Middleware
{
    public class BearerAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _expectedToken;

        public BearerAuthMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            _expectedToken = config["Auth:BearerToken"] ?? throw new InvalidOperationException("Auth:BearerToken is not configured");
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Lets only /v1 pass through
            if (!context.Request.Path.StartsWithSegments("/v1"))
            {
                await _next(context);
                return;
            }

            string authHeader = context.Request.Headers.Authorization.ToString();
            const string prefix = "Bearer ";
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith(prefix, StringComparison.Ordinal))
            {
                await ErrorResults.WriteErrorAsync(context, 401, ErrorCodes.Unauthorized, "Missing or Malformed Authorization Header");
                return;
            }

            string presentedToken = authHeader[prefix.Length..].Trim();

            if (presentedToken != _expectedToken)
            {
                await ErrorResults.WriteErrorAsync(context, 401, ErrorCodes.Unauthorized, "Invalid Bearer Token");
                return;
            }

            await _next(context);
        }
    }
}
