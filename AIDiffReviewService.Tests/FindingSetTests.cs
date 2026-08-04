using AIDiffReviewService.Domain;
using AIDiffReviewService.Services;
using AIDiffReviewService.Dtos;

namespace AIDiffReviewService.Tests
{
    public class FindingSetTests
    {
        private static Finding F(string path, int line, string ruleId) => new($"{ruleId}:{path}:{line}", 
            ruleId, path, line, Severity.Low, Category.Style, "title", "evidence");

        [Fact]
        public void Two_same_ids_return_one()
        {
            var findings = new[] { F("a.js", 1, "MOCK-001"), F("a.js", 1, "MOCK-001") };
            var result = FindingSet.Normalize(findings, 100);
            Assert.Single(result);
        }

        [Fact]
        public void Two_different_ids_return_two()
        {
            var findings = new[] { F("a.js", 1, "MOCK-001"), F("a.js", 1, "MOCK-001"), F("a.js", 2, "MOCK-001") };
            var result = FindingSet.Normalize(findings, 100);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public void Findings_ordered_by_path_line_then_ruleid()
        {
            // ordered by Path -> Line -> RuleId
            var findings = new[] { F("a.js", 2, "MOCK-001"), F("a.js", 1, "MOCK-001"), F("a.js", 3, "MOCK-001"), // This Line measures line
            F("a.js", 1, "MOCK-001"), F("a.js", 1, "MOCK-002"), F("a.js", 1, "MOCK-003"), // This line measures RuleId
            F("A.js", 1, "MOCK-002"), F("a.js", 2, "MOCK-001"), F("Z.js", 1, "MOCK-001")}; // This line measures Path

            var result = FindingSet.Normalize(findings, 100);
            Assert.Equal(new[]
            {
                "MOCK-002:A.js:1", 
                "MOCK-001:Z.js:1", 
                "MOCK-001:a.js:1", 
                "MOCK-002:a.js:1", 
                "MOCK-003:a.js:1", 
                "MOCK-001:a.js:2", 
                "MOCK-001:a.js:3"
            }, result.Select(f => f.Id));
        }

        [Fact]
        public void Returned_findings_must_be_equal_to_max_findings()
        {
            var findings = new[] { F("a.js", 2, "MOCK-001"), F("a.js", 1, "MOCK-001"), F("a.js", 3, "MOCK-001"), 
            F("a.js", 1, "MOCK-001"), F("a.js", 1, "MOCK-002"), F("a.js", 1, "MOCK-003"), 
            F("A.js", 1, "MOCK-001"), F("a.js", 1, "MOCK-001"), F("Z.js", 1, "MOCK-001")};

            var result = FindingSet.Normalize(findings, 5);
            Assert.Equal(5, result.Count());
        }
    }
}
