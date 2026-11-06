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

    [TestMethod]
    [DataRow(new string[0], "0", "0", false, false)]
    [DataRow(new[] { "-mt" }, "0", "0", true, false)]
    [DataRow(new[] { "-mt:false" }, "0", "0", false, false)]
    [DataRow(new[] { "--multiThreaded:TRUE" }, "0", "0", true, false)]
    [DataRow(new[] { "/MT:false" }, "0", "0", false, false)]
    [DataRow(new[] { "-mt:\"true\"" }, "0", "0", true, false)]
    [DataRow(new[] { "-mt:false", "-mt" }, "0", "0", true, false)]
    [DataRow(new[] { "-mt", "-mt:false" }, "0", "0", false, false)]
    [DataRow(new[] { "-mt:false", "-mt:" }, "0", "0", false, false)]
    [DataRow(new[] { "-mt:false", "-mt:\"\"" }, "0", "0", false, false)]
    [DataRow(new string[0], "0", "1", true, false)]
    [DataRow(new[] { "-mt:false" }, "0", "1", false, false)]
    [DataRow(new[] { "-mt:false" }, "1", "0", true, false)]
    [DataRow(new[] { "-mt", "--project", "DotnetTestDevices.csproj" }, "0", "0", true, false)]
    [DataRow(new[] { "-mt" }, "0", "0", true, true)]
    [DataRow(new[] { "-mt:false" }, "0", "1", false, true)]
    public void ItPassesMultiThreadingToPreparation(
        string[] arguments, string force, string enable, bool multiThreaded, bool noBuild)
    {
        TestAsset testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", Guid.NewGuid().ToString())
            .WithSource();

        if (noBuild)
        {
            new DotnetTestCommand(Log, disableNewOutput: false)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "-p:SingleDevice=true", "--no-progress")
                .Should().Pass();
        }

        string[] preparationTargets = ["Restore", "ComputeAvailableDevices", "DeployToDevice", "ComputeRunArguments"];
        string projectPath = Path.Combine(testInstance.Path, "DotnetTestDevices.csproj");
        string projectContents = File.ReadAllText(projectPath);
        foreach (string target in preparationTargets)
        {
            projectContents = projectContents.Replace("</Project>", $"""
              <Target Name="Record{target}Directory" BeforeTargets="{target}">
                <WriteLinesToFile File="$(MSBuildProjectDirectory)/{target}.cwd"
                                  Lines="$([System.IO.Directory]::GetCurrentDirectory())" Overwrite="true" />
              </Target>
            </Project>
            """);
        }
        File.WriteAllText(projectPath, projectContents);

        List<string> args = ["--framework", ToolsetInfo.CurrentTargetFramework, "-p:SingleDevice=true", "-bl", "--no-progress", .. arguments];
        if (noBuild)
        {
            args.Add("--no-build");
        }

        new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .WithEnvironmentVariable("MSBUILDFORCEMULTITHREADED", force)
            .WithEnvironmentVariable("MSBUILDENABLEMULTITHREADED", enable)
            .WithEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", "")
            .WithEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS", "1")
            .WithEnvironmentVariable("MSBUILDUSESERVER", "0")
            .Execute(args.ToArray())
            .Should().Pass()
            .And.HaveStdOutContaining("succeeded: 1");

        string binlogPath = GetTestBinaryLogPath(testInstance.Path);
        ShouldHoldASingleBuild(binlogPath);
        ShouldUseMultiThreading(binlogPath, multiThreaded);

        string[] expectedTargets = noBuild
            ? ["ComputeAvailableDevices", "DeployToDevice", "ComputeRunArguments"]
            : preparationTargets;
        BinaryLog.ReadRecords(binlogPath).Select(record => record.Args)
            .OfType<TargetStartedEventArgs>().Select(target => target.TargetName).ToArray()
            .Should().Contain(expectedTargets);
        foreach (string target in expectedTargets)
        {
            string directory = File.ReadAllText(Path.Combine(testInstance.Path, target + ".cwd")).Trim();
            Path.GetFileName(directory).Should().Be(
                multiThreaded ? "MT-sentinel-CWD" : Path.GetFileName(testInstance.Path), target);
        }
        if (!noBuild)
        {
            File.Exists(Path.Combine(testInstance.Path, "msbuild.binlog")).Should().BeTrue();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ItPassesMultiThreadingToDeviceListing(bool multiThreaded)
    {
        TestAsset testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", Guid.NewGuid().ToString())
            .WithSource();

        new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .WithEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "0")
            .WithEnvironmentVariable("MSBUILDENABLEMULTITHREADED", "1")
            .WithEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", "")
            .Execute("--list-devices", "--framework", ToolsetInfo.CurrentTargetFramework, "-bl", $"-mt:{multiThreaded}")
            .Should().Pass()
            .And.HaveStdOutContaining("test-device-1");

        string binlogPath = GetTestBinaryLogPath(testInstance.Path);
        ShouldHoldASingleBuild(binlogPath);
        ShouldUseMultiThreading(binlogPath, multiThreaded);
        BinaryLog.ReadRecords(binlogPath).Select(record => record.Args)
            .OfType<TargetStartedEventArgs>().Select(target => target.TargetName).ToArray()
            .Should().Contain(["Restore", "ComputeAvailableDevices"])
            .And.NotContain(["DeployToDevice", "ComputeRunArguments"]);
    }

    [TestMethod]
    public void ItRunsMultiThreadedPreparationWithoutABinaryLog()
    {
        TestAsset testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", Guid.NewGuid().ToString())
            .WithSource();

        new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .WithEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "0")
            .WithEnvironmentVariable("MSBUILDENABLEMULTITHREADED", "0")
            .WithEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", "")
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "-p:SingleDevice=true", "-mt", "--no-progress")
            .Should().Pass()
            .And.HaveStdOutContaining("succeeded: 1");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void ItKeepsTraceLoggingInTheInvocationDirectory(bool multiThreaded, bool absoluteTracePath)
    {
        TestAsset testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", Guid.NewGuid().ToString())
            .WithSource();
        string relativeTracePath = Path.Combine("trace-output", "trace.log");
        string tracePath = Path.Combine(testInstance.Path, relativeTracePath);

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .WithEnvironmentVariable("DOTNET_CLI_TEST_TRACEFILE", absoluteTracePath ? tracePath : relativeTracePath)
            .WithEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "0")
            .WithEnvironmentVariable("MSBUILDENABLEMULTITHREADED", "0")
            .WithEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", "")
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "-p:SingleDevice=true", "--no-progress", $"-mt:{multiThreaded}");

        result.Should().Pass().And.HaveStdOutContaining("succeeded: 1");
        result.StdOut.Should().NotContain("MSB4287").And.NotContain(nameof(DirectoryNotFoundException));
        result.StdErr.Should().NotContain("MSB4287").And.NotContain(nameof(DirectoryNotFoundException));
        File.Exists(tracePath).Should().BeTrue();
        File.ReadAllText(tracePath).Should().Contain("Loaded project").And.Contain("Starting test process");
    }

    [TestMethod]
    [DataRow(false, "Restore")]
    [DataRow(true, "Restore")]
    [DataRow(false, "CoreCompile")]
    [DataRow(true, "CoreCompile")]
    public void ItReportsBuildFailuresAfterDeviceSelection(bool multiThreaded, string failingTarget)
    {
        TestAsset testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", Guid.NewGuid().ToString())
            .WithSource();
        string projectPath = Path.Combine(testInstance.Path, "DotnetTestDevices.csproj");
        File.WriteAllText(projectPath, File.ReadAllText(projectPath).Replace("</Project>", $"""
              <Target Name="MarkDeviceSelection" AfterTargets="ComputeAvailableDevices">
                <Touch Files="$(MSBuildProjectDirectory)/device-selected.marker" AlwaysCreate="true" />
              </Target>
              <Target Name="FailSelectedBuild" BeforeTargets="{failingTarget}"
                      Condition="Exists('$(MSBuildProjectDirectory)/device-selected.marker')">
                <Error Text="Intentional {failingTarget} failure after device selection." />
              </Target>
            </Project>
            """));

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .WithEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "0")
            .WithEnvironmentVariable("MSBUILDENABLEMULTITHREADED", "0")
            .WithEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", "")
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "-p:SingleDevice=true", "-bl", $"-mt:{multiThreaded}");

        result.ExitCode.Should().Be(1);
        result.StdOut.Should().Contain($"Intentional {failingTarget} failure after device selection.");
        result.StdErr.Should().NotContain("Unhandled exception");

        string binlogPath = GetTestBinaryLogPath(testInstance.Path);
        ShouldHoldASingleBuild(binlogPath);
        ShouldUseMultiThreading(binlogPath, multiThreaded);
        GetComputeRunArgumentsProjects(BinaryLog.ReadBuild(binlogPath)).Should().BeEmpty();
    }

    /// <summary>
    /// Regression test for https://github.com/dotnet/sdk/issues/49386: every test project used to run
    /// ComputeRunArguments in its own MSBuild build, so all those builds shared the same
    /// BuildEventContext ids inside a single binary log and readers attributed every project's targets
    /// to the first project of the run.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ItRecordsOneBuildWithTheTargetsOfEveryTestProjectOfASolution(bool multiThreaded)
    {
        TestAsset testInstance = TestAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
            .WithSource();

        // The asset deliberately contains a failing test, so the run reports test failures rather
        // than succeeding. Asserting the exact exit code keeps the binlog assertions below from
        // being reached with a partial log when the build itself breaks.
        new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .WithEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "0")
            .WithEnvironmentVariable("MSBUILDENABLEMULTITHREADED", "0")
            .WithEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", "")
            .Execute("-bl", $"-mt:{multiThreaded}")
            .ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);

        string binlogPath = GetTestBinaryLogPath(testInstance.Path);
        ShouldHoldASingleBuild(binlogPath);
        ShouldUseMultiThreading(binlogPath, multiThreaded);

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
            .WithEnvironmentVariable("DOTNET_TEST_PARALLELIZATION_DIRECTORY", testInstance.Path)
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
    [DataRow(false)]
    [DataRow(true)]
    public void ItRecordsOneBuildForEveryTargetFrameworkOfADeviceProject(bool multiThreaded)
    {
        TestAsset testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", Guid.NewGuid().ToString())
            .WithSource();

        // SingleDevice=true auto-selects one device per target framework, so the run goes through
        // device selection, a build per target framework, deployment and ComputeRunArguments.
        new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .WithEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "0")
            .WithEnvironmentVariable("MSBUILDENABLEMULTITHREADED", "0")
            .WithEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", "")
            .Execute("-p:SingleDevice=true", "-bl", $"-mt:{multiThreaded}")
            .Should().Pass();

        string binlogPath = GetTestBinaryLogPath(testInstance.Path);
        ShouldHoldASingleBuild(binlogPath);
        ShouldUseMultiThreading(binlogPath, multiThreaded);

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
    [DataRow(false)]
    [DataRow(true)]
    public void ItRecordsOneBuildForASolutionOfDeviceProjects(bool multiThreaded)
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
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .WithEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "0")
            .WithEnvironmentVariable("MSBUILDENABLEMULTITHREADED", "0")
            .WithEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", "")
            .Execute("--solution", "TestSolution.slnx", "-p:SingleDevice=true", "-bl", $"-mt:{multiThreaded}")
            .Should().Pass();

        string binlogPath = GetTestBinaryLogPath(testInstance.Path);
        ShouldHoldASingleBuild(binlogPath);
        ShouldUseMultiThreading(binlogPath, multiThreaded);

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

    private static void ShouldUseMultiThreading(string binlogPath, bool multiThreaded)
        => BinaryLog.ReadRecords(binlogPath).Select(record => record.Args)
            .OfType<BuildMessageEventArgs>()
            .Count(message => message.Message?.StartsWith("Multi-threaded strict mode is enabled.", StringComparison.Ordinal) == true)
            .Should().Be(multiThreaded ? 1 : 0);

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
