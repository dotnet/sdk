// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    public class ValidatePackageTargetTests : SdkTest
    {
        public ValidatePackageTargetTests(ITestOutputHelper log) : base(log)
        {
            // clearing the cache
            string localSdkNugetCacheValidationPath = Path.Combine(TestContext.Current.NuGetCachePath, "microsoft.dotnet.packagevalidation");
            if (Directory.Exists(localSdkNugetCacheValidationPath))
                Directory.Delete(localSdkNugetCacheValidationPath, recursive: true);

            // Packing and copying the local version package validation
            string packageValidationProjectPath = Path.Combine(Path.GetDirectoryName(_testAssetsManager.TestAssetsRoot), "Compatibility", "Microsoft.DotNet.PackageValidation", "Microsoft.DotNet.PackageValidation.csproj");
            new PackCommand(Log, packageValidationProjectPath)
                .Execute($"-p:PackageVersion=1.0.0-test;PackageOutputPath={TestContext.Current.TestPackages}");
        }

        [Fact]
        public void ValidatePackageTargetRunsSuccessfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("PackageValidationTestProject")
                .WithSource();

            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute();

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(Resources.SuccessfulPackageRun, result.StdOut);
        }

        [Fact]
        public void ValidatePackageTargetRunsSuccessfullyWithBaselineCheck()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("PackageValidationTestProject")
                .WithSource();

            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageOutputPath={testAsset.TestRoot}");

            Assert.Equal(0, result.ExitCode);

            string packageValidationBaselinePath = Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.1.0.0.nupkg");
            result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;PackageValidationBaselinePath={packageValidationBaselinePath}");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(Resources.SuccessfulPackageRun, result.StdOut);
        }

        [Fact]
        public void ValidatePackageTargetRunsSuccessfullyWithBaselineVersion()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("PackageValidationTestProject")
                .WithSource();

            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageOutputPath={testAsset.TestRoot}");

            Assert.Equal(0, result.ExitCode);

            result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;PackageValidationBaselineVersion=1.0.0");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(Resources.SuccessfulPackageRun, result.StdOut);
        }


        [Fact]
        public void ValidatePackageTargetWithIncorrectBaselinePackagePath()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("PackageValidationTestProject")
                .WithSource();

            string nonExistentPackageBaselinePath = Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.1.0.0.nupkg");
            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;PackageValidationBaselinePath={nonExistentPackageBaselinePath}");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains($"{nonExistentPackageBaselinePath} does not exist. Please check the PackageValidationBaselinePath or PackageValidationBaselineVersion.", result.StdOut);
            Assert.DoesNotContain(Resources.SuccessfulPackageRun, result.StdOut);
        }
    }
}
