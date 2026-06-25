// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    [TestClass]
    public class GlobTests
    {
        [TestMethod]
        public void VerifyLeadGlobPathSpanning()
        {
            Glob g = Glob.Parse("**/file");
            Assert.IsTrue(g.IsMatch("file"));
            Assert.IsTrue(g.IsMatch("a/file"));
            Assert.IsTrue(g.IsMatch("a/b/file"));
            Assert.IsTrue(g.IsMatch("a/b/c/file"));
            Assert.IsFalse(g.IsMatch("other"));
            Assert.IsFalse(g.IsMatch("a/other"));
            Assert.IsFalse(g.IsMatch("a/b/other"));
            Assert.IsFalse(g.IsMatch("a/b/c/other"));
            Assert.IsFalse(g.IsMatch("file/stuff"));
            Assert.IsFalse(g.IsMatch("file.txt"));
            Assert.IsFalse(g.IsMatch("thefile"));
        }

        [TestMethod]
        public void VerifyGlobExactPathSpanning()
        {
            Glob g = Glob.Parse("a/**/b");
            Assert.IsTrue(g.IsMatch("a/b"));
            Assert.IsTrue(g.IsMatch("a/x/b"));
            Assert.IsTrue(g.IsMatch("a/x/y/b"));
            Assert.IsFalse(g.IsMatch("z/a/x/y/b"));
            Assert.IsFalse(g.IsMatch("z/a/b"));
        }

        [TestMethod]
        public void VerifyGlobPathSpanning()
        {
            Glob g = Glob.Parse("a/**");
            Assert.IsTrue(g.IsMatch("a/b"));
            Assert.IsTrue(g.IsMatch("a/x/b"));
            Assert.IsTrue(g.IsMatch("a/x/y/b"));
            Assert.IsFalse(g.IsMatch("z/a/x/y/b"));
            Assert.IsFalse(g.IsMatch("z/a/b"));
        }

        [TestMethod]
        public void VerifyGlobCharacterGroups()
        {
            Glob g = Glob.Parse("f[Oo]o");
            Assert.IsTrue(g.IsMatch("foo"));
            Assert.IsTrue(g.IsMatch("fOo"));
            Assert.IsTrue(g.IsMatch("a/foo"));
            Assert.IsTrue(g.IsMatch("z/a/x/y/fOo"));
            Assert.IsFalse(g.IsMatch("z/a/x/y/fOO"));
        }

        [TestMethod]
        public void VerifyGlobWildcard()
        {
            Glob g = Glob.Parse("f*o");
            Assert.IsTrue(g.IsMatch("foo"));
            Assert.IsTrue(g.IsMatch("foooooooo"));
            Assert.IsFalse(g.IsMatch("foot"));
        }

        [TestMethod]
        public void VerifyGlobNegate()
        {
            Glob g = Glob.Parse("!f*o");
            Assert.IsFalse(g.IsMatch("foo"));
            Assert.IsFalse(g.IsMatch("foooooooo"));
            Assert.IsTrue(g.IsMatch("foot"));
        }

        [TestMethod]
        public void VerifyGlobEscape()
        {
            Glob g = Glob.Parse(@"\[[\[\ \]]");
            Assert.IsTrue(g.IsMatch("[ "));
            Assert.IsTrue(g.IsMatch("[]"));
            Assert.IsTrue(g.IsMatch("[["));
            Assert.IsFalse(g.IsMatch("]"));
        }

        [TestMethod]
        public void VerifyGlobKitchenSink()
        {
            Glob g = Glob.Parse("**/[Dd]ocuments/**/*.htm*");
            Assert.IsTrue(g.IsMatch("Documents/git.html"));
            Assert.IsTrue(g.IsMatch("Documents/ppc/ppc.html"));
            Assert.IsTrue(g.IsMatch("tools/perf/Documents/perf.html"));

            g = Glob.Parse("**/[Dd]ocuments/**/*p*.htm*");
            Assert.IsFalse(g.IsMatch("Documents/git.html"));
            Assert.IsTrue(g.IsMatch("Documents/ppc/ppc.html"));
            Assert.IsTrue(g.IsMatch("tools/perf/Documents/perf.html"));

            g = Glob.Parse("[Dd]ocuments/**/*.htm*");
            Assert.IsTrue(g.IsMatch("Documents/git.html"));
            Assert.IsTrue(g.IsMatch("Documents/ppc/ppc.html"));
            Assert.IsFalse(g.IsMatch("tools/perf/Documents/perf.html"));

            g = Glob.Parse("[Dd]ocuments/**/*.h*");
            Assert.IsTrue(g.IsMatch("Documents/git.html"));
            Assert.IsTrue(g.IsMatch("Documents/ppc/ppc.html"));
            Assert.IsFalse(g.IsMatch("tools/perf/Documents/perf.html"));

            g = Glob.Parse("[Dd]ocuments/**/*.html");
            Assert.IsTrue(g.IsMatch("Documents/git.html"));
            Assert.IsTrue(g.IsMatch("Documents/ppc/ppc.html"));
            Assert.IsFalse(g.IsMatch("tools/perf/Documents/perf.html"));

            g = Glob.Parse("[Dd]ocuments/*.html");
            Assert.IsTrue(g.IsMatch("Documents/git.html"));
            Assert.IsFalse(g.IsMatch("Documents/ppc/ppc.html"));
            Assert.IsFalse(g.IsMatch("tools/perf/Documents/perf.html"));
        }
    }
}
