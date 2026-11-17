// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Commands.Workload.List;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Workload.Tests;

[TestClass]
public class WorkloadCommandBaseTests : SdkTest
{
    [TestMethod]
    public void TempDirectoryCreatedByTheCommandIsDeletedAfterExecute()
    {
        var command = new TempDirectoryProbeCommand(Parser.Parse(["dotnet", "workload", "list"]));

        command.Execute().Should().Be(0);

        command.TempDirectoryExistedDuringExecute.Should().BeTrue();
        Directory.Exists(command.ObservedTempDirectory).Should().BeFalse();
    }

    [TestMethod]
    public void TempDirectoryCreatedByTheCommandIsDeletedWhenExecuteThrows()
    {
        var command = new TempDirectoryProbeCommand(Parser.Parse(["dotnet", "workload", "list"]), throwFromExecute: true);

        Action execute = () => command.Execute();

        execute.Should().Throw<GracefulException>();
        command.TempDirectoryExistedDuringExecute.Should().BeTrue();
        Directory.Exists(command.ObservedTempDirectory).Should().BeFalse();
    }

    [TestMethod]
    public void TempDirectoryPassedByTheCallerIsKept()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory().Path;
        var command = new TempDirectoryProbeCommand(Parser.Parse(["dotnet", "workload", "list"]), tempDirPath: testDirectory);

        command.Execute().Should().Be(0);

        command.ObservedTempDirectory.Should().Be(testDirectory);
        Directory.Exists(testDirectory).Should().BeTrue();
        File.Exists(command.ObservedPackagePath).Should().BeTrue();
    }

    [TestMethod]
    public void TempDirectoryFromTheTempDirOptionIsKept()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory().Path;
        var command = new TempDirectoryProbeCommand(Parser.Parse(["dotnet", "workload", "list", "--temp-dir", testDirectory]));

        command.Execute().Should().Be(0);

        command.ObservedTempDirectory.Should().Be(testDirectory);
        Directory.Exists(testDirectory).Should().BeTrue();
        File.Exists(command.ObservedPackagePath).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("search")]
    [DataRow("repair")]
    public void TempDirectoryIsDeletedWhenTheCommandConstructorThrows(string subcommand)
    {
        // The base constructor creates the temp directory, then the command's own constructor rejects --sdk-version, so
        // Execute never runs. Run the real CLI with its temp folder redirected to an empty directory to see what's left.
        var tempDirectory = TestAssetsManager.CreateTestDirectory(identifier: subcommand).Path;

        new DotnetCommand(Log)
            .WithEnvironmentVariable("TMP", tempDirectory)
            .WithEnvironmentVariable("TEMP", tempDirectory)
            .WithEnvironmentVariable("TMPDIR", tempDirectory)
            .Execute("workload", subcommand, "--sdk-version", "1.0.0")
            .Should()
            .Fail()
            .And
            .HaveStdErrContaining(CliCommandStrings.SdkVersionOptionNotSupported);

        Directory.GetDirectories(tempDirectory).Should().BeEmpty();
    }

    /// <summary>
    /// Records the temp directory a workload command was given and writes a package into it, the way the install and
    /// update commands do when they download workload packs.
    /// </summary>
    private sealed class TempDirectoryProbeCommand(ParseResult parseResult, string? tempDirPath = null, bool throwFromExecute = false)
        : WorkloadCommandBase<WorkloadListCommandDefinition>(parseResult, tempDirPath: tempDirPath)
    {
        public string? ObservedTempDirectory { get; private set; }

        public string? ObservedPackagePath { get; private set; }

        public bool TempDirectoryExistedDuringExecute { get; private set; }

        protected override int ExecuteCore()
        {
            ObservedTempDirectory = TempDirectoryPath;
            TempDirectoryExistedDuringExecute = Directory.Exists(TempDirectoryPath);

            Directory.CreateDirectory(TempPackagesDirectory.Value);
            ObservedPackagePath = Path.Combine(TempPackagesDirectory.Value, "mock.workload.pack.1.0.0.nupkg");
            File.WriteAllText(ObservedPackagePath, string.Empty);

            if (throwFromExecute)
            {
                throw new GracefulException("Simulated workload command failure.");
            }

            return 0;
        }
    }
}
