// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.PackageValidation.Filtering;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation.Tests.Filtering
{
    [TestClass]
    public class TargetFrameworkFilterTests
    {
        [TestMethod]
        [DataRow("net8.0", "net8.0")]
        [DataRow("net8.0", "net8.0", "net9.0")]
        [DataRow("net8.0", "net8*")]
        [DataRow("net80.0", "net8*")]
        [DataRow("portable-net45+win8+wpa81+wp8", "portable-*")]
        [DataRow("portable-net45+win8+wpa81+wp8", "portable*")]
        public void IsExcluded_TargetFrameworkFound_ReturnsTrue(string targetFramework, params string[] excludedTargetFrameworks)
        {
            TargetFrameworkFilter targetFrameworkFilter = new(excludedTargetFrameworks);

            Assert.IsTrue(targetFrameworkFilter.IsExcluded(targetFramework));
        }

        [TestMethod]
        [DataRow("", "")]
        [DataRow("net8.0", "*")]
        [DataRow("net8.0", "net9.0")]
        [DataRow("net7.0", "net8.0", "net9.0")]
        public void IsExcluded_TargetFrameworkNotFound_ReturnsFalse(string targetFramework, params string[] excludedTargetFrameworks)
        {
            TargetFrameworkFilter targetFrameworkFilter = new(excludedTargetFrameworks);

            Assert.IsFalse(targetFrameworkFilter.IsExcluded(targetFramework));
        }

        [TestMethod]
        [DataRow("net8.0", "net8.0")]
        [DataRow("net8.0", "net8.0", "net9.0")]
        [DataRow("net8.0", "net8*")]
        [DataRow("net80.0", "net8*")]
        public void IsExcluded_NuGetFrameworkFound_ReturnsTrue(string targetFramework, params string[] excludedTargetFrameworks)
        {
            TargetFrameworkFilter targetFrameworkFilter = new(excludedTargetFrameworks);

            Assert.IsTrue(targetFrameworkFilter.IsExcluded(NuGetFramework.ParseFolder(targetFramework)));
        }

        [TestMethod]
        [DataRow("", "")]
        [DataRow("net8.0", "net9.0")]
        [DataRow("net7.0", "net8.0", "net9.0")]
        public void IsExcluded_NuGetFrameworkNotFound_ReturnsFalse(string targetFramework, params string[] excludedTargetFrameworks)
        {
            TargetFrameworkFilter targetFrameworkFilter = new(excludedTargetFrameworks);

            Assert.IsFalse(targetFrameworkFilter.IsExcluded(NuGetFramework.ParseFolder(targetFramework)));
        }

        [TestMethod]
        public void FoundExcludedTargetFrameworks_FrameworksFound_ReturnsEqual()
        {
            string[] excludedTargetFrameworks = ["netstandard2.0", "net4*"];
            string[] targetFrameworks = ["netstandard2.0", "net462"];
            TargetFrameworkFilter targetFrameworkFilter = new(excludedTargetFrameworks);

            foreach (string targetFramework in targetFrameworks)
            {
                Assert.IsTrue(targetFrameworkFilter.IsExcluded(targetFramework));
            }

            Assert.AreSequenceEqual(targetFrameworks, targetFrameworkFilter.FoundExcludedTargetFrameworks);
        }

        [TestMethod]
        public void FoundExcludedTargetFrameworks_FrameworksNotFound_ReturnsEmpty()
        {
            string[] excludedTargetFrameworks = ["netstandard2.0", "net4*"];
            string[] targetFrameworks = ["net6.0", "net7.0"];
            TargetFrameworkFilter targetFrameworkFilter = new(excludedTargetFrameworks);

            foreach (string targetFramework in targetFrameworks)
            {
                Assert.IsFalse(targetFrameworkFilter.IsExcluded(targetFramework));
            }

            Assert.IsEmpty(targetFrameworkFilter.FoundExcludedTargetFrameworks);
        }
    }
}
