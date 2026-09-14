// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    [TestClass]
    public class VersionStringTests
    {
        [TestMethod]
        [DataRow("1.0.0.0", true)]
        [DataRow("1.0.0", true)]
        [DataRow("1.0", true)]
        [DataRow("1", false)]
        [DataRow("1.0.0.0.", false)]
        [DataRow("1.0.0.", false)]
        [DataRow("1.0.", false)]
        [DataRow("1.", false)]
        [DataRow("MyVersion", false)]
        [DataRow("1.0.0.A", false)]
        [DataRow("A.0.0.0", false)]
        [DataRow("", false)]
        [DataRow(null, false)]
        public void VerifyVersionIsWellFormedCheckerTest(string? versionString, bool expectedParseResult)
        {
            Assert.AreEqual(expectedParseResult, VersionStringHelpers.IsVersionWellFormed(versionString));
        }

        [TestMethod]
        [DataRow("", "", null)]
        [DataRow(null, null, null)]
        [DataRow("1.0.0.0", "1.0.0.0", 0)]
        [DataRow("1.0.0.0", null, null)]
        [DataRow(null, "1.0.0.0", null)]
        [DataRow("1.0.0.0", "1.1.0.0", -1)]
        [DataRow("1.1.0.0", "1.0.0.0", 1)]
        [DataRow("1.0.0", "1.1", -1)]
        [DataRow("1.1", "1.0.0", 1)]
        public void VerifyVersionComparisonTest(string? version1, string? version2, int? expectedComparison)
        {
            Assert.AreEqual(expectedComparison, VersionStringHelpers.CompareVersions(version1, version2));
        }

        [TestMethod]
        [DataRow("1.0.0.0", "1.0.0.0", true)]
        [DataRow("1.0.0.0", "1.0.0", true)]
        [DataRow("1.0.0.0", "1.0", true)]
        [DataRow("1.0.0.0", "1.1", false)]
        [DataRow("2.0.0.0", "1.1", false)]
        [DataRow("[1.0.0.0-*)", "1.0.0.0", true)]
        [DataRow("[1.0.0.0-*)", "1.1.0.0", true)]
        [DataRow("[1.0.0.0-*)", "1.0.1.0", true)]
        [DataRow("[1.0.0.0-*)", "1.0.0.1", true)]
        [DataRow("(1.0.0.0-*)", "1.0.0.0", false)]
        [DataRow("(*-2.0.0.0)", "1.0.0.0", true)]
        [DataRow("(*-2.0.0.0)", "1.5.0.0", true)]
        [DataRow("(*-2.0.0.0)", "2.0.0.0", false)]
        [DataRow("(*-2.0.0.0]", "2.0.0.0", true)]
        [DataRow("[1.1.0.0-1.2.0.0]", "1.0.0.0", false)]
        [DataRow("[1.1.0.0-1.2.0.0]", "1.1.0.0", true)]
        [DataRow("[1.1.0.0-1.2.0.0]", "1.2.0.0", true)]
        [DataRow("[1.1.0.0-1.2.0.0]", "1.2.0.1", false)]
        [DataRow("(1.1.0.0-1.2.0.0)", "1.1.0.0", false)]
        [DataRow("(1.1.0.0-1.2.0.0)", "1.2.0.0", false)]
        [DataRow("(1.1.0.0-1.2.0.0)", "1.1.1.0", true)]
        [DataRow("(1.1.0.0-1.2.0.0)", "1.1.0.1", true)]
        public void VersionParseCompare(string allowed, string proposed, bool expected)
        {
            Assert.IsTrue(VersionStringHelpers.TryParseVersionSpecification(allowed, out IVersionSpecification? checker));
            Assert.IsNotNull(checker);
            Assert.AreEqual(expected, checker.CheckIfVersionIsValid(proposed));
        }
    }
}
