// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using BuildCommand = Microsoft.DotNet.Tools.Build.BuildCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetOsArchOptions : SdkTest
    {
        public GivenDotnetOsArchOptions(ITestOutputHelper log) : base(log)
        {
        }

        private static readonly string[] ExpectedPrefix = ["-maxcpucount", "-verbosity:m", "-tlp:default=auto", "-nologo"];
        private const string NugetInteractiveProperty = "-property:NuGetInteractive=true";
        private static readonly string[] DefaultArgs = ["-restore", "-consoleloggerparameters:Summary", NugetInteractiveProperty];

        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetBuildInvocation));

        [Fact]
        public void OsOptionIsCorrectlyResolved()
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var command = BuildCommand.FromArgs(["--os", "os"], msbuildPath);
                var expectedArch = RuntimeInformation.ProcessArchitecture.Equals(Architecture.Arm64) ? "arm64" : Environment.Is64BitOperatingSystem ? "x64" : "x86";

                command.GetArgumentTokensToMSBuild()
                    .Should()
                    .BeEquivalentTo([.. ExpectedPrefix, .. DefaultArgs, $"-property:RuntimeIdentifier=os-{expectedArch}"]);
            });
        }

        [Fact]
        public void ArchOptionIsCorrectlyResolved()
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var command = BuildCommand.FromArgs(["--arch", "arch"], msbuildPath);
                var expectedOs = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" :
                    null;
                if (expectedOs == null)
                {
                    // Not a supported OS for running test
                    return;
                }
                command.GetArgumentTokensToMSBuild()
                    .Should()
                    .BeEquivalentTo([.. ExpectedPrefix, .. DefaultArgs, $"-property:RuntimeIdentifier={expectedOs}-arch"]);
            });
        }

        [Fact]
        public void OSAndArchOptionsCanBeCombined()
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var command = BuildCommand.FromArgs(["--arch", "arch", "--os", "os"], msbuildPath);
                command.GetArgumentTokensToMSBuild()
                    .Should()
                    .BeEquivalentTo([.. ExpectedPrefix, .. DefaultArgs, "-property:RuntimeIdentifier=os-arch"]);
            });
        }

        [Fact]
        public void OptionsRespectUserSpecifiedSelfContained()
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var command = BuildCommand.FromArgs(["--arch", "arch", "--os", "os", "--self-contained"], msbuildPath);
                string[] expectedArgs = [
                    .. ExpectedPrefix,
                    .. DefaultArgs,
                    "-property:SelfContained=True",
                    "-property:_CommandLineDefinedSelfContained=true",
                    "-property:RuntimeIdentifier=os-arch"
                ];
                command.GetArgumentTokensToMSBuild()
                    .Should()
                    .BeEquivalentTo(expectedArgs);
            });
        }

        [Fact]
        public void OSOptionCannotBeCombinedWithRuntime()
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var exceptionThrown = Assert.Throws<GracefulException>(() => BuildCommand.FromArgs(["--os", "os", "--runtime", "rid"], msbuildPath));
                exceptionThrown.Message.Should().Be(CommonLocalizableStrings.CannotSpecifyBothRuntimeAndOsOptions);
            });
        }

        [Fact]
        public void ArchOptionCannotBeCombinedWithRuntime()
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var exceptionThrown = Assert.Throws<GracefulException>(() => BuildCommand.FromArgs(["--arch", "arch", "--runtime", "rid"], msbuildPath));
                exceptionThrown.Message.Should().Be(CommonLocalizableStrings.CannotSpecifyBothRuntimeAndArchOptions);
            });
        }

        [WindowsOnlyTheory]
        [InlineData("build")]
        [InlineData("publish")]
        [InlineData("test")]
        [InlineData("run")]
        public void CommandsRunWithOSOption(string command)
        {
            var testInstance = _testAssetsManager.CopyTestAsset("HelloWorld", identifier: command)
                .WithSource();

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(command, "--os", "win")
                .Should()
                .Pass();
        }

        [WindowsOnlyTheory]
        [InlineData("build")]
        [InlineData("publish")]
        [InlineData("test")]
        [InlineData("run")]
        public void CommandsRunWithArchOption(string command)
        {
            var testInstance = _testAssetsManager.CopyTestAsset("HelloWorld", identifier: command)
                .WithSource();

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(command, "--arch", RuntimeInformation.ProcessArchitecture.Equals(Architecture.Arm64) ? "arm64" : Environment.Is64BitOperatingSystem ? "x64" : "x86")
                .Should()
                .Pass();
        }

        [Fact]
        public void ArchOptionsAMD64toX64()
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var command = BuildCommand.FromArgs(["--arch", "amd64", "--os", "os"], msbuildPath);
                command.GetArgumentTokensToMSBuild()
                    .Should()
                    .BeEquivalentTo([.. ExpectedPrefix, .. DefaultArgs, "-property:RuntimeIdentifier=os-x64"]);
            });
        }

        [Fact]
        public void ArchOptionIsResolvedFromRidUnderDifferentCulture()
        {
            CultureInfo currentCultureBefore = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("th");
                CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
                {
                    var msbuildPath = "<msbuildpath>";
                    var command = BuildCommand.FromArgs(["--os", "os"], msbuildPath);
                    var expectedArch = RuntimeInformation.ProcessArchitecture.Equals(Architecture.Arm64) ? "arm64" : Environment.Is64BitOperatingSystem ? "x64" : "x86";
                    command.GetArgumentTokensToMSBuild()
                        .Should()
                        .BeEquivalentTo([.. ExpectedPrefix, .. DefaultArgs, $"-property:RuntimeIdentifier=os-{expectedArch}"]);
                });
            }
            finally { CultureInfo.CurrentCulture = currentCultureBefore; }
        }

        [Fact]
        public void OsOptionIsResolvedFromRidUnderDifferentCulture()
        {
            CultureInfo currentCultureBefore = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("th");
                CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
                {
                    var msbuildPath = "<msbuildpath>";
                    var command = BuildCommand.FromArgs(["--arch", "arch"], msbuildPath);
                    var expectedOs = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
                        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
                        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" :
                        null;
                    if (expectedOs == null)
                    {
                        // Not a supported OS for running test
                        return;
                    }
                    command.GetArgumentTokensToMSBuild()
                        .Should()
                        .BeEquivalentTo([.. ExpectedPrefix, .. DefaultArgs, $"-property:RuntimeIdentifier={expectedOs}-arch"]);
                });
            }
            finally { CultureInfo.CurrentCulture = currentCultureBefore; }
        }
    }
}
