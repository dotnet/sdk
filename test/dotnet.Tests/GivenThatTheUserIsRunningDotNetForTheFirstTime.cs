// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;

//[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.DotNet.Tests
{
    public class DotNetFirstTime
    {
        public DirectoryInfo NugetFallbackFolder;
        public DirectoryInfo DotDotnetFolder;
        public string TestDirectory;

        public TestCommand Setup(ITestOutputHelper log, TestAssetsManager testAssets, [CallerMemberName] string testName = null)
        {
            TestDirectory = testAssets.CreateTestDirectory(testName).Path;
            var testNuGetHome = Path.Combine(TestDirectory, "nuget_home");
            var cliTestFallbackFolder = Path.Combine(testNuGetHome, ".dotnet", "NuGetFallbackFolder");
            var profiled = Path.Combine(TestDirectory, "profile.d");
            var pathsd = Path.Combine(TestDirectory, "paths.d");

            var command = new DotnetCommand(log)
                .WithWorkingDirectory(TestDirectory)
                .WithEnvironmentVariable("APPDATA", testNuGetHome)
                .WithEnvironmentVariable("DOTNET_CLI_TEST_FALLBACKFOLDER", cliTestFallbackFolder)
                .WithEnvironmentVariable("DOTNET_CLI_TEST_LINUX_PROFILED_PATH", profiled)
                .WithEnvironmentVariable("DOTNET_CLI_TEST_OSX_PATHSD_PATH", pathsd)
                .WithEnvironmentVariable("SkipInvalidConfigurations", "true")
                .WithEnvironmentVariable(CliFolderPathCalculator.DotnetHomeVariableName, testNuGetHome);

            NugetFallbackFolder = new DirectoryInfo(cliTestFallbackFolder);
            DotDotnetFolder = new DirectoryInfo(Path.Combine(testNuGetHome, ".dotnet"));

            return command;
        }
    }

    public class DotNetFirstTimeFixture : IDisposable
    {
        public CommandResult FirstDotnetNonVerbUseCommandResult;
        public CommandResult FirstDotnetVerbUseCommandResult;
        public CommandResult FirstDotnetWorkloadInfoResult;
        public DirectoryInfo NugetFallbackFolder;
        public DirectoryInfo DotDotnetFolder;
        public string TestDirectory;

        public Dictionary<string, string> ExtraEnvironmentVariables = new();

        public void Init(ITestOutputHelper log, TestAssetsManager testAssets)
        {
            if (TestDirectory == null)
            {
                var dotnetFirstTime = new DotNetFirstTime();

                var command = dotnetFirstTime.Setup(log, testAssets, testName: "Dotnet_first_time_experience_tests");

                FirstDotnetNonVerbUseCommandResult = command.Execute("--info");
                FirstDotnetVerbUseCommandResult = command.Execute("new", "--debug:ephemeral-hive");

                TestDirectory = dotnetFirstTime.TestDirectory;
                NugetFallbackFolder = dotnetFirstTime.NugetFallbackFolder;
                DotDotnetFolder = dotnetFirstTime.DotDotnetFolder;
            }
        }

        public void Dispose()
        {

        }
    }

    [TestClass]
    public class GivenThatTheUserIsRunningDotNetForTheFirstTime : SdkTest
    {
        private static DotNetFirstTimeFixture _fixtureInstance;
        private DotNetFirstTimeFixture _fixture = null!;

        [TestInitialize]
        public void TestInit()
        {
            _fixtureInstance ??= new DotNetFirstTimeFixture();
            _fixtureInstance.Init(Log, TestAssetsManager);
            _fixture = _fixtureInstance;
        }

        [TestMethod]
        public void UsingDotnetForTheFirstTimeSucceeds()
        {
            _fixture.FirstDotnetVerbUseCommandResult
                .Should()
                .Pass();
        }

        [TestMethod]
        public void UsingDotnetForTheFirstTimeWithNonVerbsDoesNotPrintEula()
        {
            string firstTimeNonVerbUseMessage = Cli.Utils.LocalizableStrings.DotNetSdkInfoLabel;

            _fixture.FirstDotnetNonVerbUseCommandResult.StdOut
                .Should()
                .StartWith(firstTimeNonVerbUseMessage);
        }

        [TestMethod]
        public void ItShowsTheAppropriateMessageToTheUser()
        {

            var expectedVersion = GetDotnetVersion();
            _fixture.FirstDotnetVerbUseCommandResult.StdErr
                .Should()
                .ContainVisuallySameFragment(string.Format(
                    Configurer.LocalizableStrings.FirstTimeMessageWelcome,
                    DotnetFirstTimeUseConfigurer.ParseDotNetVersion(expectedVersion),
                    expectedVersion))
                .And.ContainVisuallySameFragment(Configurer.LocalizableStrings.FirstTimeMessageMoreInformation)
                .And.NotContain("Restore completed in");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void FirstRunExperienceMessagesShouldGoToStdErr()
        {
            // This test ensures that first-run experience messages go to stderr, 
            // not stdout, to avoid interfering with completion commands and other
            // tools that parse stdout. See: https://github.com/dotnet/sdk/issues/50444
            var expectedVersion = GetDotnetVersion();
            
            // StdErr should contain first-run messages
            _fixture.FirstDotnetVerbUseCommandResult.StdErr
                .Should()
                .ContainVisuallySameFragment(string.Format(
                    Configurer.LocalizableStrings.FirstTimeMessageWelcome,
                    DotnetFirstTimeUseConfigurer.ParseDotNetVersion(expectedVersion),
                    expectedVersion))
                .And.ContainVisuallySameFragment(Configurer.LocalizableStrings.FirstTimeMessageMoreInformation);
                
            // StdOut should NOT contain first-run messages (they should only be in stderr)
            _fixture.FirstDotnetVerbUseCommandResult.StdOut
                .Should()
                .NotContain("Welcome to .NET")
                .And.NotContain("Write your first app");
        }

        [TestMethod]
        public void ItCreatesAFirstUseSentinelFileUnderTheDotDotNetFolder()
        {
            _fixture.DotDotnetFolder
                .Should()
                .HaveFile($"{GetDotnetVersion()}.dotnetFirstUseSentinel");
        }

        [TestMethod]
        public void ItCreatesAnAspNetCertificateSentinelFileUnderTheDotDotNetFolder()
        {
            _fixture.DotDotnetFolder
                .Should()
                .HaveFile($"{GetDotnetVersion()}.aspNetCertificateSentinel");
        }

        [TestMethod]
        public void ItDoesNotCreateAFirstUseSentinelFileNorAnAspNetCertificateSentinelFileUnderTheDotDotNetFolderWhenInternalReportInstallSuccessIsInvoked()
        {
            var dotnetFirstTime = new DotNetFirstTime();

            var command = dotnetFirstTime.Setup(Log, TestAssetsManager);

            // Disable telemetry to prevent the creation of the .dotnet folder for machineid and docker cache files.
            command = command.WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "true");

            command.Execute("internal-reportinstallsuccess", "test").Should().Pass();

            var homeFolder = dotnetFirstTime.NugetFallbackFolder.Parent;
            homeFolder.Should().NotExist();
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void ItShowsTheTelemetryNoticeWhenInvokingACommandAfterInternalReportInstallSuccessHasBeenInvoked()
        {
            var dotnetFirstTime = new DotNetFirstTime();

            var command = dotnetFirstTime.Setup(Log, TestAssetsManager);

            command.Execute("internal-reportinstallsuccess", "test").Should().Pass();

            var result = command.Execute("new", "--debug:ephemeral-hive");

            var expectedVersion = GetDotnetVersion();

            result.StdErr
                .Should()
                .ContainVisuallySameFragment(string.Format(
                    Configurer.LocalizableStrings.FirstTimeMessageWelcome,
                    DotnetFirstTimeUseConfigurer.ParseDotNetVersion(expectedVersion),
                    expectedVersion))
                .And.ContainVisuallySameFragment(Configurer.LocalizableStrings.FirstTimeMessageMoreInformation);
        }

        [TestMethod]
        public void ItShowsTheAspNetCertificateGenerationMessageWhenInvokingACommandAfterInternalReportInstallSuccessHasBeenInvoked()
        {
            var dotnetFirstTime = new DotNetFirstTime();

            var command = dotnetFirstTime.Setup(Log, TestAssetsManager);


            command.Execute("internal-reportinstallsuccess", "test").Should().Pass();

            command.Execute("new", "--debug:ephemeral-hive");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Linux)]
        public void ItCreatesTheProfileFileOnLinuxWhenInvokedFromNativeInstaller()
        {
            var dotnetFirstTime = new DotNetFirstTime();

            var command = dotnetFirstTime.Setup(Log, TestAssetsManager)
                .WithEnvironmentVariable("DOTNET_ADD_GLOBAL_TOOLS_TO_PATH", "true");

            var profiled = Path.Combine(dotnetFirstTime.TestDirectory, "profile.d");

            command.Execute("internal-reportinstallsuccess", "test").Should().Pass();

            File.Exists(profiled).Should().BeTrue();
            File.ReadAllText(profiled).Should().Be(
                $"export PATH=\"$PATH:{CliFolderPathCalculator.ToolsShimPathInUnix.PathWithDollar}\"");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.OSX)]
        public void ItCreatesThePathDFileOnMacOSWhenInvokedFromNativeInstaller()
        {
            var dotnetFirstTime = new DotNetFirstTime();

            var command = dotnetFirstTime.Setup(Log, TestAssetsManager)
                .WithEnvironmentVariable("DOTNET_ADD_GLOBAL_TOOLS_TO_PATH", "true");

            var pathsd = Path.Combine(dotnetFirstTime.TestDirectory, "paths.d");

            command.Execute("internal-reportinstallsuccess", "test").Should().Pass();

            File.Exists(pathsd).Should().BeTrue();
            File.ReadAllText(pathsd).Should().Be(CliFolderPathCalculator.ToolsShimPathInUnix.PathWithTilde);
        }

        private string GetDotnetVersion()
        {
            return SdkTestContext.Current.ToolsetUnderTest.SdkVersion;
        }
    }
}
