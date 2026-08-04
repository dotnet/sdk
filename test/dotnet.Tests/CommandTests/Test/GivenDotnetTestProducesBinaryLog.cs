// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;
using StructuredLoggerBuild = Microsoft.Build.Logging.StructuredLogger.Build;
using StructuredLoggerProject = Microsoft.Build.Logging.StructuredLogger.Project;
using StructuredLoggerTarget = Microsoft.Build.Logging.StructuredLogger.Target;

namespace Microsoft.DotNet.Cli.Test.Tests;

/// <summary>
/// Covers the binary log that `dotnet test -bl` writes for the MSBuild work the test command drives
/// itself (project evaluation plus the ComputeRunArguments target of every test project), which lands
/// in msbuild-dotnet-test.binlog next to the msbuild.binlog of the build.
/// </summary>
[TestClass]
public class GivenDotnetTestProducesBinaryLog : SdkTest
{
    public GivenDotnetTestProducesBinaryLog()
    {
    }

    [TestMethod]
    public void ItRecordsTheTargetsItRunsForASingleTestProject()
    {
        TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString())
            .WithSource();

        new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("-bl")
            .Should().Pass();

        StructuredLoggerBuild build = ReadTestBinaryLog(testInstance.Path);

        GetComputeRunArgumentsProjects(build).Should().ContainSingle()
            .Which.ProjectFile.Should().EndWith("TestProject.csproj");
    }
    /// <summary>
    /// Regression test for https://github.com/dotnet/sdk/issues/49386: every test project used to run
    /// ComputeRunArguments in its own MSBuild build, so all those builds shared the same
    /// BuildEventContext ids inside a single binary log and readers attributed every project's targets
    /// to the first project of the run.
    /// </summary>
    [TestMethod]
    public void ItRecordsOneBuildWithTheTargetsOfEveryTestProjectOfASolution()
    {
        TestAsset testInstance = TestAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
            .WithSource();

        // The asset deliberately contains a failing test, so the run reports test failures rather
        // than succeeding. Asserting the exact exit code keeps the binlog assertions below from
        // being reached with a partial log when the build itself breaks.
        new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("-bl")
            .ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);

        StructuredLoggerBuild build = ReadTestBinaryLog(testInstance.Path);

        var projects = GetComputeRunArgumentsProjects(build);

        projects.Select(project => Path.GetFileName(project.ProjectFile))
            .Should().BeEquivalentTo(["TestProject.csproj", "OtherTestProject.csproj"]);

        // Each project must own its targets, instead of one project holding the targets of both.
        foreach (StructuredLoggerProject project in projects)
        {
            project.Children.OfType<StructuredLoggerTarget>()
                .Should().Contain(target => target.Name == "ComputeRunArguments",
                    $"'{project.ProjectFile}' should record the targets that ran for it");
        }
    }

    /// <summary>
    /// A multi-target-framework project produces one test module per target framework, which is the
    /// same fan-out across MSBuild builds that caused colliding ids across projects.
    /// </summary>
    [TestMethod]
    public void ItRecordsOneBuildWithTheTargetsOfEveryTargetFrameworkOfAProject()
    {
        TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithMultipleTFMsParallelization", Guid.NewGuid().ToString())
            .WithSource();
        testInstance.WithTargetFrameworks($"{DotnetVersionHelper.GetPreviousDotnetVersion()};{ToolsetInfo.CurrentTargetFramework}", "TestProject");

        new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--property", "TestTfmsInParallel=false", "-bl")
            .ExitCode.Should().Be(ExitCodes.Success);

        StructuredLoggerBuild build = ReadTestBinaryLog(testInstance.Path);

        var projects = GetComputeRunArgumentsProjects(build);

        // One node per target framework. Under the old behavior the second build reused the first
        // build's ids, so the reader collapsed both onto a single node.
        projects.Should().HaveCount(2, "the project targets two frameworks, so it yields one test module per framework");

        foreach (StructuredLoggerProject project in projects)
        {
            project.Children.OfType<StructuredLoggerTarget>()
                .Should().Contain(target => target.Name == "ComputeRunArguments",
                    $"'{project.ProjectFile}' should record the targets that ran for it");
        }
    }

    private static StructuredLoggerBuild ReadTestBinaryLog(string testInstancePath)
    {
        string binlogPath = Path.Combine(testInstancePath, "msbuild-dotnet-test.binlog");
        File.Exists(binlogPath).Should().BeTrue($"'{binlogPath}' should have been written by 'dotnet test -bl'");

        // The invariant behind the fix: the test command must contribute exactly one MSBuild build.
        // More than one means BuildEventContext ids restart mid-file, which is the corruption this
        // test class guards against.
        int buildStarted = 0;
        int buildFinished = 0;
        foreach (var record in BinaryLog.ReadRecords(binlogPath))
        {
            switch (record.Args)
            {
                case BuildStartedEventArgs:
                    buildStarted++;
                    break;
                case BuildFinishedEventArgs:
                    buildFinished++;
                    break;
            }
        }

        buildStarted.Should().Be(1, "'dotnet test -bl' must record exactly one build, otherwise BuildEventContext ids collide");
        buildFinished.Should().Be(1, "the single recorded build must be closed");

        return BinaryLog.ReadBuild(binlogPath);
    }

    /// <summary>
    /// Returns the project nodes of the ComputeRunArguments requests the test command made, which is one
    /// per test module (project and target framework) of the run.
    /// </summary>
    private static List<StructuredLoggerProject> GetComputeRunArgumentsProjects(StructuredLoggerBuild build)
        => [.. build.FindChildrenRecursive<StructuredLoggerProject>(
            project => project.EntryTargets.Contains("ComputeRunArguments"))];
}
