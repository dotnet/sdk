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

        public WatcherContext(DualOutputHelper output, ImmutableArray<ProjectRepresentation> rootProjects)
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

            // Replace path to solution, which is a temp path, with a placeholder to make assertions easier.
            BuildInvocations.Add(string.Join(" ", processSpec.Arguments.Select(a =>
                a == SolutionFile ? "<solution>" : a)));
        }

        public void Dispose()
        {
            Assert.IsFalse(File.Exists(SolutionFile));
        }
    }

    private WatcherContext CreateContext(string[]? rootProjects = null)
        => new(Output, rootProjects?.Select(ProjectRepresentation.FromProjectOrEntryPointFilePath).ToImmutableArray() ?? []);

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
            evaluatedProjectGraph: null,
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
            evaluatedProjectGraph: null,
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
        const string DeviceName = "\"device (" + TestConstants.GB18030 + ")\";InjectedProperty=true,OtherProperty=value%3B";
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
            evaluatedProjectGraph: null,
            CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(DeviceName, result.SelectedDevice?.Name);
        AssertEx.SequenceEqual(
        [
            $"restore {project} -p A=1 -consoleLoggerParameters:NoSummary",
            $"build {project} -p A=1 --framework {ToolsetInfo.CurrentTargetFramework} -p:Device={EscapedDeviceId} --no-restore"
        ], context.BuildInvocations);

        Assert.IsNotNull(result.ProjectGraph);
        var projectNode = result.ProjectGraph.TryGetProjectNode(project, ToolsetInfo.CurrentTargetFramework);
        Assert.IsNotNull(projectNode);
        Assert.AreEqual(DeviceName, projectNode.ProjectInstance.GetPropertyValue("Device"));
        Assert.AreEqual("", projectNode.ProjectInstance.GetPropertyValue("InjectedProperty"));
    }

    [TestMethod]
    [DataRow("ios-arm64", "")]
    [DataRow("iossimulator-arm64", "")]
    [DataRow("ios-arm64", "test-iphone")]
    [DataRow("iossimulator-arm64", "test-iphone")]
    public async Task SingleProject_DeviceRuntimeIdentifierDeterminesIntermediateOutputPath(string defaultRuntimeIdentifier, string defaultDevice)
    {
        const string DeviceName = "test-iphone";
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
              <Devices Include="{DeviceName}" RuntimeIdentifier="{DeviceRuntimeIdentifier}" />
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
            evaluatedProjectGraph: null,
            CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(DeviceName, result.SelectedDevice?.Name);
        Assert.AreEqual(DeviceRuntimeIdentifier, result.SelectedDevice?.RuntimeIdentifier);
        AssertEx.SequenceEqual(
        [
            $"restore {project} -p A=1 -consoleLoggerParameters:NoSummary",
            $"restore {project} -p A=1 -p:Device={DeviceName} --runtime {DeviceRuntimeIdentifier} -consoleLoggerParameters:NoSummary",
            $"build {project} -p A=1 --framework {targetFramework} -p:Device={DeviceName} --runtime {DeviceRuntimeIdentifier} --no-restore"
        ], context.BuildInvocations);

        Assert.IsNotNull(result.ProjectGraph);
        var projectNode = result.ProjectGraph.TryGetProjectNode(project, targetFramework);
        Assert.IsNotNull(projectNode);
        Assert.AreEqual(DeviceName, projectNode.ProjectInstance.GetPropertyValue(PropertyNames.Device));
        var expectedGraphLoads = defaultDevice == DeviceName && defaultRuntimeIdentifier == DeviceRuntimeIdentifier ? 1 : 2;
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
        const string DeviceName = "test-iphone";
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
            Device = new DeviceId(DeviceName, DeviceRuntimeIdentifier)
        };
        var result = await context.Watcher.BuildProjectsAsync(
            [new ProjectRepresentation(project, entryPointFilePath: null)],
            context.FileWatcher,
            mainProjectOptions: options,
            frameworkSelector: (_, _) => throw new AssertFailedException("A single framework should be selected automatically"),
            deviceSelector: (_, _) => throw new AssertFailedException("The device is already selected"),
            evaluatedProjectGraph: null,
            CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(targetFramework, result.MainProjectTargetFramework);
        Assert.IsNotNull(result.ProjectGraph);
        var instance = result.ProjectGraph.Graph.GraphRoots.Single().ProjectInstance;
        Assert.AreEqual(DeviceName, instance.GetPropertyValue("Device"));
        Assert.AreEqual(DeviceRuntimeIdentifier, instance.GetPropertyValue("RuntimeIdentifier"));

        var rebuildResult = await context.Watcher.BuildProjectsAsync(
            [new ProjectRepresentation(project, entryPointFilePath: null)],
            context.FileWatcher,
            mainProjectOptions: options with { TargetFramework = targetFramework },
            frameworkSelector: null,
            deviceSelector: null,
            evaluatedProjectGraph: null,
            CancellationToken.None);

        Assert.IsTrue(rebuildResult.Success);
        Assert.AreEqual(DeviceName, rebuildResult.SelectedDevice?.Name);
        Assert.AreEqual(DeviceRuntimeIdentifier, rebuildResult.SelectedDevice?.RuntimeIdentifier);
        AssertEx.SequenceEqual(
        [
            $"restore {project} -p A=1 -p:Device={DeviceName} --runtime {DeviceRuntimeIdentifier} -consoleLoggerParameters:NoSummary",
            $"build {project} -p A=1 --framework {targetFramework} -p:Device={DeviceName} --runtime {DeviceRuntimeIdentifier} --no-restore",
            $"build {project} -p A=1 --framework {targetFramework} -p:Device={DeviceName} --runtime {DeviceRuntimeIdentifier}"
        ], context.BuildInvocations);
    }

    [TestMethod]
    public async Task MultipleProjects_DeviceRuntimeIdentifier()
    {
        var dir = TestAssetsManager.CreateTestDirectory();
        var project1 = Path.Combine(dir.Path, "Project1.csproj");
        var project2 = Path.Combine(dir.Path, "Project2.csproj");

        using var context = CreateContext();

        var result = await context.Watcher.BuildProjectsAsync(
            [
                new ProjectRepresentation(project1, entryPointFilePath: null),
                new ProjectRepresentation(project2, entryPointFilePath: null)
            ],
            context.FileWatcher,
            mainProjectOptions: TestOptions.ProjectOptions with { TargetFramework = "net9.0", Device = new DeviceId("device", "ios-arm64") },
            frameworkSelector: null,
            deviceSelector: null,
            evaluatedProjectGraph: null,
            CancellationToken.None);

        Assert.IsTrue(result.Success);

        // Solution builds do not support a global RuntimeIdentifier (NETSDK1134).
        AssertEx.SequenceEqual(
        [
            $"build {project1} -p A=1 --framework net9.0 -p:Device=device --runtime ios-arm64",
            $"build {project2} -p A=1 --framework net9.0 -p:Device=device --runtime ios-arm64"
        ], context.BuildInvocations);
    }

    [TestMethod]
    [DataRow("Library", false)]
    [DataRow("Exe", true)]
    public async Task ReferencedProject_DeviceRuntimeIdentifier(string outputType, bool expectRuntimeIdentifier)
    {
        var targetFramework = ToolsetInfo.CurrentTargetFramework;
        var runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;
        var dir = TestAssetsManager.CreateTestDirectory(identifiers: [outputType]);
        var app = Path.Combine(dir.Path, "App", "App.csproj");
        var library = Path.Combine(dir.Path, "Library", "Library.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(app)!);
        Directory.CreateDirectory(Path.GetDirectoryName(library)!);
        File.WriteAllText(app, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{targetFramework}</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../Library/Library.csproj" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(library, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{targetFramework}</TargetFramework>
                <OutputType>{outputType}</OutputType>
              </PropertyGroup>
            </Project>
            """);

        using var context = CreateContext([app]);
        var options = TestOptions.ProjectOptions with { Device = new DeviceId("device", runtimeIdentifier) };

        // Framework selection loads the project graph with the device's RuntimeIdentifier.
        var initialBuild = await context.Watcher.BuildProjectsAsync(
            [new ProjectRepresentation(app, entryPointFilePath: null)],
            context.FileWatcher,
            options,
            frameworkSelector: (_, _) => throw new AssertFailedException("A single framework should be selected automatically"),
            deviceSelector: null,
            evaluatedProjectGraph: null,
            CancellationToken.None);
        Assert.IsTrue(initialBuild.Success);
        Assert.IsNotNull(initialBuild.ProjectGraph);

        var rebuild = await context.Watcher.BuildProjectsAsync(
            [new ProjectRepresentation(library, entryPointFilePath: null)],
            context.FileWatcher,
            options with { TargetFramework = targetFramework },
            frameworkSelector: null,
            deviceSelector: null,
            initialBuild.ProjectGraph,
            CancellationToken.None);
        Assert.IsTrue(rebuild.Success);

        // A RID-agnostic library must keep the output path used by the app's build.
        Assert.AreEqual(
            $"build {library} -p A=1 --framework {targetFramework} -p:Device=device" + (expectRuntimeIdentifier ? $" --runtime {runtimeIdentifier}" : ""),
            context.BuildInvocations[^1]);
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
            evaluatedProjectGraph: null,
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
            evaluatedProjectGraph: null,
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
            evaluatedProjectGraph: null,
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
            evaluatedProjectGraph: null,
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
            evaluatedProjectGraph: null,
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
            evaluatedProjectGraph: null,
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
            evaluatedProjectGraph: null,
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
            evaluatedProjectGraph: null,
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
            evaluatedProjectGraph: null,
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
            evaluatedProjectGraph: null,
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
            evaluatedProjectGraph: null,
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
