using System.Text;
using AIDiffReviewService.Configurations;

namespace AIDiffReviewService.Services
{
    public static class DiffChunker
    {
        public static List<string> Split(string diff)
        {
            var sections = SplitIntoFileSections(diff);
            var chunks = new List<string>();
            var current = new StringBuilder();

            int currentBytes = 0;

            foreach (var section in sections)
            {
                int sectionBytes = Encoding.UTF8.GetByteCount(section);

                if (current.Length > 0 && currentBytes + sectionBytes > ServiceLimits.ChunkBytes)
                {
                    chunks.Add(current.ToString());
                    current.Clear();
                    currentBytes = 0;
                }

                current.Append(section);
                currentBytes += sectionBytes;
            }

            if (current.Length > 0)
            {
                chunks.Add(current.ToString());
            }

            if (chunks.Count == 0)
            {
                chunks.Add(diff);
            }

            return chunks;
        }

        private static List<string> SplitIntoFileSections(string diff)
        {
            var lines = diff.Split("\n");
            bool hasGitHeaders = lines.Any(l => l.StartsWith("diff --git "));
            Func<string, bool> isBoundary = hasGitHeaders ? l => l.StartsWith("diff --git ") : l => l.StartsWith("--- ");

            var sections = new List<string>();
            var sb = new StringBuilder();

            foreach (var line in lines)
            {
                if (isBoundary(line) && sb.Length > 0)
                {
                    sections.Add(sb.ToString());
                    sb.Clear();
                }
                sb.Append(line).Append("\n");
            }

            if (sb.Length > 0)
            {
                sections.Add(sb.ToString());
            }

            return sections;
        }
    }
}
