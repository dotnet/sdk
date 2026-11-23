// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Tools.Test.Utilities;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    /// <summary>
    /// End-to-end coverage for the private artifacts directory the SDK gives to vstest.console. These tests run
    /// the real CLI and assert on what the <em>test host</em> observed, which is the only way to prove that the
    /// directory actually reaches the process that writes the artifacts.
    /// </summary>
    [TestClass]
    public class GivenDotnetTestRunsVSTestWithAPrivateArtifactsDirectory : SdkTest
    {
        private const string TestAppName = "VSTestForwardDotnetRootEnvironmentVariables";

        private static readonly string[] ConsoleLoggerOutputDetailed = ["--logger", "console;verbosity=detailed"];

        /// <summary>
        /// Set only when the default base directory cannot be resolved - on Windows when there is no local application data folder, on Linux when this agent has no <c>/run/user/&lt;uid&gt;</c> - common for hosted CI agents and
        /// containers without a systemd-logind session. <see cref="VSTestArtifactsDirectory.GetBaseDirectory"/>
        /// deliberately throws rather than falling back to the shared system temporary directory there, so these
        /// end-to-end tests - which assert that a private directory was given to the test host, not which of the
        /// two Linux contracts produced it - need a working override to get one at all. The child `dotnet test`/
        /// `dotnet vstest` processes below inherit this process's environment, so setting it here reaches them.
        /// </summary>
        private static string? s_classLevelBaseDirectoryOverride;

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
            {
                try
                {
                    VSTestArtifactsDirectory.GetBaseDirectory();
                }
                catch (Exception ex) when (ex is IOException or Microsoft.DotNet.Cli.Utils.GracefulException)
                {
                    s_classLevelBaseDirectoryOverride = Path.Combine(Path.GetTempPath(), "vstest-artifacts-e2e-tests-" + Guid.NewGuid());
                    Directory.CreateDirectory(s_classLevelBaseDirectoryOverride);
                    Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, s_classLevelBaseDirectoryOverride);
                }
            }
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (s_classLevelBaseDirectoryOverride is not null)
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, null);
                Directory.Delete(s_classLevelBaseDirectoryOverride, recursive: true);
            }
        }

        [TestMethod]
        public void ItGivesTheTestHostAPrivateArtifactsDirectoryWhenTestingADll()
        {
            string outputDll = BuildTestAsset(nameof(ItGivesTheTestHostAPrivateArtifactsDirectoryWhenTestingADll));

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                .Execute(outputDll, "--logger:console;verbosity=detailed");

            result.ExitCode.Should().Be(0);

            string artifactsDirectory = GetReportedValue(result.StdOut, "TESTHOST_VSTEST_ARTIFACTS_DIRECTORY");
            AssertIsPrivateArtifactsDirectory(artifactsDirectory);
            AssertTemporaryDirectoryUntouched(result.StdOut);
            AssertDeletedAfterTheRun(artifactsDirectory);
        }

        [TestMethod]
        public void ItGivesTheTestHostAPrivateArtifactsDirectoryWhenUsingDotnetVSTest()
        {
            string outputDll = BuildTestAsset(nameof(ItGivesTheTestHostAPrivateArtifactsDirectoryWhenUsingDotnetVSTest));

            CommandResult result = new DotnetVSTestCommand(Log)
                .Execute(outputDll, "--logger:console;verbosity=detailed");

            result.ExitCode.Should().Be(0);

            string artifactsDirectory = GetReportedValue(result.StdOut, "TESTHOST_VSTEST_ARTIFACTS_DIRECTORY");
            AssertIsPrivateArtifactsDirectory(artifactsDirectory);
            AssertTemporaryDirectoryUntouched(result.StdOut);
            AssertDeletedAfterTheRun(artifactsDirectory);
        }

        [TestMethod]
        public void ItFlowsThePrivateArtifactsDirectoryThroughMSBuildWhenTestingAProject()
        {
            TestAsset testAsset = TestAssetsManager
                .CopyTestAsset(TestAppName, nameof(ItFlowsThePrivateArtifactsDirectoryThroughMSBuildWhenTestingAProject))
                .WithSource()
                .WithVersionVariables();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: true)
                .WithWorkingDirectory(testAsset.Path)
                .Execute(ConsoleLoggerOutputDetailed);

            result.ExitCode.Should().Be(0);

            // Here vstest.console is started by the VSTest MSBuild task, so the variable is set on MSBuild and
            // inherited all the way down to the test host.
            string artifactsDirectory = GetReportedValue(result.StdOut, "TESTHOST_VSTEST_ARTIFACTS_DIRECTORY");
            AssertIsPrivateArtifactsDirectory(artifactsDirectory);
            AssertTemporaryDirectoryUntouched(result.StdOut);
            AssertDeletedAfterTheRun(artifactsDirectory);
        }

        private string BuildTestAsset(string identifier)
        {
            TestAsset testAsset = TestAssetsManager.CopyTestAsset(TestAppName, identifier)
                .WithSource()
                .WithVersionVariables();

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();

            string configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            return Path.Combine(
                buildCommand.GetOutputDirectory(configuration: configuration).FullName,
                $"{TestAppName}.dll");
        }

        private static void AssertIsPrivateArtifactsDirectory(string artifactsDirectory)
        {
            artifactsDirectory.Should().NotBeNullOrEmpty("the SDK should have provided an artifacts directory");
            artifactsDirectory.Should().StartWith(
                Path.TrimEndingDirectorySeparator(VSTestArtifactsDirectory.GetBaseDirectory()),
                "the directory lives under the platform-specific private base directory");

            Guid.TryParse(Path.GetFileName(artifactsDirectory), out _)
                .Should().BeTrue($"the directory name '{Path.GetFileName(artifactsDirectory)}' should be a random GUID");
        }

        /// <summary>
        /// The whole reason for using a dedicated variable rather than TMPDIR is that the temporary directory of
        /// the test host - and with it the runtime diagnostics socket and everything else resolved through
        /// Path.GetTempPath() - stays where it was.
        /// </summary>
        private static void AssertTemporaryDirectoryUntouched(string? stdOut)
        {
            string reportedTempPath = Path.TrimEndingDirectorySeparator(GetReportedValue(stdOut, "TESTHOST_TEMPPATH"));

            reportedTempPath.Should().Be(
                Path.TrimEndingDirectorySeparator(Path.GetTempPath()),
                "the test host should keep the default temporary directory");
        }

        private static void AssertDeletedAfterTheRun(string artifactsDirectory) =>
            Directory.Exists(artifactsDirectory)
                .Should().BeFalse("the artifacts directory should be deleted when the invocation completes");

        private static string GetReportedValue(string? stdOut, string name)
        {
            stdOut.Should().NotBeNull("the CLI should have produced output");

            string prefix = name + "=";

            // The console logger indents and interleaves the output of the test host, so the reported value is
            // located inside the stream rather than at the start of a line.
            int start = stdOut!.IndexOf(prefix, StringComparison.Ordinal);
            start.Should().BeGreaterThanOrEqualTo(0, $"the test host should report '{name}'. Output was:\n{stdOut}");

            start += prefix.Length;
            int end = stdOut.IndexOfAny(['\r', '\n'], start);

            return (end < 0 ? stdOut[start..] : stdOut[start..end]).Trim();
        }
    }
}
