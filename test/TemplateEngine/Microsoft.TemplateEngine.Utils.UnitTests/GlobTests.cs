// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    public class GlobTests
    {
        [Fact(DisplayName = nameof(VerifyLeadGlobPathSpanning))]
        public void VerifyLeadGlobPathSpanning()
        {
            Glob g = Glob.Parse("**/file");
            Assert.True(g.IsMatch("file"));
            Assert.True(g.IsMatch("a/file"));
            Assert.True(g.IsMatch("a/b/file"));
            Assert.True(g.IsMatch("a/b/c/file"));
            Assert.False(g.IsMatch("other"));
            Assert.False(g.IsMatch("a/other"));
            Assert.False(g.IsMatch("a/b/other"));
            Assert.False(g.IsMatch("a/b/c/other"));
            Assert.False(g.IsMatch("file/stuff"));
            Assert.False(g.IsMatch("file.txt"));
            Assert.False(g.IsMatch("thefile"));
        }

        [Fact(DisplayName = nameof(VerifyGlobExactPathSpanning))]
        public void VerifyGlobExactPathSpanning()
        {
            Glob g = Glob.Parse("a/**/b");
            Assert.True(g.IsMatch("a/b"));
            Assert.True(g.IsMatch("a/x/b"));
            Assert.True(g.IsMatch("a/x/y/b"));
            Assert.False(g.IsMatch("z/a/x/y/b"));
            Assert.False(g.IsMatch("z/a/b"));
        }

        [Fact(DisplayName = nameof(VerifyGlobPathSpanning))]
        public void VerifyGlobPathSpanning()
        {
            Glob g = Glob.Parse("a/**");
            Assert.True(g.IsMatch("a/b"));
            Assert.True(g.IsMatch("a/x/b"));
            Assert.True(g.IsMatch("a/x/y/b"));
            Assert.False(g.IsMatch("z/a/x/y/b"));
            Assert.False(g.IsMatch("z/a/b"));
        }

        [Fact(DisplayName = nameof(VerifyGlobCharacterGroups))]
        public void VerifyGlobCharacterGroups()
        {
            Glob g = Glob.Parse("f[Oo]o");
            Assert.True(g.IsMatch("foo"));
            Assert.True(g.IsMatch("fOo"));
            Assert.True(g.IsMatch("a/foo"));
            Assert.True(g.IsMatch("z/a/x/y/fOo"));
            Assert.False(g.IsMatch("z/a/x/y/fOO"));
        }

        [Fact(DisplayName = nameof(VerifyGlobWildcard))]
        public void VerifyGlobWildcard()
        {
            Glob g = Glob.Parse("f*o");
            Assert.True(g.IsMatch("foo"));
            Assert.True(g.IsMatch("foooooooo"));
            Assert.False(g.IsMatch("foot"));
        }

        [Fact(DisplayName = nameof(VerifyGlobNegate))]
        public void VerifyGlobNegate()
        {
            Glob g = Glob.Parse("!f*o");
            Assert.False(g.IsMatch("foo"));
            Assert.False(g.IsMatch("foooooooo"));
            Assert.True(g.IsMatch("foot"));
        }

        [Fact(DisplayName = nameof(VerifyGlobEscape))]
        public void VerifyGlobEscape()
        {
            Glob g = Glob.Parse(@"\[[\[\ \]]");
            Assert.True(g.IsMatch("[ "));
            Assert.True(g.IsMatch("[]"));
            Assert.True(g.IsMatch("[["));
            Assert.False(g.IsMatch("]"));
        }

        [Fact(DisplayName = nameof(VerifyGlobKitchenSink))]
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
