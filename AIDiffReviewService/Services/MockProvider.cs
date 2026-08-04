using System.Text;
using System.Text.RegularExpressions;
using AIDiffReviewService.Domain;
using AIDiffReviewService.Dtos;

namespace AIDiffReviewService.Services
{
    public class MockProvider : IReviewProvider
    {
        public string Name => "mock";
        public Task<IReadOnlyList<Finding>> ReviewAsync(string chunk, CancellationToken ct) => Task.FromResult(Scan(chunk));
        private record Rule(string RuleId, Severity Severity, Category Category, string Title, Func<string, bool> Matches);
        private static readonly Regex CredRegex = new(
            @"(api[_-]?key|secret|token)\s*[:=]\s*['""][A-Za-z0-9_\-]{16,}['""]", 
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SqlInString = new(
            @"['""][^'""]*\b(SELECT|INSERT|UPDATE|DELETE)\b[^'""]*['""]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex LooseNull = new(
            @"(?<![=!])(==|!=)(?!=)\s*null", RegexOptions.Compiled);

        private static readonly Rule[] SimpleRules =
        {
            new("MOCK-001", Severity.Critical, Category.Security, "eval usage", t => t.Contains("eval(")),
            new("MOCK-002", Severity.Critical, Category.Security, "hardcoded credential", t => CredRegex.IsMatch(t)),
            new("MOCK-003", Severity.High, Category.Security, "SQL string concatenation",
                t => t.Contains("+") && SqlInString.IsMatch(t)),
            new("MOCK-005", Severity.Medium, Category.Correctness, "loose null comparison",
                t => LooseNull.IsMatch(t)),
            new("MOCK-006", Severity.Medium, Category.Performance, "deep-clone via JSON",
                t => t.Contains("JSON.parse(JSON.stringify(")),
            new("MOCK-007", Severity.Low, Category.Style, "console.log left in", t => t.Contains("console.log(")),
            new("MOCK-008", Severity.Low, Category.Style, "unresolved marker",
                t => t.Contains("TODO") || t.Contains("FIXME")),
            new("MOCK-INJ", Severity.Critical, Category.Security, "prompt-injection content",
                t =>
                {
                    var l = t.ToLowerInvariant();
                    return l.Contains("ignore previous instructions") || l.Contains("disregard all prior") ||
                        l.Contains("you are now");
                })
        };

        public IReadOnlyList<Finding> Scan(string chunk)
        {
            var added = DiffParser.Parse(chunk);
            var findings = new List<Finding>();

            foreach (var al in added)
            {
                foreach (var rule in SimpleRules)
                {
                    if (rule.Matches(al.Text))
                    {
                        findings.Add(Make(rule.RuleId, rule.Severity, rule.Category, rule.Title, al));
                    }
                }
            }

            findings.AddRange(EmptyCatchFindings(added));
            return findings;
        }

        private static IEnumerable<Finding> EmptyCatchFindings(List<AddedLine> added)
        {
            foreach (var byFile in added.GroupBy(a => a.Path))
            {
                var f = byFile.ToList();
                for (int i =0; i < f.Count; i++)
                {
                    if (!Regex.IsMatch(f[i].Text, @"\bcatch\b")) { continue; }
                    if (IsEmptyCatch(f, i))
                    {
                        yield return Make("MOCK-004", Severity.High, Category.Correctness, "swallowed exception", f[i]);
                    }
                }
            }
        }

        private static bool IsEmptyCatch(List<AddedLine> f, int i)
        {
            var sb = new StringBuilder();
            for (int j = i; j < f.Count && j < i + 6; j++)
            {
                sb.Append(f[j].Text).Append("\n");
            }
            var window = sb.ToString();
            int catchPos = window.IndexOf("catch");
            int open = window.IndexOf("{", catchPos);
            if (open < 0) { return false; }
            int close = window.IndexOf("}", open + 1);
            if (close < 0) { return false; }

            return window.Substring(open + 1, close - open - 1).Trim().Length == 0;
        }

        private static Finding Make(string ruleId, Severity sev, Category cat, string title, AddedLine al) =>
            new($"{ruleId}:{al.Path}:{al.Line}", ruleId, al.Path, al.Line, sev, cat, title, al.Text);
    }
}
