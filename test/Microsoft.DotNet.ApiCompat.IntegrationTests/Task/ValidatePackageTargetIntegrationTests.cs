// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.PackageValidation;

namespace Microsoft.DotNet.ApiCompat.Task.IntegrationTests
{
    public class ValidatePackageTargetIntegrationTests : SdkTest
    {
        public ValidatePackageTargetIntegrationTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void InvalidPackage()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("PackageValidationTestProject", allowCopyIfPresent: true)
                .WithSource();

            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:ForceValidationProblem=true");

            // No failures while running the package validation on a simple assembly.
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("error CP0002: Member 'void PackageValidationTestProject.Program.SomeAPINotInCore()' exists on lib/netstandard2.0/PackageValidationTestProject.dll but not on lib/net8.0/PackageValidationTestProject.dll", result.StdOut);
        }

        [Fact]
        public void ValidatePackageTargetRunsSuccessfully()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("PackageValidationTestProject", allowCopyIfPresent: true)
                .WithSource();

            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute();

            // No failures while running the package validation on a simple assembly.
            Assert.Equal(0, result.ExitCode);
        }

        [Fact]
        public void ValidatePackageTargetRunsSuccessfullyWithBaselineCheck()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("PackageValidationTestProject", allowCopyIfPresent: true)
                .WithSource();

            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageOutputPath={testAsset.TestRoot}");

            Assert.Equal(0, result.ExitCode);

            string packageValidationBaselinePath = Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.1.0.0.nupkg");
            result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;PackageValidationBaselinePath={packageValidationBaselinePath}");

            // No failures while running the package validation on a simple assembly.
            Assert.Equal(0, result.ExitCode);
        }

        [Fact]
        public void ValidatePackageTargetRunsSuccessfullyWithBaselineVersion()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("PackageValidationTestProject", allowCopyIfPresent: true)
                .WithSource();

            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageOutputPath={testAsset.TestRoot}");

            Assert.Equal(0, result.ExitCode);

            result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;PackageValidationBaselineVersion=1.0.0;PackageValidationBaselineName=PackageValidationTestProject");

            // No failures while running the package validation on a simple assembly.
            Assert.Equal(0, result.ExitCode);
        }

        [Fact]
        public void ValidatePackageTargetFailsWithBaselineVersion()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("PackageValidationTestProject", allowCopyIfPresent: true)
                .WithSource();

            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageOutputPath={testAsset.TestRoot}");

            Assert.Equal(0, result.ExitCode);

            string packageValidationBaselinePath = Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.1.0.0.nupkg");
            result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;AddBreakingChange=true;PackageValidationBaselinePath={packageValidationBaselinePath}");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("error CP0002: Member 'void PackageValidationTestProject.Program.SomeApiNotInLatestVersion()' exists on [Baseline] lib/net8.0/PackageValidationTestProject.dll but not on lib/net8.0/PackageValidationTestProject.dll", result.StdOut);
            Assert.Contains("error CP0002: Member 'void PackageValidationTestProject.Program.SomeApiNotInLatestVersion()' exists on [Baseline] lib/netstandard2.0/PackageValidationTestProject.dll but not on lib/netstandard2.0/PackageValidationTestProject.dll", result.StdOut);
        }

        [Fact]
        public void ValidatePackageTargetWithIncorrectBaselinePackagePath()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("PackageValidationTestProject", allowCopyIfPresent: true)
                .WithSource();

            string nonExistentPackageBaselinePath = Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.1.0.0.nupkg");
            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;PackageValidationBaselinePath={nonExistentPackageBaselinePath}");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains(string.Format(Resources.NonExistentPackagePath, nonExistentPackageBaselinePath), result.StdOut);

            // Disables package baseline validation.
            result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;DisablePackageBaselineValidation=true;PackageValidationBaselinePath={nonExistentPackageBaselinePath}");
            Assert.Equal(0, result.ExitCode);
        }

        [Fact]
        public void ValidatePackageTargetFailsWithBaselineVersionInStrictMode()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("PackageValidationTestProject", allowCopyIfPresent: true)
                .WithSource();

            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageOutputPath={testAsset.TestRoot}");

            Assert.Equal(0, result.ExitCode);

            string packageValidationBaselinePath = Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.1.0.0.nupkg");
            result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;ForceStrictModeBaselineValidationProblem=true;EnableStrictModeForBaselineValidation=true;PackageValidationBaselinePath={packageValidationBaselinePath}");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("error CP0002: Member 'void PackageValidationTestProject.Program.SomeApiOnlyInLatestVersion()' exists on lib/net8.0/PackageValidationTestProject.dll but not on [Baseline] lib/net8.0/PackageValidationTestProject.dll", result.StdOut);
            Assert.Contains("error CP0002: Member 'void PackageValidationTestProject.Program.SomeApiOnlyInLatestVersion()' exists on lib/netstandard2.0/PackageValidationTestProject.dll but not on [Baseline] lib/netstandard2.0/PackageValidationTestProject.dll", result.StdOut);
        }

        [Fact]
        public void ValidatePackageTargetSucceedsWithBaselineVersionNotInStrictMode()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("PackageValidationTestProject", allowCopyIfPresent: true)
                .WithSource();

            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageOutputPath={testAsset.TestRoot}");

            Assert.Equal(0, result.ExitCode);

            string packageValidationBaselinePath = Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.1.0.0.nupkg");
            result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;ForceStrictModeBaselineValidationProblem=true;PackageValidationBaselinePath={packageValidationBaselinePath}");

            Assert.Equal(0, result.ExitCode);
        }
    }
}
