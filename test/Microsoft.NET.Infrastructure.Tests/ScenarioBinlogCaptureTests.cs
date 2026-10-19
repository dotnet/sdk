// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Logging;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Infrastructure.Tests;

[TestClass]
public sealed class ScenarioBinlogCaptureTests : SdkTest
{
    [TestMethod]
    public void DotnetBuildCapturesReadableScenarioBinlogWhenEnabled()
    {
        TestAsset testAsset = TestAssetsManager.CopyTestAsset("MSBuildTestApp").WithSource();
        TestDirectory diagnosticDirectory = TestAssetsManager.CreateTestDirectory();

        var command = new DotnetBuildCommand(Log, testAsset.Path)
            .WithEnvironmentVariable(
                "DOTNET_SDK_TEST_SCENARIO_BINLOG_DIRECTORY",
                diagnosticDirectory.Path);

        CommandResult result = command.Execute();

        Assert.AreEqual(0, result.ExitCode, result.StdOut + result.StdErr);
        string binlogPath = Directory.GetFiles(diagnosticDirectory.Path, "*.binlog").Single();
        AssertReadableBinlog(binlogPath);
    }

    [TestMethod]
    public void MSBuildCommandCapturesReadableScenarioBinlogWhenEnabled()
    {
        TestAsset testAsset = TestAssetsManager.CopyTestAsset("MSBuildTestApp").WithSource();
        TestDirectory diagnosticDirectory = TestAssetsManager.CreateTestDirectory();
        var command = new MSBuildCommand(testAsset, "Build")
            .WithEnvironmentVariable(
                "DOTNET_SDK_TEST_SCENARIO_BINLOG_DIRECTORY",
                diagnosticDirectory.Path);

        CommandResult result = command.Execute();

        Assert.AreEqual(0, result.ExitCode, result.StdOut + result.StdErr);
        string binlogPath = Directory.GetFiles(diagnosticDirectory.Path, "*.binlog").Single();
        AssertReadableBinlog(binlogPath);
    }

    [TestMethod]
    public void ExplicitBinlogDestinationIsPreservedAndEachExecutionIsArchived()
    {
        TestAsset testAsset = TestAssetsManager.CopyTestAsset("MSBuildTestApp").WithSource();
        TestDirectory diagnosticDirectory = TestAssetsManager.CreateTestDirectory();
        string authoredBinlogPath = Path.Combine(testAsset.Path, "authored.binlog");
        var command = new DotnetBuildCommand(Log, testAsset.Path)
            .WithEnvironmentVariable(
                "DOTNET_SDK_TEST_SCENARIO_BINLOG_DIRECTORY",
                diagnosticDirectory.Path);

        CommandResult firstResult = command.Execute($"/bl:{authoredBinlogPath}");
        CommandResult secondResult = command.Execute($"/bl:{authoredBinlogPath}");

        Assert.AreEqual(0, firstResult.ExitCode, firstResult.StdOut + firstResult.StdErr);
        Assert.AreEqual(0, secondResult.ExitCode, secondResult.StdOut + secondResult.StdErr);
        AssertReadableBinlog(authoredBinlogPath);

        string authoredDirectory = Path.Combine(diagnosticDirectory.Path, "authored");
        string[] archivedBinlogs = Directory.GetFiles(authoredDirectory, "*.binlog");
        Assert.HasCount(2, archivedBinlogs);
        Assert.AreNotEqual(archivedBinlogs[0], archivedBinlogs[1]);
        foreach (string archivedBinlog in archivedBinlogs)
        {
            AssertReadableBinlog(archivedBinlog);
        }

        string expandedPathTemplate = Path.Combine(testAsset.Path, "expanded-{}.binlog");
        CommandResult expandedResult = command.Execute($"/bl:{expandedPathTemplate}");

        Assert.AreEqual(0, expandedResult.ExitCode, expandedResult.StdOut + expandedResult.StdErr);
        string[] expandedBinlogs = Directory.GetFiles(testAsset.Path, "expanded-*.binlog");
        Assert.HasCount(1, expandedBinlogs);
        AssertReadableBinlog(expandedBinlogs[0]);
    }

    [TestMethod]
    public void FailedBuildStillProducesReadableScenarioBinlog()
    {
        TestAsset testAsset = TestAssetsManager.CopyTestAsset("MSBuildTestApp").WithSource();
        TestDirectory diagnosticDirectory = TestAssetsManager.CreateTestDirectory();
        var command = new DotnetBuildCommand(Log, testAsset.Path)
            .WithEnvironmentVariable(
                "DOTNET_SDK_TEST_SCENARIO_BINLOG_DIRECTORY",
                diagnosticDirectory.Path);

        CommandResult result = command.Execute("-target:ScenarioTargetThatDoesNotExist");

        Assert.AreNotEqual(0, result.ExitCode, result.StdOut + result.StdErr);
        string binlogPath = Directory.GetFiles(diagnosticDirectory.Path, "*.binlog").Single();
        AssertReadableBinlog(binlogPath);
    }

    [TestMethod]
    public void ScenarioBinlogIsCopiedToHelixUploadRoot()
    {
        TestAsset testAsset = TestAssetsManager.CopyTestAsset("MSBuildTestApp").WithSource();
        TestDirectory diagnosticDirectory = TestAssetsManager.CreateTestDirectory(identifier: "diagnostics");
        TestDirectory uploadRoot = TestAssetsManager.CreateTestDirectory(identifier: "helix-upload");
        var command = new DotnetBuildCommand(Log, testAsset.Path)
            .WithEnvironmentVariable(
                "DOTNET_SDK_TEST_SCENARIO_BINLOG_DIRECTORY",
                diagnosticDirectory.Path)
            .WithEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT", uploadRoot.Path);

        CommandResult result = command.Execute();

        Assert.AreEqual(0, result.ExitCode, result.StdOut + result.StdErr);
        string uploadedPath = Directory.GetFiles(
            Path.Combine(uploadRoot.Path, "scenario-binlogs"),
            "*.binlog").Single();
        AssertReadableBinlog(uploadedPath);
    }

    [TestMethod]
    public void CaptureOptOutSkipsDiagnosticConfiguration()
    {
        TestAsset testAsset = TestAssetsManager.CopyTestAsset("MSBuildTestApp").WithSource();
        TestDirectory diagnosticDirectory = TestAssetsManager.CreateTestDirectory();
        var command = new DotnetBuildCommand(Log, testAsset.Path)
        {
            DisableScenarioBinlogCapture = true
        };
        command.WithEnvironmentVariable(
            "DOTNET_SDK_TEST_SCENARIO_BINLOG_DIRECTORY",
            Path.Combine(diagnosticDirectory.Path, "relative"));

        CommandResult result = command.Execute();

        Assert.AreEqual(0, result.ExitCode, result.StdOut + result.StdErr);
        Assert.IsFalse(Directory.Exists(Path.Combine(diagnosticDirectory.Path, "relative")));
    }

    [TestMethod]
    public void RelativeDiagnosticDirectoryFailsBeforeLaunchingBuild()
    {
        TestAsset testAsset = TestAssetsManager.CopyTestAsset("MSBuildTestApp").WithSource();
        var command = new DotnetBuildCommand(Log, testAsset.Path)
            .WithEnvironmentVariable(
                "DOTNET_SDK_TEST_SCENARIO_BINLOG_DIRECTORY",
                "relative-scenario-binlogs");

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => command.Execute());

        Assert.Contains("must be an absolute path", exception.Message);
    }

    private void AssertReadableBinlog(string path)
    {
        Assert.IsTrue(File.Exists(path), $"Binlog was not created: {path}");
        var replay = new BinaryLogReplayEventSource();
        bool receivedBuildEvent = false;
        replay.AnyEventRaised += (_, _) => receivedBuildEvent = true;
        replay.Replay(path);
        Assert.IsTrue(receivedBuildEvent, $"Binlog contained no build events: {path}");
    }
}
