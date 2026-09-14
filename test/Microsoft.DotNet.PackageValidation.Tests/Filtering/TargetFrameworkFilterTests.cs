// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.PackageValidation.Filtering;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation.Tests.Filtering
{
    public class TargetFrameworkFilterTests
    {
        [Theory]
        [InlineData("net8.0", "net8.0")]
        [InlineData("net8.0", "net8.0", "net9.0")]
        [InlineData("net8.0", "net8*")]
        [InlineData("net80.0", "net8*")]
        [InlineData("portable-net45+win8+wpa81+wp8", "portable-*")]
        [InlineData("portable-net45+win8+wpa81+wp8", "portable*")]
        public void IsExcluded_TargetFrameworkFound_ReturnsTrue(string targetFramework, params string[] excludedTargetFrameworks)
        {
            TargetFrameworkFilter targetFrameworkFilter = new(excludedTargetFrameworks);

            Assert.True(targetFrameworkFilter.IsExcluded(targetFramework));
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("net8.0", "*")]
        [InlineData("net8.0", "net9.0")]
        [InlineData("net7.0", "net8.0", "net9.0")]
        public void IsExcluded_TargetFrameworkNotFound_ReturnsFalse(string targetFramework, params string[] excludedTargetFrameworks)
        {
            TargetFrameworkFilter targetFrameworkFilter = new(excludedTargetFrameworks);

            Assert.False(targetFrameworkFilter.IsExcluded(targetFramework));
        }

        [Theory]
        [InlineData("net8.0", "net8.0")]
        [InlineData("net8.0", "net8.0", "net9.0")]
        [InlineData("net8.0", "net8*")]
        [InlineData("net80.0", "net8*")]
        public void IsExcluded_NuGetFrameworkFound_ReturnsTrue(string targetFramework, params string[] excludedTargetFrameworks)
        {
            TargetFrameworkFilter targetFrameworkFilter = new(excludedTargetFrameworks);

            Assert.True(targetFrameworkFilter.IsExcluded(NuGetFramework.ParseFolder(targetFramework)));
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("net8.0", "net9.0")]
        [InlineData("net7.0", "net8.0", "net9.0")]
        public void IsExcluded_NuGetFrameworkNotFound_ReturnsFalse(string targetFramework, params string[] excludedTargetFrameworks)
        {
            TargetFrameworkFilter targetFrameworkFilter = new(excludedTargetFrameworks);

            Assert.False(targetFrameworkFilter.IsExcluded(NuGetFramework.ParseFolder(targetFramework)));
        }

        [Fact]
        public void FoundExcludedTargetFrameworks_FrameworksFound_ReturnsEqual()
        {
            string[] excludedTargetFrameworks = ["netstandard2.0", "net4*"];
            string[] targetFrameworks = ["netstandard2.0", "net462"];
            TargetFrameworkFilter targetFrameworkFilter = new(excludedTargetFrameworks);

            foreach (string targetFramework in targetFrameworks)
            {
                Assert.True(targetFrameworkFilter.IsExcluded(targetFramework));
            }

            Assert.Equal(targetFrameworks, targetFrameworkFilter.FoundExcludedTargetFrameworks);
        }

        [Fact]
        public void FoundExcludedTargetFrameworks_FrameworksNotFound_ReturnsEmpty()
        {
            string[] excludedTargetFrameworks = ["netstandard2.0", "net4*"];
            string[] targetFrameworks = ["net6.0", "net7.0"];
            TargetFrameworkFilter targetFrameworkFilter = new(excludedTargetFrameworks);

            foreach (string targetFramework in targetFrameworks)
            {
                Assert.False(targetFrameworkFilter.IsExcluded(targetFramework));
            }

            Assert.Empty(targetFrameworkFilter.FoundExcludedTargetFrameworks);
        }
    }
}
