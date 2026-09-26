// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompat;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    [TestClass]
    public class RegexStringTransformerTests
    {
        [TestMethod]
        public void Transform_CaptureGroupPatternDoesNotMatchInput_ReturnsInput()
        {
            const string CaptureGroupPattern = "(abc)def";
            const string ReplacementPattern = "$1";
            const string Input = "ghi";

            string output = new RegexStringTransformer(CaptureGroupPattern, ReplacementPattern).Transform(Input);

            Assert.AreEqual(Input, output);
        }

        [TestMethod]
        public void Transform_ReplacementPatternWithoutCaptureGroups_ReturnsReplacementPattern()
        {
            const string CaptureGroupPattern = "(abc)d*";
            const string ReplacementPattern = "xyz";
            const string Input = "abc";

            string output = new RegexStringTransformer(CaptureGroupPattern, ReplacementPattern).Transform(Input);

            Assert.AreEqual(ReplacementPattern, output);
        }

        [TestMethod]
        public void Transform_ReplacementPatternWithTooManyReplacementMarkers_ReturnOutputWithoutTransformedReplacementMarkers()
        {
            const string CaptureGroupPattern = "(abc)(def)ghi";
            const string ReplacementPattern = "1:$1, 2:$2, 3:$3";
            const string Input = "abcdefghi";

            string output = new RegexStringTransformer(CaptureGroupPattern, ReplacementPattern).Transform(Input);

            Assert.AreEqual("1:abc, 2:def, 3:$3", output);
        }

        [TestMethod]
        public void Transform_SameNumberOfGroupsAndMarkers_ReturnsExpected()
        {
            const string CaptureGroupPattern = @".+\\(.+)\\(.+)";
            const string ReplacementPattern = "lib/$1/$2";
            const string Input = @"C:\git\runtime\artifacts\bin\System.Linq\Debug\net7.0-android\System.Linq.dll";

            string output = new RegexStringTransformer(CaptureGroupPattern, ReplacementPattern).Transform(Input);

            Assert.AreEqual("lib/net7.0-android/System.Linq.dll", output);
        }

        [TestMethod]
        public void Transform_MultiplePatterns_ReturnsExpected()
        {
            var patterns = new (string, string)[]
            {
                (@".+\\(.+)\\(.+)", "lib/$1/$2"),
                (@"(.+)/(net\d.\d)-(.+)/(.+)", "runtimes/$3/$1/$2/$4"),
                ("runtimes/windows/", "runtimes/win/")
            };

            const string Input = @"C:\git\runtime\artifacts\bin\System.Linq\Debug\net7.0-android\System.Linq.dll";

            string output = new RegexStringTransformer(patterns).Transform(Input);

            Assert.AreEqual("runtimes/android/lib/net7.0/System.Linq.dll", output);
        }
    }
}
