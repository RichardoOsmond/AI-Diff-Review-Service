using AIDiffReviewService.Dtos;

namespace AIDiffReviewService.Services
{
    public interface IReviewProvider
    {
        string Name { get; }
        Task<IReadOnlyList<Finding>> ReviewAsync(string chunk, CancellationToken ct);
    }
}
