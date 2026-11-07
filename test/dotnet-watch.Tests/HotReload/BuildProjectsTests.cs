// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias MSTestFramework;

using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class BuildProjects
{
    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = null!;
    private DualOutputHelper? _output;
    private DualOutputHelper Output => _output ??= new(new MSTestFramework::Microsoft.NET.TestFramework.TestContextOutputHelper(TestContext));
    private class WatcherContext : IDisposable
    {
        public readonly HotReloadDotNetWatcher Watcher;
        public readonly FileWatcher FileWatcher;
        public readonly TestConsole Console;
        public readonly TestLogger BuildLogger;

        public readonly List<string> BuildInvocations = [];
        public string? SolutionFile;

        public WatcherContext(DualOutputHelper output, ImmutableArray<ProjectRepresentation> rootProjects, bool runBuilds)
        {
            var environmentOptions = TestOptions.GetEnvironmentOptions();
            var processOutputReporter = new TestProcessOutputReporter();

            var processRunner = new TestProcessRunner()
            {
                RunImpl = (processSpec, _, _) =>
                {
                    LogBuildInvocation(processSpec);
                    return runBuilds ? null : 0;
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

            // Replace path to solution, which is a temp path, with a placeholder to make assertions easier.
            BuildInvocations.Add(string.Join(" ", processSpec.Arguments.Select(a =>
                a == SolutionFile ? "<solution>" : a)));
        }

        public void Dispose()
        {
            Assert.IsFalse(File.Exists(SolutionFile));
        }
    }

    private WatcherContext CreateContext(string[]? rootProjects = null, bool runBuilds = false)
        => new(Output, rootProjects?.Select(ProjectRepresentation.FromProjectOrEntryPointFilePath).ToImmutableArray() ?? [], runBuilds);

    [TestMethod]
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

        Assert.IsTrue(result.Success);

        AssertEx.SequenceEqual([$"build {project1} -p A=1"], context.BuildInvocations);
    }

    [TestMethod]
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

        Assert.IsTrue(result.Success);

        AssertEx.SequenceEqual(
        [
            $"restore {project1} -p A=1 -consoleLoggerParameters:NoSummary",
            $"build {project1} -p A=1 --framework net9.0 --no-restore"
        ], context.BuildInvocations);
    }

    [TestMethod]
    public async Task SingleProject_DeviceIdIsEscaped()
    {
        const string DeviceId = "\"device (" + TestConstants.GB18030 + ")\";InjectedProperty=true,OtherProperty=value%3B";
        const string EscapedDeviceId = "%22device %28" + TestConstants.GB18030 + "%29%22%3bInjectedProperty=true%2COtherProperty=value%253B";

        var dir = TestAssetsManager.CreateTestDirectory();
        var project = Path.Combine(dir.Path, "Project.csproj");
        File.WriteAllText(project, $"""
        <Project>
          <PropertyGroup>
            <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
          </PropertyGroup>
          <Target Name="ComputeAvailableDevices" Returns="@(Devices)">
            <ItemGroup>
              <Devices Include="{EscapedDeviceId}" />
            </ItemGroup>
          </Target>
        </Project>
        """);

        using var context = CreateContext([project]);
        var result = await context.Watcher.BuildProjectsAsync(
            [new ProjectRepresentation(project, entryPointFilePath: null)],
            context.FileWatcher,
            mainProjectOptions: TestOptions.ProjectOptions with { TargetFramework = ToolsetInfo.CurrentTargetFramework },
            frameworkSelector: null,
            deviceSelector: (_, _) => throw new AssertFailedException("A single device should be selected automatically"),
            CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(DeviceId, result.SelectedDevice);
        AssertEx.SequenceEqual(
        [
            $"restore {project} -p A=1 -consoleLoggerParameters:NoSummary",
            $"build {project} -p A=1 --framework {ToolsetInfo.CurrentTargetFramework} -p:Device={EscapedDeviceId} --no-restore"
        ], context.BuildInvocations);

        Assert.IsNotNull(result.ProjectGraph);
        var projectNode = result.ProjectGraph.TryGetProjectNode(project, ToolsetInfo.CurrentTargetFramework);
        Assert.IsNotNull(projectNode);
        Assert.AreEqual(DeviceId, projectNode.ProjectInstance.GetPropertyValue("Device"));
        Assert.AreEqual("", projectNode.ProjectInstance.GetPropertyValue("InjectedProperty"));
    }

    [TestMethod]
    [DataRow("ios-arm64", "")]
    [DataRow("iossimulator-arm64", "")]
    [DataRow("ios-arm64", "test-iphone")]
    [DataRow("iossimulator-arm64", "test-iphone")]
    public async Task SingleProject_DeviceRuntimeIdentifierDeterminesIntermediateOutputPath(string defaultRuntimeIdentifier, string defaultDevice)
    {
        const string DeviceId = "test-iphone";
        const string DeviceRuntimeIdentifier = "ios-arm64";
        var targetFramework = ToolsetInfo.CurrentTargetFramework;
        var dir = TestAssetsManager.CreateTestDirectory(identifiers: [defaultRuntimeIdentifier, defaultDevice]);
        var project = Path.Combine(dir.Path, "Project.csproj");

        // Model the Apple workload's default simulator RID without requiring a mobile SDK.
        File.WriteAllText(project, $"""
        <Project>
          <PropertyGroup>
            <TargetFramework>{targetFramework}</TargetFramework>
            <Device Condition="'$(Device)' == ''">{defaultDevice}</Device>
            <RuntimeIdentifier Condition="'$(RuntimeIdentifier)' == ''">{defaultRuntimeIdentifier}</RuntimeIdentifier>
            <IntermediateOutputPath>obj\Debug\$(TargetFramework)\$(RuntimeIdentifier)\</IntermediateOutputPath>
          </PropertyGroup>
          <ItemGroup>
            <ProjectCapability Include="HotReloadWebSockets" />
          </ItemGroup>
          <Target Name="ComputeAvailableDevices" Returns="@(Devices)">
            <ItemGroup>
              <Devices Include="{DeviceId}" RuntimeIdentifier="{DeviceRuntimeIdentifier}" />
            </ItemGroup>
          </Target>
        </Project>
        """);

        using var context = CreateContext([project]);
        var result = await context.Watcher.BuildProjectsAsync(
            [new ProjectRepresentation(project, entryPointFilePath: null)],
            context.FileWatcher,
            mainProjectOptions: TestOptions.ProjectOptions with { TargetFramework = targetFramework },
            frameworkSelector: null,
            deviceSelector: (_, _) => throw new AssertFailedException("A single device should be selected automatically"),
            CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(DeviceId, result.SelectedDevice);
        Assert.AreEqual(DeviceRuntimeIdentifier, result.SelectedDeviceRuntimeIdentifier);
        AssertEx.SequenceEqual(
        [
            $"restore {project} -p A=1 -consoleLoggerParameters:NoSummary",
            $"restore {project} -p A=1 -p:Device={DeviceId} --runtime {DeviceRuntimeIdentifier} -consoleLoggerParameters:NoSummary",
            $"build {project} -p A=1 --framework {targetFramework} -p:Device={DeviceId} --runtime {DeviceRuntimeIdentifier} --no-restore"
        ], context.BuildInvocations);

        Assert.IsNotNull(result.ProjectGraph);
        var projectNode = result.ProjectGraph.TryGetProjectNode(project, targetFramework);
        Assert.IsNotNull(projectNode);
        Assert.AreEqual(DeviceId, projectNode.ProjectInstance.GetPropertyValue(PropertyNames.Device));
        var expectedGraphLoads = defaultDevice == DeviceId && defaultRuntimeIdentifier == DeviceRuntimeIdentifier ? 1 : 2;
        Assert.AreEqual(expectedGraphLoads,
            context.BuildLogger.GetAndClearMessages().Count(message => message.StartsWith("[Debug] Project graph loaded in ", StringComparison.Ordinal)));
        var watcherKeyPath = Path.Combine(projectNode.ProjectInstance.GetIntermediateOutputDirectory()!, "HotReload", "websocket-key.private.json");
        var buildKeyPath = Path.Combine(dir.Path, "obj", "Debug", targetFramework, DeviceRuntimeIdentifier, "HotReload", "websocket-key.private.json");

        Assert.AreEqual(buildKeyPath, watcherKeyPath,
            "Watch must read the key from the same RID-specific directory used by the selected device's build.");
    }

    [TestMethod]
    public async Task SingleProject_PreselectedDevice()
    {
        const string DeviceId = "test-iphone";
        const string DeviceRuntimeIdentifier = "ios-arm64";
        var targetFramework = ToolsetInfo.CurrentTargetFramework;
        var dir = TestAssetsManager.CreateTestDirectory();
        var project = Path.Combine(dir.Path, "Project.csproj");
        File.WriteAllText(project, $"""
        <Project>
          <PropertyGroup>
            <TargetFramework>{targetFramework}</TargetFramework>
            <RuntimeIdentifier Condition="'$(RuntimeIdentifier)' == ''">iossimulator-arm64</RuntimeIdentifier>
          </PropertyGroup>
        </Project>
        """);

        using var context = CreateContext([project]);
        var options = TestOptions.ProjectOptions with
        {
            Device = DeviceId,
            DeviceRuntimeIdentifier = DeviceRuntimeIdentifier
        };
        var result = await context.Watcher.BuildProjectsAsync(
            [new ProjectRepresentation(project, entryPointFilePath: null)],
            context.FileWatcher,
            mainProjectOptions: options,
            frameworkSelector: (_, _) => throw new AssertFailedException("A single framework should be selected automatically"),
            deviceSelector: (_, _) => throw new AssertFailedException("The device is already selected"),
            CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(targetFramework, result.MainProjectTargetFramework);
        Assert.IsNotNull(result.ProjectGraph);
        var instance = result.ProjectGraph.Graph.GraphRoots.Single().ProjectInstance;
        Assert.AreEqual(DeviceId, instance.GetPropertyValue("Device"));
        Assert.AreEqual(DeviceRuntimeIdentifier, instance.GetPropertyValue("RuntimeIdentifier"));

        var rebuildResult = await context.Watcher.BuildProjectsAsync(
            [new ProjectRepresentation(project, entryPointFilePath: null)],
            context.FileWatcher,
            mainProjectOptions: options with { TargetFramework = targetFramework },
            frameworkSelector: null,
            deviceSelector: null,
            CancellationToken.None);

        Assert.IsTrue(rebuildResult.Success);
        Assert.AreEqual(DeviceId, rebuildResult.SelectedDevice);
        Assert.AreEqual(DeviceRuntimeIdentifier, rebuildResult.SelectedDeviceRuntimeIdentifier);
        AssertEx.SequenceEqual(
        [
            $"restore {project} -p A=1 -p:Device={DeviceId} --runtime {DeviceRuntimeIdentifier} -consoleLoggerParameters:NoSummary",
            $"build {project} -p A=1 --framework {targetFramework} -p:Device={DeviceId} --runtime {DeviceRuntimeIdentifier} --no-restore",
            $"build {project} -p A=1 --framework {targetFramework} -p:Device={DeviceId} --runtime {DeviceRuntimeIdentifier}"
        ], context.BuildInvocations);
    }

    [TestMethod]
    public async Task MultipleProjects_SelectedDeviceRuntimeIdentifier()
    {
        var dir = TestAssetsManager.CreateTestDirectory();
        var projects = new[] { "Project1", "Project2" }.Select(name =>
        {
            var projectDirectory = Directory.CreateDirectory(Path.Combine(dir.Path, name)).FullName;
            var path = Path.Combine(projectDirectory, name + ".csproj");
            File.WriteAllText(path, $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            return ProjectRepresentation.FromProjectOrEntryPointFilePath(path);
        }).ToImmutableArray();

        using var context = CreateContext(runBuilds: true);
        var options = TestOptions.ProjectOptions with { TargetFramework = ToolsetInfo.CurrentTargetFramework };
        var control = await context.Watcher.BuildProjectsAsync(
            projects, context.FileWatcher, options, frameworkSelector: null, deviceSelector: null, CancellationToken.None);
        Assert.IsTrue(control.Success);

        var result = await context.Watcher.BuildProjectsAsync(
            projects, context.FileWatcher,
            options with { Device = "test-device", DeviceRuntimeIdentifier = RuntimeInformation.RuntimeIdentifier },
            frameworkSelector: null, deviceSelector: null, CancellationToken.None);

        Assert.IsTrue(result.Success, "Rebuilding multiple projects for a selected device should succeed.");
        AssertEx.SequenceEqual(
        [
            $"build <solution> -p A=1 --framework {ToolsetInfo.CurrentTargetFramework}",
            $"build {projects[0].ProjectOrEntryPointFilePath} -p A=1 --framework {ToolsetInfo.CurrentTargetFramework} -p:Device=test-device --runtime {RuntimeInformation.RuntimeIdentifier}",
            $"build {projects[1].ProjectOrEntryPointFilePath} -p A=1 --framework {ToolsetInfo.CurrentTargetFramework} -p:Device=test-device --runtime {RuntimeInformation.RuntimeIdentifier}"
        ], context.BuildInvocations);
    }

    [TestMethod]
    public async Task SingleProject_DeviceRuntimeIdentifierIsNormalized()
    {
        var dir = TestAssetsManager.CreateTestDirectory();
        var project = Path.Combine(dir.Path, "Project.csproj");
        File.WriteAllText(project, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
              <Target Name="ComputeAvailableDevices" Returns="@(Devices)">
                <ItemGroup>
                  <Devices Include="test-device" RuntimeIdentifier="win-amd64" />
                </ItemGroup>
              </Target>
              <Target Name="RecordIntermediateOutputPath" AfterTargets="Build">
                <WriteLinesToFile File="built-intermediate.txt" Lines="$(IntermediateOutputPath)" Overwrite="true" />
              </Target>
            </Project>
            """);

        using var context = CreateContext([project], runBuilds: true);
        var result = await context.Watcher.BuildProjectsAsync(
            [ProjectRepresentation.FromProjectOrEntryPointFilePath(project)], context.FileWatcher,
            TestOptions.ProjectOptions with { TargetFramework = ToolsetInfo.CurrentTargetFramework },
            frameworkSelector: null,
            deviceSelector: (_, _) => throw new AssertFailedException("A single device should be selected automatically"),
            CancellationToken.None);

        Assert.IsTrue(result.Success);
        var builtOutputDirectory = Path.GetFullPath(File.ReadAllText(Path.Combine(dir.Path, "built-intermediate.txt")).Trim(), dir.Path);
        Assert.AreEqual("win-x64", Path.GetFileName(Path.TrimEndingDirectorySeparator(builtOutputDirectory)));
        Assert.IsNotNull(result.ProjectGraph);
        var node = result.ProjectGraph.TryGetProjectNode(project, ToolsetInfo.CurrentTargetFramework);
        Assert.IsNotNull(node);
        Assert.AreEqual(builtOutputDirectory, node.ProjectInstance.GetIntermediateOutputDirectory(),
            "The graph and build should use the same normalized RID.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReferencedProject_RebuildUsesOriginalOutputPath(bool isExecutable)
    {
        var dir = TestAssetsManager.CreateTestDirectory(identifiers: [isExecutable]);
        var appDirectory = Directory.CreateDirectory(Path.Combine(dir.Path, "App")).FullName;
        var libraryDirectory = Directory.CreateDirectory(Path.Combine(dir.Path, "Library")).FullName;
        var app = Path.Combine(appDirectory, "App.csproj");
        var library = Path.Combine(libraryDirectory, "Library.csproj");
        var source = Path.Combine(libraryDirectory, "Library.cs");
        File.WriteAllText(app, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../Library/Library.csproj" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(library, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                <OutputType>{(isExecutable ? "Exe" : "Library")}</OutputType>
                <UseAppHost>false</UseAppHost>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(source, "public class Library { public const int Value = 1; public static void Main() {} }");

        using var context = CreateContext([app], runBuilds: true);
        var options = TestOptions.ProjectOptions with
        {
            TargetFramework = ToolsetInfo.CurrentTargetFramework,
            Device = "test-device",
            DeviceRuntimeIdentifier = RuntimeInformation.RuntimeIdentifier
        };
        var initialBuild = await context.Watcher.BuildProjectsAsync(
            [ProjectRepresentation.FromProjectOrEntryPointFilePath(app)], context.FileWatcher, options with { TargetFramework = null },
            frameworkSelector: (_, _) => throw new AssertFailedException("A single framework should be selected automatically"),
            deviceSelector: null, CancellationToken.None);
        Assert.IsTrue(initialBuild.Success);
        Assert.IsNotNull(initialBuild.ProjectGraph);
        var expectedRuntimeIdentifier = isExecutable ? RuntimeInformation.RuntimeIdentifier : "";

        var libraryOutput = Path.Combine(libraryDirectory, "bin", "Debug", ToolsetInfo.CurrentTargetFramework, expectedRuntimeIdentifier, "Library.dll");
        var originalAssembly = File.ReadAllBytes(libraryOutput);
        File.WriteAllText(source, "public class Library { public const int Value = 2; public static void Main() {} }");

        var rebuild = await context.Watcher.BuildProjectsAsync(
            [ProjectRepresentation.FromProjectOrEntryPointFilePath(library)], context.FileWatcher, options,
            frameworkSelector: null, deviceSelector: null, CancellationToken.None,
            evaluatedProjectGraph: initialBuild.ProjectGraph);
        Assert.IsTrue(rebuild.Success);
        Assert.IsFalse(originalAssembly.SequenceEqual(File.ReadAllBytes(libraryOutput)),
            "The rebuild should update the dependency output used by the app's original build.");
    }

    [TestMethod]
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

        Assert.IsTrue(result.Success);

        AssertEx.SequenceEqual(["build <solution> -p A=1"], context.BuildInvocations);
    }

    [TestMethod]
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

        Assert.IsTrue(result.Success);

        AssertEx.SequenceEqual([$"build {file1} -p A=1"], context.BuildInvocations);
    }

    [TestMethod]
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

        Assert.IsTrue(result.Success);

        AssertEx.SequenceEqual([$"build {file1} -p A=1 --framework net9.0"], context.BuildInvocations);
    }

    [TestMethod]
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

            Assert.IsFalse(result.Success);
            Assert.IsEmpty(context.BuildInvocations);
        }
        else
        {
            Assert.IsTrue(result.Success);
            AssertEx.SequenceEqual([$"build {file1} -p A=1 --framework net9.0"], context.BuildInvocations);
        }
    }

    [TestMethod]
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

        Assert.IsTrue(result.Success);

        AssertEx.SequenceEqual([$"build {file1} -p A=1 --framework net8.0"], context.BuildInvocations);
    }

    [TestMethod]
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

        Assert.IsTrue(result.Success);

        AssertEx.SequenceEqual(
        [
            $"build {file1} -p A=1",
            $"build {file2} -p A=1"
        ], context.BuildInvocations);
    }

    [TestMethod]
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

        Assert.IsTrue(result.Success);

        AssertEx.SequenceEqual(
        [
            $"build {project1} -p A=1",
            $"build {file1} -p A=1",
            $"build {file2} -p A=1"
        ], context.BuildInvocations);
    }

    [TestMethod]
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

        Assert.IsTrue(result.Success);

        AssertEx.SequenceEqual(
        [
            "build <solution> -p A=1",
            $"build {file1} -p A=1",
            $"build {file2} -p A=1"
        ], context.BuildInvocations);
    }

    [TestMethod]
    [DataRow(ToolsetInfo.CurrentTargetFramework)]
    [DataRow("net9.0")]
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
        
        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.ProjectGraph);
        Assert.AreEqual(expectedTfm, result.MainProjectTargetFramework);

        AssertEx.SequenceEqual(
        [
            $"restore {project1} -p A=1 -consoleLoggerParameters:NoSummary",
            $"build {project1} -p A=1 --framework {expectedTfm} --no-restore"
        ], context.BuildInvocations);
    }

    [TestMethod]
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

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ProjectGraph);
        Assert.AreEqual("net9.0", result.MainProjectTargetFramework);

        AssertEx.SequenceEqual(
        [
            $"build {project1} -p A=1 --framework net9.0"
        ], context.BuildInvocations);
    }

    [TestMethod]
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

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ProjectGraph);
        Assert.IsNull(result.MainProjectTargetFramework);

        AssertEx.SequenceEqual(
        [
            $"build {project1} -p A=1"
        ], context.BuildInvocations);
    }
}
