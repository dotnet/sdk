// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Xunit;

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    public class VersionStringTests
    {
        [Theory(DisplayName = nameof(VerifyVersionIsWellFormedCheckerTest))]
        [InlineData("1.0.0.0", true)]
        [InlineData("1.0.0", true)]
        [InlineData("1.0", true)]
        [InlineData("1", false)]
        [InlineData("1.0.0.0.", false)]
        [InlineData("1.0.0.", false)]
        [InlineData("1.0.", false)]
        [InlineData("1.", false)]
        [InlineData("MyVersion", false)]
        [InlineData("1.0.0.A", false)]
        [InlineData("A.0.0.0", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void VerifyVersionIsWellFormedCheckerTest(string versionString, bool expectedParseResult)
        {
            Assert.Equal(expectedParseResult, VersionStringHelpers.IsVersionWellFormed(versionString));
        }

        [Theory(DisplayName = nameof(VerifyVersionComparisonTest))]
        [InlineData("", "", null)]
        [InlineData(null, null, null)]
        [InlineData("1.0.0.0", "1.0.0.0", 0)]
        [InlineData("1.0.0.0", null, null)]
        [InlineData(null, "1.0.0.0", null)]
        [InlineData("1.0.0.0", "1.1.0.0", -1)]
        [InlineData("1.1.0.0", "1.0.0.0", 1)]
        [InlineData("1.0.0", "1.1", -1)]
        [InlineData("1.1", "1.0.0", 1)]
        public void VerifyVersionComparisonTest(string version1, string version2, int? expectedComparison)
        {
            Assert.Equal(expectedComparison, VersionStringHelpers.CompareVersions(version1, version2));
        }

        [Theory(DisplayName = nameof(VersionParseCompare))]
        [InlineData("1.0.0.0", "1.0.0.0", true)]
        [InlineData("1.0.0.0", "1.0.0", true)]
        [InlineData("1.0.0.0", "1.0", true)]
        [InlineData("1.0.0.0", "1.1", false)]
        [InlineData("2.0.0.0", "1.1", false)]
        [InlineData("[1.0.0.0-*)", "1.0.0.0", true)]
        [InlineData("[1.0.0.0-*)", "1.1.0.0", true)]
        [InlineData("[1.0.0.0-*)", "1.0.1.0", true)]
        [InlineData("[1.0.0.0-*)", "1.0.0.1", true)]
        [InlineData("(1.0.0.0-*)", "1.0.0.0", false)]
        [InlineData("(*-2.0.0.0)", "1.0.0.0", true)]
        [InlineData("(*-2.0.0.0)", "1.5.0.0", true)]
        [InlineData("(*-2.0.0.0)", "2.0.0.0", false)]
        [InlineData("(*-2.0.0.0]", "2.0.0.0", true)]
        [InlineData("[1.1.0.0-1.2.0.0]", "1.0.0.0", false)]
        [InlineData("[1.1.0.0-1.2.0.0]", "1.1.0.0", true)]
        [InlineData("[1.1.0.0-1.2.0.0]", "1.2.0.0", true)]
        [InlineData("[1.1.0.0-1.2.0.0]", "1.2.0.1", false)]
        [InlineData("(1.1.0.0-1.2.0.0)", "1.1.0.0", false)]
        [InlineData("(1.1.0.0-1.2.0.0)", "1.2.0.0", false)]
        [InlineData("(1.1.0.0-1.2.0.0)", "1.1.1.0", true)]
        [InlineData("(1.1.0.0-1.2.0.0)", "1.1.0.1", true)]
        public void VersionParseCompare(string allowed, string proposed, bool expected)
        {
            Assert.True(VersionStringHelpers.TryParseVersionSpecification(allowed, out IVersionSpecification checker));
            Assert.Equal(expected, checker.CheckIfVersionIsValid(proposed));
        }
    }
}
