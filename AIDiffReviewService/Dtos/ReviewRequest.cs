namespace AIDiffReviewService.Dtos
{
    public class ReviewRequest
    {
        public string? Diff { get; set; }
        public ReviewOptions? Options { get; set; }
    }
}
