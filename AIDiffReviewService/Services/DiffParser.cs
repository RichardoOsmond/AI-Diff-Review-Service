using System.Text.RegularExpressions;
using AIDiffReviewService.Domain;

namespace AIDiffReviewService.Services
{
    public static class DiffParser
    {
        private static readonly Regex HunkHeader = new(@"^@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@", RegexOptions.Compiled);

        public static List<AddedLine> Parse(string diff)
        {
            var result = new List<AddedLine>();
            string? currentPath = null;
            int newline = 0;

            var lines = diff.Replace("\r\n", "\n").Split('\n');

            foreach (var line in lines)
            {
                if (line.StartsWith("+++ "))
                {
                    currentPath = NormalizePath(line.Substring(4));
                }
                else if (line.StartsWith("--- "))
                {
                }
                else if (line.StartsWith("@@"))
                {
                    var m = HunkHeader.Match(line);
                    if (m.Success)
                    {
                        newline = int.Parse(m.Groups[1].Value);
                    }
                }
                else if (line.StartsWith("+"))
                {
                    if (currentPath is not null)
                    {
                        result.Add(new AddedLine(currentPath, newline, line.Substring(1)));
                    }
                    newline++;
                }
                else if (line.StartsWith("-"))
                {
                }
                else if (line.StartsWith(" "))
                {
                    newline++;
                }
            }
            return result;
        }

        private static string NormalizePath(string headerPath)
        {
            var path = headerPath.Trim();
            int tab = path.IndexOf('\t');
            if (tab >= 0)
            {
                path = path.Substring(0, tab);
            }

            if (path.StartsWith("a/") || path.StartsWith("b/"))
            {
                path = path.Substring(2);
            }
            return path;
        }
    }
}
