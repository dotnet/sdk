// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Watch.UnitTests;

public class RuntimeProcessLauncherTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    public enum TriggerEvent
    {
        HotReloadSessionStarting,
        HotReloadSessionStarted,
        WaitingForChanges,
    }

    private record class RunningWatcher(
        RuntimeProcessLauncherTests Test,
        HotReloadDotNetWatcher Watcher,
        Task Task,
        TestReporter Reporter,
        TestConsole Console,
        StrongBox<TestRuntimeProcessLauncher?> ServiceHolder,
        CancellationTokenSource ShutdownSource) : IAsyncDisposable
    {
        public TestRuntimeProcessLauncher? Service => ServiceHolder.Value;

        public async ValueTask DisposeAsync()
        {
            if (!ShutdownSource.IsCancellationRequested)
            {
                Test.Log("Shutting down");
                ShutdownSource.Cancel();
            }

            try
            {
                await Task;
            }
            catch (OperationCanceledException)
            {
            }
        }

        public TaskCompletionSource CreateCompletionSource()
        {
            var source = new TaskCompletionSource();
            ShutdownSource.Token.Register(() => source.TrySetCanceled(ShutdownSource.Token));
            return source;
        }
    }

    private TestAsset CopyTestAsset(string assetName, params object[] testParameters)
        => TestAssets.CopyTestAsset(assetName, identifier: string.Join(";", testParameters)).WithSource();

    private static async Task<RunningProject> Launch(string projectPath, TestRuntimeProcessLauncher service, string workingDirectory, CancellationToken cancellationToken)
    {
        var projectOptions = new ProjectOptions()
        {
            IsRootProject = false,
            ProjectPath = projectPath,
            WorkingDirectory = workingDirectory,
            BuildArguments = [],
            Command = "run",
            CommandArguments = ["--project", projectPath],
            LaunchEnvironmentVariables = [],
            LaunchProfileName = null,
            NoLaunchProfile = true,
            TargetFramework = null,
        };

        RestartOperation? startOp = null;
        startOp = new RestartOperation(async cancellationToken =>
        {
            var result = await service.ProjectLauncher.TryLaunchProcessAsync(
                projectOptions,
                new CancellationTokenSource(),
                onOutput: null,
                restartOperation: startOp!,
                cancellationToken);

            Assert.NotNull(result);

            await result.WaitForProcessRunningAsync(cancellationToken);

            return result;
        });

        return await startOp(cancellationToken);
    }

    private RunningWatcher StartWatcher(TestAsset testAsset, string[] args, string workingDirectory, string projectPath, SemaphoreSlim? fileChangesCompleted = null)
    {
        var console = new TestConsole(Logger);
        var reporter = new TestReporter(Logger);

        var program = Program.TryCreate(
           TestOptions.GetCommandLineOptions(["--verbose", ..args, "--project", projectPath]),
           console,
           TestOptions.GetEnvironmentOptions(workingDirectory, TestContext.Current.ToolsetUnderTest.DotNetHostPath, testAsset) with
           {
               ProcessCleanupTimeout = TimeSpan.FromSeconds(0),
           },
           reporter,
           out var errorCode);

        Assert.Equal(0, errorCode);
        Assert.NotNull(program);

        var serviceHolder = new StrongBox<TestRuntimeProcessLauncher?>();
        var factory = new TestRuntimeProcessLauncher.Factory(s =>
        {
            serviceHolder.Value = s;
        });

        var watcher = Assert.IsType<HotReloadDotNetWatcher>(program.CreateWatcher(factory));

        var shutdownSource = new CancellationTokenSource();
        var watchTask = Task.Run(async () =>
        {
            try
            {
                await watcher.WatchAsync(shutdownSource.Token);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                shutdownSource.Cancel();
                ((IReporter)reporter).Error($"Unexpected exception {e}");
                throw;
            }
        }, shutdownSource.Token);

        return new RunningWatcher(this, watcher, watchTask, reporter, console, serviceHolder, shutdownSource);
    }

    [Theory]
    [CombinatorialData]
    public async Task UpdateAndRudeEdit(TriggerEvent trigger)
    {
        var testAsset = CopyTestAsset("WatchAppMultiProc", trigger);

        var tfm = ToolsetInfo.CurrentTargetFramework;

        var workingDirectory = testAsset.Path;
        var hostDir = Path.Combine(testAsset.Path, "Host");
        var hostProject = Path.Combine(hostDir, "Host.csproj");
        var serviceDirA = Path.Combine(testAsset.Path, "ServiceA");
        var serviceSourceA1 = Path.Combine(serviceDirA, "A1.cs");
        var serviceSourceA2 = Path.Combine(serviceDirA, "A2.cs");
        var serviceProjectA = Path.Combine(serviceDirA, "A.csproj");
        var serviceDirB = Path.Combine(testAsset.Path, "ServiceB");
        var serviceProjectB = Path.Combine(serviceDirB, "B.csproj");
        var libDir = Path.Combine(testAsset.Path, "Lib");
        var libProject = Path.Combine(libDir, "Lib.csproj");
        var libSource = Path.Combine(libDir, "Lib.cs");

        await using var w = StartWatcher(testAsset, ["--non-interactive"], workingDirectory, hostProject);

        var launchCompletionA = w.CreateCompletionSource();
        var launchCompletionB = w.CreateCompletionSource();

        w.Reporter.RegisterAction(trigger switch
        {
            TriggerEvent.HotReloadSessionStarting => MessageDescriptor.HotReloadSessionStarting,
            TriggerEvent.HotReloadSessionStarted => MessageDescriptor.HotReloadSessionStarted,
            TriggerEvent.WaitingForChanges => MessageDescriptor.WaitingForChanges,
            _ => throw new InvalidOperationException(),
        }, () =>
        {
            // only launch once
            if (launchCompletionA.Task.IsCompleted)
            {
                return;
            }

            // service should have been created before Hot Reload session started:
            Assert.NotNull(w.Service);

            Launch(serviceProjectA, w.Service, workingDirectory, w.ShutdownSource.Token).Wait();
            launchCompletionA.TrySetResult();

            Launch(serviceProjectB, w.Service, workingDirectory, w.ShutdownSource.Token).Wait();
            launchCompletionB.TrySetResult();
        });

        var waitingForChanges = w.Reporter.RegisterSemaphore(MessageDescriptor.WaitingForChanges);

        var changeHandled = w.Reporter.RegisterSemaphore(MessageDescriptor.HotReloadChangeHandled);
        var sessionStarted = w.Reporter.RegisterSemaphore(MessageDescriptor.HotReloadSessionStarted);
        var projectBaselinesUpdated = w.Reporter.RegisterSemaphore(MessageDescriptor.ProjectBaselinesUpdated);
        await launchCompletionA.Task;
        await launchCompletionB.Task;

        // let the host process start:
        Log("Waiting for changes...");
        await waitingForChanges.WaitAsync(w.ShutdownSource.Token);

        Log("Waiting for session started...");
        await sessionStarted.WaitAsync(w.ShutdownSource.Token);

        await MakeRudeEditChange();

        Log("Waiting for changed handled ...");
        await changeHandled.WaitAsync(w.ShutdownSource.Token);

        // Wait for project baselines to be updated, so that we capture the new solution snapshot
        // and further changes are treated as another update.
        Log("Waiting for baselines updated...");
        await projectBaselinesUpdated.WaitAsync(w.ShutdownSource.Token);

        await MakeValidDependencyChange();

        Log("Waiting for changed handled ...");
        await changeHandled.WaitAsync(w.ShutdownSource.Token);

        // Hot Reload shared dependency - should update both service projects
        async Task MakeValidDependencyChange()
        {
            var hasUpdateSourceA = w.CreateCompletionSource();
            var hasUpdateSourceB = w.CreateCompletionSource();
            w.Reporter.OnProcessOutput += line =>
            {
                if (line.Content.Contains("<Updated Lib>"))
                {
                    if (line.Content.StartsWith($"[A ({tfm})]"))
                    {
                        if (!hasUpdateSourceA.Task.IsCompleted)
                        {
                            hasUpdateSourceA.SetResult();
                        }
                    }
                    else if (line.Content.StartsWith($"[B ({tfm})]"))
                    {
                        if (!hasUpdateSourceB.Task.IsCompleted)
                        {
                            hasUpdateSourceB.SetResult();
                        }
                    }
                    else
                    {
                        Assert.Fail($"Only service projects should be updated: '{line.Content}'");
                    }
                }
            };

            await Task.Delay(TimeSpan.FromSeconds(1));

            UpdateSourceFile(libSource,
                """
                using System;

                public class Lib
                {
                    public static void Common()
                    {
                        Console.WriteLine("<Updated Lib>");
                    }
                }
                """);

            Log("Waiting for updated output from project A ...");
            await hasUpdateSourceA.Task;

            Log("Waiting for updated output from project B ...");
            await hasUpdateSourceB.Task;

            Assert.True(hasUpdateSourceA.Task.IsCompletedSuccessfully);
            Assert.True(hasUpdateSourceB.Task.IsCompletedSuccessfully);
        }

        // make a rude edit and check that the process is restarted
        async Task MakeRudeEditChange()
        {
            var hasUpdateSource = w.CreateCompletionSource();
            w.Reporter.OnProcessOutput += line =>
            {
                if (line.Content.StartsWith($"[A ({tfm})]") && line.Content.Contains("Started A: 2"))
                {
                    hasUpdateSource.SetResult();
                }
            };

            await Task.Delay(TimeSpan.FromSeconds(1));

            // rude edit in A (changing assembly level attribute):
            UpdateSourceFile(serviceSourceA2, """
                [assembly: System.Reflection.AssemblyMetadata("TestAssemblyMetadata", "2")]
                """);

            Log("Waiting for updated output from project A ...");
            await hasUpdateSource.Task;

            Assert.True(hasUpdateSource.Task.IsCompletedSuccessfully);
        }
    }

    [Theory]
    [CombinatorialData] 
    public async Task UpdateAppliedToNewProcesses(bool sharedOutput)
    {
        var testAsset = CopyTestAsset("WatchAppMultiProc", sharedOutput);
        var tfm = ToolsetInfo.CurrentTargetFramework;

        if (sharedOutput)
        {
            testAsset = testAsset.UpdateProjProperty("OutDir", "bin", @"..\Shared");
        }

        var workingDirectory = testAsset.Path;
        var hostDir = Path.Combine(testAsset.Path, "Host");
        var hostProject = Path.Combine(hostDir, "Host.csproj");
        var serviceDirA = Path.Combine(testAsset.Path, "ServiceA");
        var serviceProjectA = Path.Combine(serviceDirA, "A.csproj");
        var serviceDirB = Path.Combine(testAsset.Path, "ServiceB");
        var serviceProjectB = Path.Combine(serviceDirB, "B.csproj");
        var libDir = Path.Combine(testAsset.Path, "Lib");
        var libProject = Path.Combine(libDir, "Lib.csproj");
        var libSource = Path.Combine(libDir, "Lib.cs");

        await using var w = StartWatcher(testAsset, ["--non-interactive"], workingDirectory, hostProject);

        var waitingForChanges = w.Reporter.RegisterSemaphore(MessageDescriptor.WaitingForChanges);
        var changeHandled = w.Reporter.RegisterSemaphore(MessageDescriptor.HotReloadChangeHandled);
        var updatesApplied = w.Reporter.RegisterSemaphore(MessageDescriptor.UpdatesApplied);

        var hasUpdateA = new SemaphoreSlim(initialCount: 0);
        var hasUpdateB = new SemaphoreSlim(initialCount: 0);
        w.Reporter.OnProcessOutput += line =>
        {
            if (line.Content.Contains("<Updated Lib>"))
            {
                if (line.Content.StartsWith($"[A ({tfm})]"))
                {
                    hasUpdateA.Release();
                }
                else if (line.Content.StartsWith($"[B ({tfm})]"))
                {
                    hasUpdateB.Release();
                }
                else
                {
                    Assert.Fail($"Only service projects should be updated: '{line.Content}'");
                }
            }
        };

        // let the host process start:
        Log("Waiting for changes...");
        await waitingForChanges.WaitAsync(w.ShutdownSource.Token);

        // service should have been created before Hot Reload session started:
        Assert.NotNull(w.Service);

        await Launch(serviceProjectA, w.Service, workingDirectory, w.ShutdownSource.Token);

        UpdateSourceFile(libSource,
            """
                using System;

                public class Lib
                {
                    public static void Common()
                    {
                        Console.WriteLine("<Updated Lib>");
                    }
                }
                """);

        Log("Waiting for updated output from A ...");
        await hasUpdateA.WaitAsync(w.ShutdownSource.Token);

        // Host and ServiceA received updates:
        Log("Waiting for updates applied 1/2 ...");
        await updatesApplied.WaitAsync(w.ShutdownSource.Token);

        Log("Waiting for updates applied 2/2 ...");
        await updatesApplied.WaitAsync(w.ShutdownSource.Token);

        await Launch(serviceProjectB, w.Service, workingDirectory, w.ShutdownSource.Token);

        // ServiceB received updates:
        Log("Waiting for updates applied ...");
        await updatesApplied.WaitAsync(w.ShutdownSource.Token);

        Log("Waiting for updated output from B ...");
        await hasUpdateB.WaitAsync(w.ShutdownSource.Token);
    }

    public enum UpdateLocation
    {
        Dependency,
        TopLevel,
        TopFunction,
    }

    [Theory]
    [CombinatorialData]
    public async Task HostRestart(UpdateLocation updateLocation)
    {
        var testAsset = CopyTestAsset("WatchAppMultiProc", updateLocation);
        var tfm = ToolsetInfo.CurrentTargetFramework;

        var workingDirectory = testAsset.Path;
        var hostDir = Path.Combine(testAsset.Path, "Host");
        var hostProject = Path.Combine(hostDir, "Host.csproj");
        var hostProgram = Path.Combine(hostDir, "Program.cs");
        var libProject = Path.Combine(testAsset.Path, "Lib2", "Lib2.csproj");
        var lib = Path.Combine(testAsset.Path, "Lib2", "Lib2.cs");

        await using var w = StartWatcher(testAsset, args: [], workingDirectory, hostProject);

        var waitingForChanges = w.Reporter.RegisterSemaphore(MessageDescriptor.WaitingForChanges);
        var changeHandled = w.Reporter.RegisterSemaphore(MessageDescriptor.HotReloadChangeHandled);
        var restartNeeded = w.Reporter.RegisterSemaphore(MessageDescriptor.ApplyUpdate_ChangingEntryPoint);
        var restartRequested = w.Reporter.RegisterSemaphore(MessageDescriptor.RestartRequested);

        var hasUpdate = new SemaphoreSlim(initialCount: 0);
        w.Reporter.OnProcessOutput += line =>
        {
            if (line.Content.Contains("<Updated>"))
            {
                if (line.Content.StartsWith($"[Host ({tfm})]"))
                {
                    hasUpdate.Release();
                }
                else
                {
                    Assert.Fail($"Only service projects should be updated: '{line.Content}'");
                }
            }
        };

        await Task.Delay(TimeSpan.FromSeconds(1));

        // let the host process start:
        Log("Waiting for changes...");
        await waitingForChanges.WaitAsync(w.ShutdownSource.Token);

        switch (updateLocation)
        {
            case UpdateLocation.Dependency:
                UpdateSourceFile(lib, """
                    using System;

                    public class Lib2
                    {
                        public static void Print()
                        {
                            Console.WriteLine("<Updated>");
                        }
                    }
                    """);

                // Host received Hot Reload updates:
                Log("Waiting for change handled ...");
                await changeHandled.WaitAsync(w.ShutdownSource.Token);
                break;

            case UpdateLocation.TopFunction:
                // Update top-level function body:
                UpdateSourceFile(hostProgram, content => content.Replace("Waiting", "<Updated>"));

                // Host received Hot Reload updates:
                Log("Waiting for change handled ...");
                await changeHandled.WaitAsync(w.ShutdownSource.Token);
                break;

            case UpdateLocation.TopLevel:
                // Update top-level code that does not get reloaded until the app restarts:
                UpdateSourceFile(hostProgram, content => content.Replace("Started", "<Updated>"));

                // ⚠ ENC0118: Changing 'top-level code' might not have any effect until the application is restarted. Press "Ctrl + R" to restart.
                Log("Waiting for restart needed ...");
                await restartNeeded.WaitAsync(w.ShutdownSource.Token);

                w.Console.PressKey(new ConsoleKeyInfo('R', ConsoleKey.R, shift: false, alt: false, control: true));

                Log("Waiting for restart requested ...");
                await restartRequested.WaitAsync(w.ShutdownSource.Token);
                break;
        }

        Log("Waiting updated output from Host ...");
        await hasUpdate.WaitAsync(w.ShutdownSource.Token);
    }

    [Fact]
    public async Task RudeEditInProjectWithoutRunningProcess()
    {
        var testAsset = CopyTestAsset("WatchAppMultiProc");

        var workingDirectory = testAsset.Path;
        var hostDir = Path.Combine(testAsset.Path, "Host");
        var hostProject = Path.Combine(hostDir, "Host.csproj");
        var serviceDirA = Path.Combine(testAsset.Path, "ServiceA");
        var serviceSourceA2 = Path.Combine(serviceDirA, "A2.cs");
        var serviceProjectA = Path.Combine(serviceDirA, "A.csproj");

        await using var w = StartWatcher(testAsset, ["--non-interactive"], workingDirectory, hostProject);

        var waitingForChanges = w.Reporter.RegisterSemaphore(MessageDescriptor.WaitingForChanges);

        var changeHandled = w.Reporter.RegisterSemaphore(MessageDescriptor.HotReloadChangeHandled);
        var sessionStarted = w.Reporter.RegisterSemaphore(MessageDescriptor.HotReloadSessionStarted);

        // let the host process start:
        Log("Waiting for changes...");
        await waitingForChanges.WaitAsync(w.ShutdownSource.Token);

        // service should have been created before Hot Reload session started:
        Assert.NotNull(w.Service);

        var runningProject = await Launch(serviceProjectA, w.Service, workingDirectory, w.ShutdownSource.Token);
        Log("Waiting for session started ...");
        await sessionStarted.WaitAsync(w.ShutdownSource.Token);

        // Terminate the process:
        await w.Service.ProjectLauncher.TerminateProcessAsync(runningProject, CancellationToken.None);

        // rude edit in A (changing assembly level attribute):
        UpdateSourceFile(serviceSourceA2, """
            [assembly: System.Reflection.AssemblyMetadata("TestAssemblyMetadata", "2")]
            """);

        Log("Waiting for change handled ...");
        await changeHandled.WaitAsync(w.ShutdownSource.Token);

        w.Reporter.ProcessOutput.Contains("verbose ⌚ Rude edits detected but do not affect any running process");
        w.Reporter.ProcessOutput.Contains($"verbose ❌ {serviceSourceA2}(1,12): error ENC0003: Updating 'attribute' requires restarting the application.");
    }

    public enum DirectoryKind
    {
        Ordinary,
        Hidden,
        Bin,
        Obj,
    }

    [Theory]
    [CombinatorialData]
    public async Task IgnoredChange(bool isExisting, bool isIncluded, DirectoryKind directoryKind)
    {
        var testAsset = CopyTestAsset("WatchNoDepsApp", [isExisting, isIncluded, directoryKind]);

        var workingDirectory = testAsset.Path;
        string dir;

        switch (directoryKind)
        {
            case DirectoryKind.Bin:
                dir = Path.Combine(workingDirectory, "bin", "Debug", ToolsetInfo.CurrentTargetFramework);
                break;

            case DirectoryKind.Obj:
                dir = Path.Combine(workingDirectory, "obj", "Debug", ToolsetInfo.CurrentTargetFramework);
                break;

            case DirectoryKind.Hidden:
                dir = Path.Combine(workingDirectory, ".dir");
                break;

            default:
                dir = workingDirectory;
                break;
        }

        var extension = isIncluded ? ".cs" : ".txt";

        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, "File" + extension);

        if (isExisting)
        {
            File.WriteAllText(path, "class C { int F() => 1; }");

            if (isIncluded && directoryKind is DirectoryKind.Bin or DirectoryKind.Obj or DirectoryKind.Hidden)
            {
                var project = Path.Combine(workingDirectory, "WatchNoDepsApp.csproj");
                File.WriteAllText(project, File.ReadAllText(project).Replace(
                    "<!-- add item -->",
                    $"""
                    <Compile Include="{path}"/>
                    """));
            }
        }

        await using var w = StartWatcher(testAsset, ["--no-exit"], workingDirectory, workingDirectory);

        var waitingForChanges = w.Reporter.RegisterSemaphore(MessageDescriptor.WaitingForChanges);
        var changeHandled = w.Reporter.RegisterSemaphore(MessageDescriptor.HotReloadChangeHandled);
        var ignoringChangeInHiddenDirectory = w.Reporter.RegisterSemaphore(MessageDescriptor.IgnoringChangeInHiddenDirectory);
        var ignoringChangeInOutputDirectory = w.Reporter.RegisterSemaphore(MessageDescriptor.IgnoringChangeInOutputDirectory);
        var fileAdditionTriggeredReEvaluation = w.Reporter.RegisterSemaphore(MessageDescriptor.FileAdditionTriggeredReEvaluation);
        var noHotReloadChangesToApply = w.Reporter.RegisterSemaphore(MessageDescriptor.NoCSharpChangesToApply);

        Log("Waiting for changes...");
        await waitingForChanges.WaitAsync(w.ShutdownSource.Token);
        
        UpdateSourceFile(path, "class C { int F() => 2; }");

        switch ((isExisting, isIncluded, directoryKind))
        {
            case (isExisting: true, isIncluded: true, directoryKind: _):
                Log("Waiting for changed handled ...");
                await changeHandled.WaitAsync(w.ShutdownSource.Token);
                break;

            case (isExisting: true, isIncluded: false, directoryKind: DirectoryKind.Ordinary):
                Log("Waiting for no hot reload changes to apply ...");
                await noHotReloadChangesToApply.WaitAsync(w.ShutdownSource.Token);
                break;

            case (isExisting: false, isIncluded: _, directoryKind: DirectoryKind.Ordinary):
                Log("Waiting for file addition re-evalutation ...");
                await fileAdditionTriggeredReEvaluation.WaitAsync(w.ShutdownSource.Token);
                break;

            case (isExisting: _, isIncluded: _, directoryKind: DirectoryKind.Hidden):
                Log("Waiting for ignored change in hidden dir ...");
                await ignoringChangeInHiddenDirectory.WaitAsync(w.ShutdownSource.Token);
                break;

            case (isExisting: _, isIncluded: _, directoryKind: DirectoryKind.Bin or DirectoryKind.Obj):
                Log("Waiting for ignored change in output dir ...");
                await ignoringChangeInOutputDirectory.WaitAsync(w.ShutdownSource.Token);
                break;

            default:
                throw new InvalidOperationException();
        }
    }
}
