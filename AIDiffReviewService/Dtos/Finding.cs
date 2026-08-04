using AIDiffReviewService.Domain;
using System.Xml.Schema;

namespace AIDiffReviewService.Dtos
{
    public record Finding(
        string Id,
        string RuleId,
        string Path,
        int Line,
        Severity Severity,
        Category Category,
        string Title,
        string Evidence
        );
}
