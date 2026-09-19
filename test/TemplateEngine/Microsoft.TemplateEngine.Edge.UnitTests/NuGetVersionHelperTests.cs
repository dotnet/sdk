// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Edge.Installers.NuGet;
using NuGet.Versioning;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    [TestClass]
    public class NuGetVersionHelperTests
    {
        [TestMethod]
        [DataRow(null, true)]
        [DataRow("", true)]
        [DataRow("*", true)]
        [DataRow("1.*", true)]
        [DataRow("55.66.77.*", true)]
        [DataRow("55.66.77*", true)]
        [DataRow("123.456.789.012", true)]
        [DataRow("1.2", true)]
        [DataRow("1.*.1", false)]
        [DataRow("1.*.*", false)]
        [DataRow("*.1", false)]
        [DataRow("a.b", false)]
        [DataRow("a.b.*", false)]
        public void IsSupportedVersionStringTest(string? versionString, bool isSupported)
        {
            Assert.AreEqual(isSupported, NuGetVersionHelper.IsSupportedVersionString(versionString));
        }

        [TestMethod]
        [DataRow(null, true)]
        [DataRow("", true)]
        [DataRow("*", true)]
        [DataRow("1.*", true)]
        [DataRow("55.66.77.*", true)]
        [DataRow("55.66.77*", true)]
        [DataRow("123.456.789.012", false)]
        [DataRow("1.2", false)]
        public void TryParseFloatRangeReturnsExpectedBoolFlag(string? versionString, bool isFloatingVersion)
        {
            Assert.AreEqual(isFloatingVersion, NuGetVersionHelper.TryParseFloatRangeEx(versionString, out _));
        }

        [TestMethod]
        [DataRow("1.2.3.4", null, true)]
        [DataRow("1.2.3.4", "", true)]
        [DataRow("1.2.3.4", "1.2.*", true)]
        [DataRow("1.2.3.4", "2.2*", false)]
        [DataRow("1.2.3.4", "1.2.*-*", true)]
        public void TryParseFloatRangeMatchingTest(string versionString, string? pattern, bool isMatch)
        {
            NuGetVersion version = new NuGetVersion(versionString);
            Assert.IsTrue(NuGetVersionHelper.TryParseFloatRangeEx(pattern, out FloatRange floatRange));
            Assert.AreEqual(isMatch, floatRange.Satisfies(version));
        }
    }
}
