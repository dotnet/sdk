// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.Watch.UnitTests;

public class BuildProjects(ITestOutputHelper output)
{
    private class TestContext : IDisposable
    {
        public readonly HotReloadDotNetWatcher Watcher;
        public readonly FileWatcher FileWatcher;
        public readonly TestConsole Console;
        public readonly TestLogger BuildLogger;

        public readonly List<string> BuildInvocations = [];
        public string? SolutionFile;

        public TestContext(ITestOutputHelper output, ImmutableArray<ProjectRepresentation> rootProjects)
        {
            var environmentOptions = TestOptions.GetEnvironmentOptions();
            var processOutputReporter = new TestProcessOutputReporter();

            var processRunner = new TestProcessRunner()
            {
                RunImpl = (processSpec, _, _) =>
                {
                    LogBuildInvocation(processSpec);
                    return 0;
                }
            };

            BuildLogger = new TestLogger(output);

            var context = new DotNetWatchContext()
            {
                ProcessOutputReporter = processOutputReporter,
                LoggerFactory = NullLoggerFactory.Instance,
                Logger = NullLogger.Instance,
                BuildLogger = BuildLogger,
                ProcessRunner = processRunner,
                Options = new(),
                MainProjectOptions = null,
                RootProjects = rootProjects,
                BuildArguments = ["-p", "A=1"],
                EnvironmentOptions = environmentOptions,
                BrowserLauncher = new BrowserLauncher(NullLogger.Instance, processOutputReporter, environmentOptions),
                BrowserRefreshServerFactory = new BrowserRefreshServerFactory()
            };

            FileWatcher = new FileWatcher(NullLogger.Instance, environmentOptions);

            Console = new TestConsole(output);
            Watcher = new HotReloadDotNetWatcher(context, Console, runtimeProcessLauncherFactory: null, selectionPrompt: null);
        }

        public void LogBuildInvocation(ProcessSpec processSpec)
        {
            SolutionFile = processSpec.Arguments.FirstOrDefault(a => a.EndsWith(".slnx"));

            // Replace path to solution, which is a temp path, with placeholder to make assertions easier.
            BuildInvocations.Add(string.Join(" ", processSpec.Arguments.Select(a => a == SolutionFile ? "<solution>" : a)));
        }

        public void Dispose()
        {
            Assert.False(File.Exists(SolutionFile));
        }
    }

    private TestContext CreateContext(string[]? rootProjects = null)
        => new(output, rootProjects?.Select(ProjectRepresentation.FromProjectOrEntryPointFilePath).ToImmutableArray() ?? []);

    [Fact]
    public async Task SingleProject_NotMain()
    {
        var dir = TestAssetsManager.CreateTestDirectory();
        var project1 = Path.Combine(dir.Path, "Project1.csproj");

        using var context = CreateContext();

        var result = await context.Watcher.BuildProjectsAsync(
            [new ProjectRepresentation(project1, entryPointFilePath: null)],
            context.FileWatcher,
            mainProjectOptions: null,
            frameworkSelector: (_, _) =>
            {
                Assert.Fail("Selector should not be invoked");
                return ValueTask.FromResult("n/a");
            },
            deviceSelector: null,
            CancellationToken.None);

        Assert.True(result.Success);

        AssertEx.SequenceEqual([$"build {project1} -p A=1"], context.BuildInvocations);
    }

    [Fact]
    public async Task SingleProject_Main()
    {
        var dir = TestAssetsManager.CreateTestDirectory();
        var project1 = Path.Combine(dir.Path, "Project1.csproj");

        File.WriteAllText(project1, $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net9.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """);

        using var context = CreateContext([project1]);

        var result = await context.Watcher.BuildProjectsAsync(
            [new ProjectRepresentation(project1, entryPointFilePath: null)],
            context.FileWatcher,
            mainProjectOptions: TestOptions.ProjectOptions,
            frameworkSelector: (_, _) =>
            {
                Assert.Fail("Selector should not be invoked");
                return ValueTask.FromResult("n/a");
            },
            deviceSelector: null,
            CancellationToken.None);

        Assert.True(result.Success);

        AssertEx.SequenceEqual(
        [
            $"restore {project1} -p A=1 -consoleLoggerParameters:NoSummary",
            $"build {project1} -p A=1 --framework net9.0 --no-restore"
        ], context.BuildInvocations);
    }

    [Fact]
    public async Task MultipleProjects()
    {
        var dir = TestAssetsManager.CreateTestDirectory();
        var project1 = Path.Combine(dir.Path, "Project1.csproj");
        var project2 = Path.Combine(dir.Path, "Project2.csproj");

        using var context = CreateContext();

        var result = await context.Watcher.BuildProjectsAsync(
            projects:
            [
                new ProjectRepresentation(project1, entryPointFilePath: null),
                new ProjectRepresentation(project2, entryPointFilePath: null)
            ],
            context.FileWatcher,
            mainProjectOptions: null,
            frameworkSelector: null,
            deviceSelector: null,
            CancellationToken.None);

        Assert.True(result.Success);

        AssertEx.SequenceEqual(["build <solution> -p A=1"], context.BuildInvocations);
    }

    [Theory]
    [CombinatorialData]
    public async Task FileBasedApp_NoFrameworkProperties(bool isMain)
    {
        var dir = TestAssetsManager.CreateTestDirectory(identifiers: [isMain]);
        var file1 = Path.Combine(dir.Path, "File1.cs");
        File.WriteAllText(file1, """
            Console.WriteLine(1);
            """);

        using var context = CreateContext([file1]);

        var result = await context.Watcher.BuildProjectsAsync(
            [new ProjectRepresentation(projectPath: null, entryPointFilePath: file1)],
            context.FileWatcher,
            mainProjectOptions: isMain ? TestOptions.GetProjectOptions(["--file", file1]) : null,
            frameworkSelector: (_, _) =>
            {
                Assert.Fail("Selector should not be invoked");
                return ValueTask.FromResult("n/a");
            },
            deviceSelector: null,
            CancellationToken.None);

        Assert.True(result.Success);

        AssertEx.SequenceEqual([$"build {file1} -p A=1"], context.BuildInvocations);
    }

    [Theory]
    [CombinatorialData]
    public async Task FileBasedApp_TargetFrameworkProperty(bool nonInteractive)
    {
        var dir = TestAssetsManager.CreateTestDirectory(identifiers: [nonInteractive]);
        var file1 = Path.Combine(dir.Path, "File1.cs");
        File.WriteAllText(file1, """
            #:property TargetFramework=   net9.0    
            Console.WriteLine(1);
            """);

        using var context = CreateContext([file1]);

        var result = await context.Watcher.BuildProjectsAsync(
            [new ProjectRepresentation(projectPath: null, entryPointFilePath: file1)],
            context.FileWatcher,
            mainProjectOptions: TestOptions.GetProjectOptions(["--file", file1]),
            frameworkSelector: nonInteractive ? null : (_, _) =>
            {
                Assert.Fail("Selector should not be invoked");
                return ValueTask.FromResult("n/a");
            },
            deviceSelector: null,
            CancellationToken.None);

        Assert.True(result.Success);

        AssertEx.SequenceEqual([$"build {file1} -p A=1 --framework net9.0"], context.BuildInvocations);
    }

    [Theory]
    [CombinatorialData]
    public async Task FileBasedApp_TargetFrameworksProperty(bool nonInteractive)
    {
        var dir = TestAssetsManager.CreateTestDirectory(identifiers: [nonInteractive]);
        var file1 = Path.Combine(dir.Path, "File1.cs");
        File.WriteAllText(file1, """
            #:property TargetFrameworks=net9.0;net10.0
            Console.WriteLine(1);
            """);

        using var context = CreateContext([file1]);

        var result = await context.Watcher.BuildProjectsAsync(
            [new ProjectRepresentation(projectPath: null, entryPointFilePath: file1)],
            context.FileWatcher,
            mainProjectOptions: TestOptions.GetProjectOptions(["--file", file1]),
            frameworkSelector: nonInteractive ? null : (_, _) =>
            {
                return ValueTask.FromResult("net9.0");
            },
            deviceSelector: null,
            CancellationToken.None);

        if (nonInteractive)
        {
            AssertEx.SequenceEqual(
            [
                "[Error] " + MessageDescriptor.FileSpecifiesMultipleTargetFrameworks.GetMessage((file1, "net9.0', 'net10.0"))
            ], context.BuildLogger.GetAndClearMessages());

            Assert.False(result.Success);
            Assert.Empty(context.BuildInvocations);
        }
        else
        {
            Assert.True(result.Success);
            AssertEx.SequenceEqual([$"build {file1} -p A=1 --framework net9.0"], context.BuildInvocations);
        }
    }

    [Fact]
    public async Task FileBasedApp_TargetFrameworkOption()
    {
        var dir = TestAssetsManager.CreateTestDirectory();
        var file1 = Path.Combine(dir.Path, "File1.cs");
        File.WriteAllText(file1, """
            #:property TargetFrameworks=net9.0;net10.0
            Console.WriteLine(1);
            """);

        using var context = CreateContext([file1]);

        var result = await context.Watcher.BuildProjectsAsync(
            [new ProjectRepresentation(projectPath: null, entryPointFilePath: file1)],
            context.FileWatcher,
            mainProjectOptions: TestOptions.GetProjectOptions(["--file", file1, "-f", "net8.0"]),
            frameworkSelector: (_, _) =>
            {
                Assert.Fail("Selector should not be invoked");
                return ValueTask.FromResult("n/a");
            },
            deviceSelector: null,
            CancellationToken.None);

        Assert.True(result.Success);

        AssertEx.SequenceEqual([$"build {file1} -p A=1 --framework net8.0"], context.BuildInvocations);
    }

    [Fact]
    public async Task MultipleFiles()
    {
        var dir = TestAssetsManager.CreateTestDirectory();
        var file1 = Path.Combine(dir.Path, "File1.cs");
        var file2 = Path.Combine(dir.Path, "File2.cs");

        using var context = CreateContext();

        var result = await context.Watcher.BuildProjectsAsync(
            projects:
            [
                new ProjectRepresentation(projectPath: null, entryPointFilePath: file1),
                new ProjectRepresentation(projectPath: null, entryPointFilePath: file2)
            ],
            context.FileWatcher,
            mainProjectOptions: null,
            frameworkSelector: null,
            deviceSelector: null,
            CancellationToken.None);

        Assert.True(result.Success);

        AssertEx.SequenceEqual(
        [
            $"build {file1} -p A=1",
            $"build {file2} -p A=1"
        ], context.BuildInvocations);
    }

    [Fact]
    public async Task SingleProject_MultipleFiles()
    {
        var dir = TestAssetsManager.CreateTestDirectory();
        var project1 = Path.Combine(dir.Path, "Project1.csproj");
        var file1 = Path.Combine(dir.Path, "File1.cs");
        var file2 = Path.Combine(dir.Path, "File2.cs");

        using var context = CreateContext();

        var result = await context.Watcher.BuildProjectsAsync(
            projects:
            [
                new ProjectRepresentation(projectPath: null, entryPointFilePath: file1),
                new ProjectRepresentation(project1, entryPointFilePath: null),
                new ProjectRepresentation(projectPath: null, entryPointFilePath: file2)
            ],
            context.FileWatcher,
            mainProjectOptions: null,
            frameworkSelector: null,
            deviceSelector: null,
            CancellationToken.None);

        Assert.True(result.Success);

        AssertEx.SequenceEqual(
        [
            $"build {project1} -p A=1",
            $"build {file1} -p A=1",
            $"build {file2} -p A=1"
        ], context.BuildInvocations);
    }

    [Fact]
    public async Task MultipleProjects_MultipleFiles()
    {
        var dir = TestAssetsManager.CreateTestDirectory();
        var project1 = Path.Combine(dir.Path, "Project1.csproj");
        var project2 = Path.Combine(dir.Path, "Project2.csproj");
        var file1 = Path.Combine(dir.Path, "File1.cs");
        var file2 = Path.Combine(dir.Path, "File2.cs");

        using var context = CreateContext();

        var result = await context.Watcher.BuildProjectsAsync(
            projects:
            [
                new ProjectRepresentation(projectPath: null, entryPointFilePath: file1),
                new ProjectRepresentation(project1, entryPointFilePath: null),
                new ProjectRepresentation(project2, entryPointFilePath: null),
                new ProjectRepresentation(projectPath: null, entryPointFilePath: file2)
            ],
            context.FileWatcher,
            mainProjectOptions: null,
            frameworkSelector: null,
            deviceSelector: null,
            CancellationToken.None);

        Assert.True(result.Success);

        AssertEx.SequenceEqual(
        [
            "build <solution> -p A=1",
            $"build {file1} -p A=1",
            $"build {file2} -p A=1"
        ], context.BuildInvocations);
    }

    [Theory]
    [InlineData(ToolsetInfo.CurrentTargetFramework)]
    [InlineData("net9.0")]
    public async Task MultiTfm_FrameworkSelection(string expectedTfm)
    {
        var dir = TestAssetsManager.CreateTestDirectory(identifiers: [expectedTfm]);
        var project1 = Path.Combine(dir.Path, "Project1.csproj");

        var currentTfm = ToolsetInfo.CurrentTargetFramework;

        File.WriteAllText(project1, $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFrameworks>{currentTfm};net9.0</TargetFrameworks>
          </PropertyGroup>
        </Project>
        """);

        using var context = CreateContext(rootProjects: [project1]);

        var result = await context.Watcher.BuildProjectsAsync(
            [ProjectRepresentation.FromProjectOrEntryPointFilePath(project1)],
            context.FileWatcher,
            mainProjectOptions: TestOptions.ProjectOptions,
            frameworkSelector: (frameworks, _) =>
            {
                AssertEx.SequenceEqual([currentTfm, "net9.0"], frameworks);
                return ValueTask.FromResult(expectedTfm);
            },
            deviceSelector: null,
            CancellationToken.None);
        
        Assert.True(result.Success);
        Assert.NotNull(result.ProjectGraph);
        Assert.Equal(expectedTfm, result.MainProjectTargetFramework);

        AssertEx.SequenceEqual(
        [
            $"restore {project1} -p A=1 -consoleLoggerParameters:NoSummary",
            $"build {project1} -p A=1 --framework {expectedTfm} --no-restore"
        ], context.BuildInvocations);
    }

    [Fact]
    public async Task MultiTfm_CommandLineOption()
    {
        var dir = TestAssetsManager.CreateTestDirectory();
        var project1 = Path.Combine(dir.Path, "Project1.csproj");

        var currentTfm = ToolsetInfo.CurrentTargetFramework;

        File.WriteAllText(project1, $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFrameworks>{currentTfm};net9.0</TargetFrameworks>
          </PropertyGroup>
        </Project>
        """);

        using var context = CreateContext(rootProjects: [project1]);

        var result = await context.Watcher.BuildProjectsAsync(
            [ProjectRepresentation.FromProjectOrEntryPointFilePath(project1)],
            context.FileWatcher,
            mainProjectOptions: TestOptions.GetProjectOptions(["-f", "net9.0"]),
            frameworkSelector: (frameworks, _) =>
            {
                Assert.Fail("Selector should not be invoked");
                return ValueTask.FromResult("n/a");
            },
            deviceSelector: null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.ProjectGraph);
        Assert.Equal("net9.0", result.MainProjectTargetFramework);

        AssertEx.SequenceEqual(
        [
            $"build {project1} -p A=1 --framework net9.0"
        ], context.BuildInvocations);
    }

    [Fact]
    public async Task MultiTfm_NoMainProject()
    {
        var dir = TestAssetsManager.CreateTestDirectory();
        var project1 = Path.Combine(dir.Path, "Project1.csproj");

        var currentTfm = ToolsetInfo.CurrentTargetFramework;

        File.WriteAllText(project1, $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFrameworks>{currentTfm};net9.0</TargetFrameworks>
          </PropertyGroup>
        </Project>
        """);

        using var context = CreateContext(rootProjects: [project1]);

        var result = await context.Watcher.BuildProjectsAsync(
            [ProjectRepresentation.FromProjectOrEntryPointFilePath(project1)],
            context.FileWatcher,
            mainProjectOptions: null,
            frameworkSelector: (frameworks, _) =>
            {
                Assert.Fail("Selector should not be invoked");
                return ValueTask.FromResult("n/a");
            },
            deviceSelector: null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.ProjectGraph);
        Assert.Null(result.MainProjectTargetFramework);

        AssertEx.SequenceEqual(
        [
            $"build {project1} -p A=1"
        ], context.BuildInvocations);
    }
}
