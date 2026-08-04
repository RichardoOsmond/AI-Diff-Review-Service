namespace AIDiffReviewService.Dtos
{
    public record ErrorEnvelope(ErrorBody Error);
    public record ErrorBody(string Code, string Message);
}
