// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.PackageValidation.Filtering;

namespace Microsoft.DotNet.PackageValidation.Tests.Filtering
{
    public class TargetFrameworkRegexFilterTests
    {
        [Theory]
        [InlineData("net8.0", "net8.0")]
        [InlineData("net8.0;net9.0", "net8.0")]
        [InlineData("net8*", "net8.0")]
        [InlineData("net8*", "net80.0")]
        public void IsExcluded_FrameworkFound_ReturnsTrue(string pattern, string targetFramework)
        {
            TargetFrameworkRegexFilter targetFrameworkRegexFilter = new(pattern);

            Assert.True(targetFrameworkRegexFilter.IsExcluded(targetFramework));
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("net9.0", "net8.0")]
        [InlineData("net8.0;net9.0", "net7.0")]
        public void IsExcluded_FrameworkNotFound_ReturnsFalse(string pattern, string targetFramework)
        {
            TargetFrameworkRegexFilter targetFrameworkRegexFilter = new(pattern);

            Assert.False(targetFrameworkRegexFilter.IsExcluded(targetFramework));
        }

        [Fact]
        public void FoundExcludedTargetFrameworks_FrameworksFound_ReturnsEqual()
        {
            const string pattern = "netstandard2.0;net4*";
            string[] targetFrameworks = [ "netstandard2.0", "net462" ];
            TargetFrameworkRegexFilter targetFrameworkRegexFilter = new(pattern);

            foreach (string targetFramework in targetFrameworks)
            {
                Assert.True(targetFrameworkRegexFilter.IsExcluded(targetFramework));
            }

            Assert.Equal(targetFrameworks, targetFrameworkRegexFilter.FoundExcludedTargetFrameworks);
        }

        [Fact]
        public void FoundExcludedTargetFrameworks_FrameworksNotFound_ReturnsEmpty()
        {
            const string pattern = "netstandard2.0;net4*";
            string[] targetFrameworks = ["net6.0", "net7.0"];
            TargetFrameworkRegexFilter targetFrameworkRegexFilter = new(pattern);

            foreach (string targetFramework in targetFrameworks)
            {
                Assert.False(targetFrameworkRegexFilter.IsExcluded(targetFramework));
            }

            Assert.Empty(targetFrameworkRegexFilter.FoundExcludedTargetFrameworks);
        }
    }
}
