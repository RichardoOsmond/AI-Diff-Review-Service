using System.Text;
using AIDiffReviewService.Configurations;
using AIDiffReviewService.Services;

namespace AIDiffReviewService.Tests
{
    public class DiffChunkerTests
    {
        private static string FileSection(string name, string added) =>
            $"diff --git a/{name} b/{name}\n--- a/{name}\n+++ b/{name}\n@@ -1,0 +1,1 @@\n+{added}\n";

        private static string BigMultiFileDiff(int files)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < files; i++)
            {
                sb.Append(FileSection($"f{i}.js", i % 5 == 0 ? "eval(x);" : "const y = 1;"));
            }
            return sb.ToString();
        }

        [Fact]
        public void Chunked_scan_is_identical_to_unchunked()
        {
            var big = BigMultiFileDiff(1000);
            var mock = new MockProvider();
            var unchunked = FindingSet.Normalize(mock.Scan(big), 100000);
            var chunks = DiffChunker.Split(big);
            var merged = chunks.SelectMany(c => mock.Scan(c));
            var chunked = FindingSet.Normalize(merged, 100000);

            Assert.True(chunks.Count > 1);
            Assert.Equal(unchunked.Select(f => f.Id), chunked.Select(f => f.Id));
        }

        [Fact]
        public void Every_chunk_within_limit_when_files_are_small()
        {
            var chunks = DiffChunker.Split(BigMultiFileDiff(1000));
            Assert.All(chunks, c => Assert.True(Encoding.UTF8.GetByteCount(c) <= ServiceLimits.ChunkBytes));
        }

        [Fact]
        public void Single_oversized_file_is_its_own_chunk()
        {
            var huge = FileSection("big.js", new string('x', 70000));
            var chunks = DiffChunker.Split(huge);
            Assert.Single(chunks);
        }
    }
}
