// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.Watch.UnitTests;

public class BuildProjectsTests : IDisposable
{
    private readonly HotReloadDotNetWatcher _watcher;
    private readonly List<string> _buildFiles = [];
    private string? _solutionFile;

    public BuildProjectsTests(ITestOutputHelper output)
    {
        var environmentOptions = TestOptions.GetEnvironmentOptions();
        var processOutputReporter = new TestProcessOutputReporter();

        var runner = new TestProcessRunner()
        {
            RunImpl = (processSpec, _, _) =>
            {
                Assert.Equal("build", processSpec.Arguments[0]);
                Assert.Equal("arg1", processSpec.Arguments[2]);
                Assert.Equal("arg2", processSpec.Arguments[3]);

                var target = processSpec.Arguments[1];
                if (Path.GetExtension(target) == ".slnx")
                {
                    _solutionFile = target;
                    target = "<solution>";
                }

                _buildFiles.Add(target);
                return 0;
            }
        };

        var context = new DotNetWatchContext()
        {
            ProcessOutputReporter = processOutputReporter,
            LoggerFactory = NullLoggerFactory.Instance,
            Logger = NullLogger.Instance,
            BuildLogger = NullLogger.Instance,
            ProcessRunner = runner,
            Options = new(),
            MainProjectOptions = null,
            RootProjects = [],
            TargetFramework = null,
            BuildArguments = ["arg1", "arg2"],
            EnvironmentOptions = environmentOptions,
            BrowserLauncher = new BrowserLauncher(NullLogger.Instance, processOutputReporter, environmentOptions),
            BrowserRefreshServerFactory = new BrowserRefreshServerFactory()
        };

        var console = new TestConsole(output);
        _watcher = new HotReloadDotNetWatcher(context, console, runtimeProcessLauncherFactory: null);
    }

    public void Dispose()
    {
        Assert.False(File.Exists(_solutionFile));
    }

    [Fact]
    public async Task SingleProject()
    {
        var dir = Path.GetTempPath();
        var project1 = Path.Combine(dir, "Project1.csproj");

        Assert.True(await _watcher.BuildProjectsAsync([new ProjectRepresentation(project1, entryPointFilePath: null)], CancellationToken.None));

        AssertEx.SequenceEqual([project1], _buildFiles);
    }

    [Fact]
    public async Task MultipleProjects()
    {
        var dir = Path.GetTempPath();
        var project1 = Path.Combine(dir, "Project1.csproj");
        var project2 = Path.Combine(dir, "Project2.csproj");

        Assert.True(await _watcher.BuildProjectsAsync(
        [
            new ProjectRepresentation(project1, entryPointFilePath: null),
            new ProjectRepresentation(project2, entryPointFilePath: null)
        ], CancellationToken.None));

        AssertEx.SequenceEqual(["<solution>"], _buildFiles);
    }

    [Fact]
    public async Task SingleFile()
    {
        var dir = Path.GetTempPath();
        var file1 = Path.Combine(dir, "File1.cs");

        Assert.True(await _watcher.BuildProjectsAsync([new ProjectRepresentation(projectPath: null, entryPointFilePath: file1)], CancellationToken.None));

        AssertEx.SequenceEqual([file1], _buildFiles);
    }

    [Fact]
    public async Task MultipleFiles()
    {
        var dir = Path.GetTempPath();
        var file1 = Path.Combine(dir, "File1.cs");
        var file2 = Path.Combine(dir, "File2.cs");

        Assert.True(await _watcher.BuildProjectsAsync(
        [
            new ProjectRepresentation(projectPath: null, entryPointFilePath: file1),
            new ProjectRepresentation(projectPath: null, entryPointFilePath: file2)
        ], CancellationToken.None));

        AssertEx.SequenceEqual(
        [
            file1,
            file2
        ], _buildFiles);
    }

    [Fact]
    public async Task SingleProject_MultipleFiles()
    {
        var dir = Path.GetTempPath();
        var project1 = Path.Combine(dir, "Project1.csproj");
        var file1 = Path.Combine(dir, "File1.cs");
        var file2 = Path.Combine(dir, "File2.cs");

        Assert.True(await _watcher.BuildProjectsAsync(
        [
            new ProjectRepresentation(projectPath: null, entryPointFilePath: file1),
            new ProjectRepresentation(project1, entryPointFilePath: null),
            new ProjectRepresentation(projectPath: null, entryPointFilePath: file2)
        ], CancellationToken.None));

        AssertEx.SequenceEqual(
        [
            project1,
            file1,
            file2
        ], _buildFiles);
    }

    [Fact]
    public async Task MultipleProjects_MultipleFiles()
    {
        var dir = Path.GetTempPath();
        var project1 = Path.Combine(dir, "Project1.csproj");
        var project2 = Path.Combine(dir, "Project2.csproj");
        var file1 = Path.Combine(dir, "File1.cs");
        var file2 = Path.Combine(dir, "File2.cs");

        Assert.True(await _watcher.BuildProjectsAsync(
        [
            new ProjectRepresentation(projectPath: null, entryPointFilePath: file1),
            new ProjectRepresentation(project1, entryPointFilePath: null),
            new ProjectRepresentation(project2, entryPointFilePath: null),
            new ProjectRepresentation(projectPath: null, entryPointFilePath: file2)
        ], CancellationToken.None));

        AssertEx.SequenceEqual(
        [
            "<solution>",
            file1,
            file2
        ], _buildFiles);
    }
}
