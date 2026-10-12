// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Test;
using Microsoft.DotNet.Tools.Test.Utilities;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestRunsVSTestWithAPrivateArtifactsDirectory : SdkTest, IDisposable
    {
        private const string TestAppName = "VSTestForwardDotnetRootEnvironmentVariables";

        /// <summary>
        /// Set only when the default base directory cannot be resolved - on Windows when there is no local application data folder, on Linux when this agent has no /run/user/&lt;uid&gt; - common for hosted CI agents and
        /// containers without a systemd-logind session. VSTestArtifactsDirectory.GetBaseDirectory deliberately
        /// throws rather than falling back to the shared system temporary directory there, so these end-to-end
        /// tests need a working override to get one at all. The child dotnet test/dotnet vstest processes below
        /// inherit this process's environment, so setting it here reaches them.
        /// </summary>
        private readonly string _baseDirectoryOverride;

        public GivenDotnetTestRunsVSTestWithAPrivateArtifactsDirectory(ITestOutputHelper log)
            : base(log)
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
            {
                try
                {
                    VSTestArtifactsDirectory.GetBaseDirectory();
                }
                catch (Exception ex) when (ex is IOException or Microsoft.DotNet.Cli.Utils.GracefulException)
                {
                    _baseDirectoryOverride = Path.Combine(Path.GetTempPath(), "vstest-artifacts-e2e-tests-" + Guid.NewGuid());
                    Directory.CreateDirectory(_baseDirectoryOverride);
                    Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, _baseDirectoryOverride);
                }
            }
        }

        public void Dispose()
        {
            if (_baseDirectoryOverride != null)
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, null);
                Directory.Delete(_baseDirectoryOverride, recursive: true);
            }
        }

        [Fact]
        public void ItGivesTheTestHostAPrivateArtifactsDirectoryWhenTestingADll()
        {
            string outputDll = BuildTestAsset(nameof(ItGivesTheTestHostAPrivateArtifactsDirectoryWhenTestingADll));

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                .Execute(outputDll, "--logger:console;verbosity=detailed");

            AssertPrivateArtifactsDirectory(result);
        }

        [Fact]
        public void ItGivesTheTestHostAPrivateArtifactsDirectoryWhenUsingDotnetVSTest()
        {
            string outputDll = BuildTestAsset(nameof(ItGivesTheTestHostAPrivateArtifactsDirectoryWhenUsingDotnetVSTest));

            CommandResult result = new DotnetVSTestCommand(Log)
                .Execute(outputDll, "--logger:console;verbosity=detailed");

            AssertPrivateArtifactsDirectory(result);
        }

        [Fact]
        public void ItFlowsThePrivateArtifactsDirectoryThroughMSBuildWhenTestingAProject()
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset(TestAppName, nameof(ItFlowsThePrivateArtifactsDirectoryThroughMSBuildWhenTestingAProject))
                .WithSource()
                .WithVersionVariables();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: true)
                .WithWorkingDirectory(testAsset.Path)
                .Execute("--logger", "console;verbosity=detailed");

            AssertPrivateArtifactsDirectory(result);
        }

        private string BuildTestAsset(string identifier)
        {
            TestAsset testAsset = _testAssetsManager.CopyTestAsset(TestAppName, identifier)
                .WithSource()
                .WithVersionVariables();

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();

            string configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            return Path.Combine(
                buildCommand.GetOutputDirectory(configuration: configuration).FullName,
                $"{TestAppName}.dll");
        }

        private static void AssertPrivateArtifactsDirectory(CommandResult result)
        {
            result.ExitCode.Should().Be(0);

            string artifactsDirectory = GetReportedValue(
                result.StdOut,
                "TESTHOST_VSTEST_ARTIFACTS_DIRECTORY");

            artifactsDirectory.Should().NotBeNullOrEmpty();
            artifactsDirectory.Should().StartWith(Path.TrimEndingDirectorySeparator(VSTestArtifactsDirectory.GetBaseDirectory()));
            Guid.TryParse(Path.GetFileName(artifactsDirectory), out _).Should().BeTrue();

            Path.TrimEndingDirectorySeparator(GetReportedValue(result.StdOut, "TESTHOST_TEMPPATH"))
                .Should().Be(Path.TrimEndingDirectorySeparator(Path.GetTempPath()));

            Directory.Exists(artifactsDirectory).Should().BeFalse();
        }

        private static string GetReportedValue(string stdOut, string name)
        {
            string prefix = name + "=";
            int start = stdOut.IndexOf(prefix, StringComparison.Ordinal);
            start.Should().BeGreaterThanOrEqualTo(0, $"Output was:\n{stdOut}");

            start += prefix.Length;
            int end = stdOut.IndexOfAny(new[] { '\r', '\n' }, start);
            return (end < 0 ? stdOut[start..] : stdOut[start..end]).Trim();
        }
    }
}
