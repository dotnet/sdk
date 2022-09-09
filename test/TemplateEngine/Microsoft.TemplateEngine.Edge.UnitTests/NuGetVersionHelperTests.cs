// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Edge.Installers.NuGet;
using NuGet.Versioning;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class NuGetVersionHelperTests
    {
        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("*", true)]
        [InlineData("1.*", true)]
        [InlineData("55.66.77.*", true)]
        [InlineData("55.66.77*", true)]
        [InlineData("123.456.789.012", true)]
        [InlineData("1.2", true)]
        [InlineData("1.*.1", false)]
        [InlineData("1.*.*", false)]
        [InlineData("*.1", false)]
        [InlineData("a.b", false)]
        [InlineData("a.b.*", false)]
        public void IsSupportedVersionStringTest(string versionString, bool isSupported)
        {
            Assert.Equal(isSupported, NuGetVersionHelper.IsSupportedVersionString(versionString));
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("*", true)]
        [InlineData("1.*", true)]
        [InlineData("55.66.77.*", true)]
        [InlineData("55.66.77*", true)]
        [InlineData("123.456.789.012", false)]
        [InlineData("1.2", false)]
        public void TryParseFloatRangeReturnsExpectedBoolFlag(string versionString, bool isFloatingVersion)
        {
            Assert.Equal(isFloatingVersion, NuGetVersionHelper.TryParseFloatRangeEx(versionString, out _));
        }

        [Theory]
        [InlineData("1.2.3.4", null, true)]
        [InlineData("1.2.3.4", "", true)]
        [InlineData("1.2.3.4", "1.2.*", true)]
        [InlineData("1.2.3.4", "2.2*", false)]
        [InlineData("1.2.3.4", "1.2.*-*", true)]
        public void TryParseFloatRangeMatchingTest(string versionString, string pattern, bool isMatch)
        {
            NuGetVersion version = new NuGetVersion(versionString);
            Assert.True(NuGetVersionHelper.TryParseFloatRangeEx(pattern, out FloatRange floatRange));
            Assert.Equal(isMatch, floatRange.Satisfies(version));
        }
    }
}
