using AIDiffReviewService.Dtos;

namespace AIDiffReviewService.Services
{
    public static class FindingSet
    {
        public static List<Finding> Normalize(IEnumerable<Finding> findings, int maxFindings)
        {
            return findings.DistinctBy(f => f.Id).OrderBy(f => f.Path, StringComparer.Ordinal)
                .ThenBy(f => f.Line).ThenBy(f => f.RuleId, StringComparer.Ordinal).Take(maxFindings).ToList();
        }
    }
}
