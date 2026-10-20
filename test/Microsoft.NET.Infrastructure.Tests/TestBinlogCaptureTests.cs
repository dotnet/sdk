// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Infrastructure.Tests;

[TestClass]
public sealed class TestBinlogCaptureTests : SdkTest
{
    [TestMethod]
    public void DotnetBuildCapturesReadableTestBinlogWhenEnabled()
    {
        TestAsset testAsset = TestAssetsManager.CopyTestAsset("MSBuildTestApp").WithSource();
        TestDirectory diagnosticDirectory = TestAssetsManager.CreateTestDirectory();

        var command = new DotnetBuildCommand(Log, testAsset.Path)
            .WithEnvironmentVariable(
                "DOTNET_SDK_TEST_BINLOG_DIRECTORY",
                diagnosticDirectory.Path);

        CommandResult result = command.Execute();

        Assert.AreEqual(0, result.ExitCode, result.StdOut + result.StdErr);
        string binlogPath = Directory.GetFiles(diagnosticDirectory.Path, "*.binlog").Single();
        AssertReadableBinlog(binlogPath);
    }

    [TestMethod]
    public void MSBuildCommandCapturesReadableTestBinlogWhenEnabled()
    {
        TestAsset testAsset = TestAssetsManager.CopyTestAsset("MSBuildTestApp").WithSource();
        TestDirectory diagnosticDirectory = TestAssetsManager.CreateTestDirectory();
        var command = new MSBuildCommand(testAsset, "Build")
            .WithEnvironmentVariable(
                "DOTNET_SDK_TEST_BINLOG_DIRECTORY",
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
                "DOTNET_SDK_TEST_BINLOG_DIRECTORY",
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
    public void FailedBuildStillProducesReadableTestBinlog()
    {
        TestAsset testAsset = TestAssetsManager.CopyTestAsset("MSBuildTestApp").WithSource();
        TestDirectory diagnosticDirectory = TestAssetsManager.CreateTestDirectory();
        var command = new DotnetBuildCommand(Log, testAsset.Path)
            .WithEnvironmentVariable(
                "DOTNET_SDK_TEST_BINLOG_DIRECTORY",
                diagnosticDirectory.Path);

        CommandResult result = command.Execute("-target:TestTargetThatDoesNotExist");

        Assert.AreNotEqual(0, result.ExitCode, result.StdOut + result.StdErr);
        string binlogPath = Directory.GetFiles(diagnosticDirectory.Path, "*.binlog").Single();
        AssertReadableBinlog(binlogPath);
    }

    [TestMethod]
    public void TestBinlogRemainsLocalWhenHelixUploadRootIsConfigured()
    {
        TestAsset testAsset = TestAssetsManager.CopyTestAsset("MSBuildTestApp").WithSource();
        TestDirectory diagnosticDirectory = TestAssetsManager.CreateTestDirectory(identifier: "diagnostics");
        TestDirectory uploadRoot = TestAssetsManager.CreateTestDirectory(identifier: "helix-upload");
        var command = new DotnetBuildCommand(Log, testAsset.Path)
            .WithEnvironmentVariable(
                "DOTNET_SDK_TEST_BINLOG_DIRECTORY",
                diagnosticDirectory.Path)
            .WithEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT", uploadRoot.Path);

        CommandResult result = command.Execute();

        Assert.AreEqual(0, result.ExitCode, result.StdOut + result.StdErr);
        string retainedPath = Directory.GetFiles(diagnosticDirectory.Path, "*.binlog").Single();
        AssertReadableBinlog(retainedPath);
        Assert.IsEmpty(Directory.GetFiles(uploadRoot.Path, "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    [DataRow(8)]
    [DataRow(190)]
    public void AuthoredBinlogAndImportsKeepMatchingCompactNamesWhenArchived(int basenameLength)
    {
        TestDirectory projectDirectory = TestAssetsManager.CreateTestDirectory(identifier: $"project-{basenameLength}");
        TestDirectory diagnosticDirectory = TestAssetsManager.CreateTestDirectory(identifier: $"diagnostics-{basenameLength}");
        TestDirectory uploadRoot = TestAssetsManager.CreateTestDirectory(identifier: $"upload-{basenameLength}");
        File.WriteAllText(
            Path.Combine(projectDirectory.Path, "imported.targets"),
            """
            <Project>
              <Target Name="Build">
                <Message Text="TEST-IMPORTED-CONTENT" Importance="high" />
              </Target>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(projectDirectory.Path, "fixture.proj"),
            """<Project><Import Project="imported.targets" /></Project>""");
        string authoredPath = Path.Combine(projectDirectory.Path, new string('a', basenameLength) + ".binlog");
        var command = new MSBuildCommand(Log, "Build", projectDirectory.Path)
        {
            ShouldRestore = false,
            WorkingDirectory = projectDirectory.Path
        };
        command.WithEnvironmentVariable("DOTNET_SDK_TEST_BINLOG_DIRECTORY", diagnosticDirectory.Path);
        command.WithEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT", uploadRoot.Path);

        for (int attempt = 0; attempt < 2; attempt++)
        {
            CommandResult result = command.Execute($"/bl:{authoredPath};ProjectImports=ZipFile");
            Assert.AreEqual(0, result.ExitCode, result.StdOut + result.StdErr);
        }

        AssertReadableBinlog(authoredPath);
        AssertImportedContent(Path.ChangeExtension(authoredPath, ".ProjectImports.zip"));
        string archiveDirectory = Path.Combine(diagnosticDirectory.Path, "authored");
        Assert.HasCount(4, Directory.GetFiles(archiveDirectory));
        Assert.IsEmpty(Directory.GetFiles(uploadRoot.Path, "*", SearchOption.AllDirectories));
        foreach (string archivedBinlog in Directory.GetFiles(archiveDirectory, "*.binlog"))
        {
            Assert.AreEqual(32, Path.GetFileNameWithoutExtension(archivedBinlog).Length);
            AssertReadableBinlog(archivedBinlog);
            string archivedImports = Path.ChangeExtension(archivedBinlog, ".ProjectImports.zip");
            AssertImportedContent(archivedImports);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AncestorResponseFileSkipsCaptureWithoutOverridingItsLogger(bool useProjectWorkingDirectory)
    {
        TestDirectory root = TestAssetsManager.CreateTestDirectory(identifier: useProjectWorkingDirectory.ToString());
        string projectParent = Path.Combine(root.Path, "project-parent");
        string projectDirectory = Path.Combine(projectParent, "nested");
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(
            Path.Combine(projectParent, "Directory.Build.rsp"),
            "/bl:ancestor.binlog;ProjectImports=None");
        File.WriteAllText(
            Path.Combine(projectDirectory, "fixture.proj"),
            """<Project><Target Name="Build" /></Project>""");
        string diagnosticDirectory = Path.Combine(root.Path, "diagnostics");
        string uploadRoot = Path.Combine(root.Path, "upload");
        var logger = new StringTestLogger();
        var command = new MSBuildCommand(logger, "Build", projectDirectory)
        {
            ShouldRestore = false,
            WorkingDirectory = useProjectWorkingDirectory ? projectDirectory : root.Path
        };
        command.WithEnvironmentVariable("DOTNET_SDK_TEST_BINLOG_DIRECTORY", diagnosticDirectory);
        command.WithEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT", uploadRoot);

        CommandResult result = command.Execute();

        Assert.AreEqual(0, result.ExitCode, result.StdOut + result.StdErr);
        Assert.Contains("Test binlog capture skipped", logger.ToString());
        AssertReadableBinlog(Path.Combine(command.WorkingDirectory, "ancestor.binlog"));
        Assert.IsEmpty(Directory.GetFiles(diagnosticDirectory, "*", SearchOption.AllDirectories));
        Assert.IsFalse(Directory.Exists(uploadRoot));
    }

    [TestMethod]
    public void CaptureOptOutSkipsDiagnosticConfiguration()
    {
        TestAsset testAsset = TestAssetsManager.CopyTestAsset("MSBuildTestApp").WithSource();
        var command = new DotnetBuildCommand(Log, testAsset.Path)
        {
            DisableTestBinlogCapture = true
        };
        command.WithEnvironmentVariable(
            "DOTNET_SDK_TEST_BINLOG_DIRECTORY",
            "relative-test-binlogs");

        CommandResult result = command.Execute();

        Assert.AreEqual(0, result.ExitCode, result.StdOut + result.StdErr);
        Assert.IsEmpty(Directory.GetFiles(testAsset.Path, "*.binlog"));
    }

    [TestMethod]
    public void CommandEnvironmentRemovalBypassesInvalidCaptureConfiguration()
    {
        TestAsset testAsset = TestAssetsManager.CopyTestAsset("MSBuildTestApp").WithSource();
        var command = new DotnetBuildCommand(Log, testAsset.Path)
            .WithEnvironmentVariable(
                "DOTNET_SDK_TEST_BINLOG_DIRECTORY",
                "relative-test-binlogs");
        command.EnvironmentToRemove.Add("DOTNET_SDK_TEST_BINLOG_DIRECTORY");

        CommandResult result = command.Execute();

        Assert.AreEqual(0, result.ExitCode, result.StdOut + result.StdErr);
        Assert.IsFalse(Directory.Exists(Path.Combine(testAsset.Path, "relative-test-binlogs")));
        Assert.IsEmpty(Directory.GetFiles(testAsset.Path, "*.binlog"));
    }

    [TestMethod]
    public void RetryRetainsSeparateBinlogsForFailedAndSuccessfulAttempts()
    {
        TestDirectory projectDirectory = TestAssetsManager.CreateTestDirectory(identifier: "project");
        TestDirectory diagnosticDirectory = TestAssetsManager.CreateTestDirectory(identifier: "diagnostics");
        string attemptsPath = Path.Combine(projectDirectory.Path, "attempts.txt");
        File.WriteAllText(
            Path.Combine(projectDirectory.Path, "fixture.proj"),
            """
            <Project>
              <Target Name="Build">
                <PropertyGroup>
                  <FirstAttempt Condition="!Exists('attempts.txt')">true</FirstAttempt>
                </PropertyGroup>
                <WriteLinesToFile File="attempts.txt" Lines="attempt" Overwrite="false" />
                <Error Condition="'$(FirstAttempt)' == 'true'" Text="Controlled first-attempt failure" />
              </Target>
            </Project>
            """);
        var command = new MSBuildCommand(Log, "Build", projectDirectory.Path)
        {
            ShouldRestore = false,
            WorkingDirectory = projectDirectory.Path
        };
        command.WithEnvironmentVariable("DOTNET_SDK_TEST_BINLOG_DIRECTORY", diagnosticDirectory.Path);
        command.WithRetryOnExitCode(1);

        CommandResult result = command.Execute();

        Assert.AreEqual(0, result.ExitCode, result.StdOut + result.StdErr);
        Assert.HasCount(2, File.ReadAllLines(attemptsPath));
        string[] binlogs = Directory.GetFiles(diagnosticDirectory.Path, "*.binlog");
        Assert.HasCount(2, binlogs);
        var outcomes = new List<bool>();
        foreach (string binlog in binlogs)
        {
            var replay = new BinaryLogReplayEventSource();
            replay.BuildFinished += (_, buildFinished) => outcomes.Add(buildFinished.Succeeded);
            replay.Replay(binlog, TestContext.CancellationToken);
        }
        Assert.AreSequenceEqual(new[] { false, true }, outcomes.OrderBy(succeeded => succeeded));
    }

    [TestMethod]
    public void RelativeDiagnosticDirectoryFailsBeforeLaunchingBuild()
    {
        TestAsset testAsset = TestAssetsManager.CopyTestAsset("MSBuildTestApp").WithSource();
        var command = new DotnetBuildCommand(Log, testAsset.Path)
            .WithEnvironmentVariable(
                "DOTNET_SDK_TEST_BINLOG_DIRECTORY",
                "relative-test-binlogs");

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

    private static void AssertImportedContent(string path)
    {
        Assert.IsTrue(File.Exists(path), $"Project-import archive was not retained: {path}");
        using ZipArchive archive = ZipFile.OpenRead(path);
        bool foundContent = archive.Entries.Any(entry =>
        {
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd().Contains("TEST-IMPORTED-CONTENT", StringComparison.Ordinal);
        });
        Assert.IsTrue(foundContent, $"Project-import archive omitted the imported targets: {path}");
    }
}
