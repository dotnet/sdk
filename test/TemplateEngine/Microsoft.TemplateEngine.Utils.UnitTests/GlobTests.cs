using Xunit;

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    public class GlobTests
    {
        [Fact]
        public void VerifyGlobExactPathSpanning()
        {
            Glob g = Glob.Parse("a/**/b");
            Assert.True(g.IsMatch("a/b"));
            Assert.True(g.IsMatch("a/x/b"));
            Assert.True(g.IsMatch("a/x/y/b"));
            Assert.False(g.IsMatch("z/a/x/y/b"));
            Assert.False(g.IsMatch("z/a/b"));
        }

        [Fact]
        public void VerifyGlobPathSpanning()
        {
            Glob g = Glob.Parse("a/**");
            Assert.True(g.IsMatch("a/b"));
            Assert.True(g.IsMatch("a/x/b"));
            Assert.True(g.IsMatch("a/x/y/b"));
            Assert.False(g.IsMatch("z/a/x/y/b"));
            Assert.False(g.IsMatch("z/a/b"));
        }

        [Fact]
        public void VerifyGlobCharacterGroups()
        {
            Glob g = Glob.Parse("f[Oo]o");
            Assert.True(g.IsMatch("foo"));
            Assert.True(g.IsMatch("fOo"));
            Assert.True(g.IsMatch("a/foo"));
            Assert.True(g.IsMatch("z/a/x/y/fOo"));
            Assert.False(g.IsMatch("z/a/x/y/fOO"));
        }

        [Fact]
        public void VerifyGlobWildcard()
        {
            Glob g = Glob.Parse("f*o");
            Assert.True(g.IsMatch("foo"));
            Assert.True(g.IsMatch("foooooooo"));
            Assert.False(g.IsMatch("foot"));
        }

        [Fact]
        public void VerifyGlobKitchenSink()
        {
            Glob g = Glob.Parse("**/[Dd]ocuments/**/*.htm*");
            Assert.True(g.IsMatch("Documents/git.html"));
            Assert.True(g.IsMatch("Documents/ppc/ppc.html"));
            Assert.True(g.IsMatch("tools/perf/Documents/perf.html"));

            g = Glob.Parse("**/[Dd]ocuments/**/*p*.htm*");
            Assert.False(g.IsMatch("Documents/git.html"));
            Assert.True(g.IsMatch("Documents/ppc/ppc.html"));
            Assert.True(g.IsMatch("tools/perf/Documents/perf.html"));

            g = Glob.Parse("[Dd]ocuments/**/*.htm*");
            Assert.True(g.IsMatch("Documents/git.html"));
            Assert.True(g.IsMatch("Documents/ppc/ppc.html"));
            Assert.False(g.IsMatch("tools/perf/Documents/perf.html"));

            g = Glob.Parse("[Dd]ocuments/**/*.h*");
            Assert.True(g.IsMatch("Documents/git.html"));
            Assert.True(g.IsMatch("Documents/ppc/ppc.html"));
            Assert.False(g.IsMatch("tools/perf/Documents/perf.html"));

            g = Glob.Parse("[Dd]ocuments/**/*.html");
            Assert.True(g.IsMatch("Documents/git.html"));
            Assert.True(g.IsMatch("Documents/ppc/ppc.html"));
            Assert.False(g.IsMatch("tools/perf/Documents/perf.html"));

            g = Glob.Parse("[Dd]ocuments/*.html");
            Assert.True(g.IsMatch("Documents/git.html"));
            Assert.False(g.IsMatch("Documents/ppc/ppc.html"));
            Assert.False(g.IsMatch("tools/perf/Documents/perf.html"));
        }
    }
}
