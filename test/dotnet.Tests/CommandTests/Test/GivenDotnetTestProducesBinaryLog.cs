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
/// itself (project evaluation, device selection and deployment, plus the ComputeRunArguments target of
/// every test project), which lands in msbuild-dotnet-test.binlog next to the msbuild.binlog of the build.
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

        string binlogPath = GetTestBinaryLogPath(testInstance.Path);
        ShouldHoldASingleBuild(binlogPath);

        GetComputeRunArgumentsProjects(BinaryLog.ReadBuild(binlogPath)).Should().ContainSingle()
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

        string binlogPath = GetTestBinaryLogPath(testInstance.Path);
        ShouldHoldASingleBuild(binlogPath);

        var projects = GetComputeRunArgumentsProjects(BinaryLog.ReadBuild(binlogPath));

        projects.Select(project => Path.GetFileName(project.ProjectFile))
            .Should().BeEquivalentTo(["TestProject.csproj", "OtherTestProject.csproj"]);

        ShouldOwnItsTargets(projects);
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

        string binlogPath = GetTestBinaryLogPath(testInstance.Path);
        ShouldHoldASingleBuild(binlogPath);

        var projects = GetComputeRunArgumentsProjects(BinaryLog.ReadBuild(binlogPath));

        // One node per target framework. Under the old behavior the second build reused the first
        // build's ids, so the reader collapsed both onto a single node.
        projects.Should().HaveCount(2, "the project targets two frameworks, so it yields one test module per framework");

        ShouldOwnItsTargets(projects);
    }

    /// <summary>
    /// Regression test for https://github.com/dotnet/sdk/issues/55561: device selection used to run its
    /// own MSBuild builds (restore plus ComputeAvailableDevices) and each target framework of a device
    /// project used to get a build session of its own, so a device run wrote several builds with
    /// colliding BuildEventContext ids into the same binary log.
    /// </summary>
    [TestMethod]
    public void ItRecordsOneBuildForEveryTargetFrameworkOfADeviceProject()
    {
        TestAsset testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", Guid.NewGuid().ToString())
            .WithSource();

        // SingleDevice=true auto-selects one device per target framework, so the run goes through
        // device selection, a build per target framework, deployment and ComputeRunArguments.
        new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("-p:SingleDevice=true", "-bl")
            .Should().Pass();

        string binlogPath = GetTestBinaryLogPath(testInstance.Path);
        ShouldHoldASingleBuild(binlogPath);

        StructuredLoggerBuild build = BinaryLog.ReadBuild(binlogPath);

        var projects = GetComputeRunArgumentsProjects(build);
        projects.Should().HaveCount(2, "the project is deployed and run for each of its two target frameworks");
        ShouldOwnItsTargets(projects);

        build.FindChildrenRecursive<StructuredLoggerTarget>(target => target.Name == "ComputeAvailableDevices")
            .Should().NotBeEmpty("device selection should be part of the same build");
    }

    /// <summary>
    /// Regression test for https://github.com/dotnet/sdk/issues/55561: a solution containing device
    /// projects used to emit the device builds of every project on top of the build of the run itself.
    /// </summary>
    [TestMethod]
    public void ItRecordsOneBuildForASolutionOfDeviceProjects()
    {
        TestAsset testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", Guid.NewGuid().ToString())
            .WithSource();

        var project1Dir = Path.Combine(testInstance.Path, "Project1");
        var project2Dir = Path.Combine(testInstance.Path, "Project2");
        Directory.CreateDirectory(project1Dir);
        Directory.CreateDirectory(project2Dir);

        foreach (var dir in new[] { project1Dir, project2Dir })
        {
            File.Copy(Path.Combine(testInstance.Path, "Program.cs"), Path.Combine(dir, "Program.cs"));
            File.Copy(
                Path.Combine(testInstance.Path, "DotnetTestDevices.csproj"),
                Path.Combine(dir, Path.GetFileName(dir) + ".csproj"));
            File.Copy(
                Path.Combine(testInstance.Path, "DotnetTestDevices.Device.targets"),
                Path.Combine(dir, "DotnetTestDevices.Device.targets"));
        }

        File.Delete(Path.Combine(testInstance.Path, "DotnetTestDevices.csproj"));
        File.Delete(Path.Combine(testInstance.Path, "Program.cs"));

        File.WriteAllText(Path.Combine(testInstance.Path, "TestSolution.slnx"),
            """
            <Solution>
              <Project Path="Project1\Project1.csproj" />
              <Project Path="Project2\Project2.csproj" />
            </Solution>
            """);

        new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--solution", "TestSolution.slnx", "-p:SingleDevice=true", "-bl")
            .Should().Pass();

        string binlogPath = GetTestBinaryLogPath(testInstance.Path);
        ShouldHoldASingleBuild(binlogPath);

        var projects = GetComputeRunArgumentsProjects(BinaryLog.ReadBuild(binlogPath));
        projects.Should().HaveCount(4, "both projects are deployed and run for each of their two target frameworks");
        ShouldOwnItsTargets(projects);
    }

    private static string GetTestBinaryLogPath(string testInstancePath)
    {
        string binlogPath = Path.Combine(testInstancePath, "msbuild-dotnet-test.binlog");
        File.Exists(binlogPath).Should().BeTrue($"'{binlogPath}' should have been written by 'dotnet test -bl'");

        return binlogPath;
    }

    /// <summary>
    /// The invariant behind the fix: the test command must contribute exactly one MSBuild build.
    /// More than one means BuildEventContext ids restart mid-file, so readers attribute the projects
    /// and targets of the later builds to whichever project of the first build claimed the id, which
    /// is the corruption this test class guards against.
    /// </summary>
    private static void ShouldHoldASingleBuild(string binlogPath)
    {
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
    }

    private static void ShouldOwnItsTargets(IEnumerable<StructuredLoggerProject> projects)
    {
        // Each project must own its targets, instead of one project holding the targets of all of them.
        foreach (StructuredLoggerProject project in projects)
        {
            project.Children.OfType<StructuredLoggerTarget>()
                .Should().Contain(target => target.Name == "ComputeRunArguments",
                    $"'{project.ProjectFile}' should record the targets that ran for it");
        }
    }

    /// <summary>
    /// Returns the project nodes of the ComputeRunArguments requests the test command made, which is one
    /// per test module (project and target framework) of the run.
    /// </summary>
    private static List<StructuredLoggerProject> GetComputeRunArgumentsProjects(StructuredLoggerBuild build)
        => [.. build.FindChildrenRecursive<StructuredLoggerProject>(
            project => project.EntryTargets.Contains("ComputeRunArguments"))];
}
