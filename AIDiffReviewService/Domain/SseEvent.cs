namespace AIDiffReviewService.Domain
{
    // One recorded Server-Sent Event: the event name and its pre-serialized JSON data.
    // Storing these on the job is what lets a late subscriber replay the stream identically.
    public record SseEvent(string Event, string Data);
}
