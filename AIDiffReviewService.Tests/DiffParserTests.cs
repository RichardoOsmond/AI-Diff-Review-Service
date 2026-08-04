using AIDiffReviewService.Services;

namespace AIDiffReviewService.Tests
{
    public class DiffParserTests
    {
        [Fact]
        public void Parses_added_line_with_correct_path_and_line_number()
        {
            var diff = 
                "--- a/src/db.ts\n" + 
                "+++ b/src/db.ts\n" + 
                "@@ -40,2 +40,3 @@\n" + 
                " const conn = pool.get();\n" + 
                "+console.log('x');\n" + 
                " return conn.query(q);\n";

            var added = DiffParser.Parse(diff);

            Assert.Single(added);
            Assert.Equal("src/db.ts", added[0].Path);
            Assert.Equal(41, added[0].Line);
            Assert.Equal("console.log('x');", added[0].Text);
        }

        [Fact]
        public void Removed_lines_must_not_shift_line_numbers()
        {
            var diff = 
                "--- a/app.js\n" + 
                "+++ b/app.js\n" + 
                "@@ -10,3 +10,3 @@\n" + 
                " context line\n" + 
                "-removed line\n" + 
                "+added line\n" + 
                " trailing context\n";

            var added = DiffParser.Parse(diff);

            Assert.Equal(11, added[0].Line);
        }

        [Fact]
        public void HeaderPath_never_returned_as_content()
        {
            var diff = 
                "--- a/readme.md\n" + 
                "+++ b/readme.md\n" + 
                "@@ -1,0 +1,1 @@\n" + 
                "+real content\n";

            var added = DiffParser.Parse(diff);
            Assert.Equal("real content", added[0].Text);
        }
    }
}
