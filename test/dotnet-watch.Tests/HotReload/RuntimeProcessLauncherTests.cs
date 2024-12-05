// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tests;

public class RuntimeProcessLauncherTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    public enum TriggerEvent
    {
        HotReloadSessionStarting,
        HotReloadSessionStarted,
        WaitingForChanges,
    }

    private static async Task<RunningProject> Launch(string projectPath, TestRuntimeProcessLauncher service, string workingDirectory, CancellationToken cancellationToken)
    {
        var projectOptions = new ProjectOptions()
        {
            IsRootProject = false,
            ProjectPath = projectPath,
            WorkingDirectory = workingDirectory,
            BuildProperties = [],
            Command = "run",
            CommandArguments = ["--project", projectPath],
            LaunchEnvironmentVariables = [],
            LaunchProfileName = null,
            NoLaunchProfile = true,
            TargetFramework = null,
        };

        RestartOperation? startOp = null;
        startOp = new RestartOperation(async (build, cancellationToken) =>
        {
            var result = await service.ProjectLauncher.TryLaunchProcessAsync(
                projectOptions,
                new CancellationTokenSource(),
                onOutput: null,
                restartOperation: startOp!,
                build,
                cancellationToken);

            Assert.NotNull(result);

            await result.WaitForProcessRunningAsync(cancellationToken);

            return result;
        });

        return await startOp(build: false, cancellationToken);
    }

    [Theory(Skip="https://github.com/dotnet/sdk/issues/42850")]
    [CombinatorialData]
    public async Task UpdateAndRudeEdit(TriggerEvent trigger)
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppMultiProc", identifier: trigger.ToString())
            .WithSource();

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

        var console = new TestConsole(Logger);
        var reporter = new TestReporter(Logger);

        var program = Program.TryCreate(
            TestOptions.GetCommandLineOptions(["--verbose", "--non-interactive", "--project", hostProject]),
            console,
            TestOptions.GetEnvironmentOptions(workingDirectory, TestContext.Current.ToolsetUnderTest.DotNetHostPath),
            reporter,
            out var errorCode);

        Assert.Equal(0, errorCode);
        Assert.NotNull(program);

        TestRuntimeProcessLauncher? service = null;
        var factory = new TestRuntimeProcessLauncher.Factory(s =>
        {
            service = s;
        });

        var watcher = Assert.IsType<HotReloadDotNetWatcher>(program.CreateWatcher(factory));

        var watchCancellationSource = new CancellationTokenSource();
        var watchTask = watcher.WatchAsync(watchCancellationSource.Token);

        var launchCompletionA = new TaskCompletionSource();
        var launchCompletionB = new TaskCompletionSource();

        reporter.RegisterAction(trigger switch
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
            Assert.NotNull(service);

            Launch(serviceProjectA, service, workingDirectory, watchCancellationSource.Token).Wait();
            launchCompletionA.TrySetResult();

            Launch(serviceProjectB, service, workingDirectory, watchCancellationSource.Token).Wait();
            launchCompletionB.TrySetResult();
        });

        var waitingForChanges = reporter.RegisterSemaphore(MessageDescriptor.WaitingForChanges);

        var launchedProcessCount = 0;
        reporter.RegisterAction(MessageDescriptor.LaunchedProcess, () => Interlocked.Increment(ref launchedProcessCount));

        var changeHandled = reporter.RegisterSemaphore(MessageDescriptor.HotReloadChangeHandled);
        var sessionStarted = reporter.RegisterSemaphore(MessageDescriptor.HotReloadSessionStarted);

        await launchCompletionA.Task;
        await launchCompletionB.Task;

        // let the host process start:
        await waitingForChanges.WaitAsync();
        await sessionStarted.WaitAsync();

        await MakeRudeEditChange();
        await changeHandled.WaitAsync();

        // Wait for a new session to start, so that we capture the new solution snapshot
        // and further changes are treated as another update.
        await sessionStarted.WaitAsync();

        await MakeValidDependencyChange();
        await changeHandled.WaitAsync();

        // clean up:
        watchCancellationSource.Cancel();
        try
        {
            await watchTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Equal(4, launchedProcessCount);

        // Hot Reload shared dependency - should update both service projects
        async Task MakeValidDependencyChange()
        {
            var hasUpdateSourceA = new TaskCompletionSource();
            var hasUpdateSourceB = new TaskCompletionSource();
            reporter.OnProjectProcessOutput += (projectPath, line) =>
            {
                if (line.Content.Contains("<Updated Lib>"))
                {
                    if (projectPath == serviceProjectA)
                    {
                        if (!hasUpdateSourceA.Task.IsCompleted)
                        {
                            hasUpdateSourceA.SetResult();
                        }
                    }
                    else if (projectPath == serviceProjectB)
                    {
                        if (!hasUpdateSourceB.Task.IsCompleted)
                        {
                            hasUpdateSourceB.SetResult();
                        }
                    }
                    else
                    {
                        Assert.Fail("Only service projects should be updated");
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

            await hasUpdateSourceA.Task;
            await hasUpdateSourceB.Task;

            Assert.True(hasUpdateSourceA.Task.IsCompletedSuccessfully);
            Assert.True(hasUpdateSourceB.Task.IsCompletedSuccessfully);
        }

        // make a rude edit and check that the process is restarted
        async Task MakeRudeEditChange()
        {
            var hasUpdateSource = new TaskCompletionSource();
            reporter.OnProjectProcessOutput += (projectPath, line) =>
            {
                if (projectPath == serviceProjectA && line.Content.Contains("Started A: 2"))
                {
                    hasUpdateSource.SetResult();
                }
            };

            await Task.Delay(TimeSpan.FromSeconds(1));

            // rude edit in A (changing assembly level attribute):
            UpdateSourceFile(serviceSourceA2, """
                [assembly: System.Reflection.AssemblyMetadata("TestAssemblyMetadata", "2")]
                """);

            await hasUpdateSource.Task;

            Assert.True(hasUpdateSource.Task.IsCompletedSuccessfully);

            Assert.Equal(4, launchedProcessCount);
        }
    }

    [Theory]
    [CombinatorialData]
    public async Task UpdateAppliedToNewProcesses(bool sharedOutput)
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppMultiProc", identifier: sharedOutput.ToString())
            .WithSource();

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

        var console = new TestConsole(Logger);
        var reporter = new TestReporter(Logger);

        var program = Program.TryCreate(
            TestOptions.GetCommandLineOptions(["--verbose", "--non-interactive", "--project", hostProject]),
            console,
            TestOptions.GetEnvironmentOptions(workingDirectory, TestContext.Current.ToolsetUnderTest.DotNetHostPath),
            reporter,
            out var errorCode);

        Assert.Equal(0, errorCode);
        Assert.NotNull(program);

        TestRuntimeProcessLauncher? service = null;
        var factory = new TestRuntimeProcessLauncher.Factory(s =>
        {
            service = s;
        });

        var watcher = Assert.IsType<HotReloadDotNetWatcher>(program.CreateWatcher(factory));

        var watchCancellationSource = new CancellationTokenSource();
        var watchTask = watcher.WatchAsync(watchCancellationSource.Token);

        var waitingForChanges = reporter.RegisterSemaphore(MessageDescriptor.WaitingForChanges);
        var changeHandled = reporter.RegisterSemaphore(MessageDescriptor.HotReloadChangeHandled);
        var updatesApplied = reporter.RegisterSemaphore(MessageDescriptor.UpdatesApplied);

        var hasUpdateA = new SemaphoreSlim(initialCount: 0);
        var hasUpdateB = new SemaphoreSlim(initialCount: 0);
        reporter.OnProjectProcessOutput += (projectPath, line) =>
        {
            if (line.Content.Contains("<Updated Lib>"))
            {
                if (projectPath == serviceProjectA)
                {
                    hasUpdateA.Release();
                }
                else if (projectPath == serviceProjectB)
                {
                    hasUpdateB.Release();
                }
                else
                {
                    Assert.Fail("Only service projects should be updated");
                }
            }
        };

        await Task.Delay(TimeSpan.FromSeconds(1));

        // let the host process start:
        await waitingForChanges.WaitAsync();

        // service should have been created before Hot Reload session started:
        Assert.NotNull(service);

        await Launch(serviceProjectA, service, workingDirectory, watchCancellationSource.Token);

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

        await hasUpdateA.WaitAsync();

        // Host and ServiceA received updates:
        await updatesApplied.WaitAsync();
        await updatesApplied.WaitAsync();

        await Launch(serviceProjectB, service, workingDirectory, watchCancellationSource.Token);

        // ServiceB received updates:
        await updatesApplied.WaitAsync();
        await hasUpdateB.WaitAsync();

        // clean up:
        watchCancellationSource.Cancel();
        try
        {
            await watchTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    public enum UpdateLocation
    {
        Dependency,
        TopLevel,
        TopFunction,
    }

    [Theory(Skip="https://github.com/dotnet/sdk/issues/42850")]
    [CombinatorialData]
    public async Task HostRestart(UpdateLocation updateLocation)
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppMultiProc", identifier: updateLocation.ToString())
            .WithSource();

        var workingDirectory = testAsset.Path;
        var hostDir = Path.Combine(testAsset.Path, "Host");
        var hostProject = Path.Combine(hostDir, "Host.csproj");
        var hostProgram = Path.Combine(hostDir, "Program.cs");
        var libProject = Path.Combine(testAsset.Path, "Lib2", "Lib2.csproj");
        var lib = Path.Combine(testAsset.Path, "Lib2", "Lib2.cs");

        var console = new TestConsole(Logger);
        var reporter = new TestReporter(Logger);

        var program = Program.TryCreate(
            TestOptions.GetCommandLineOptions(["--verbose", "--project", hostProject]),
            console,
            TestOptions.GetEnvironmentOptions(workingDirectory, TestContext.Current.ToolsetUnderTest.DotNetHostPath),
            reporter,
            out var errorCode);

        Assert.Equal(0, errorCode);
        Assert.NotNull(program);

        TestRuntimeProcessLauncher? service = null;
        var factory = new TestRuntimeProcessLauncher.Factory(s =>
        {
            service = s;
        });

        var watcher = Assert.IsType<HotReloadDotNetWatcher>(program.CreateWatcher(factory));

        var watchCancellationSource = new CancellationTokenSource();
        var watchTask = watcher.WatchAsync(watchCancellationSource.Token);

        var waitingForChanges = reporter.RegisterSemaphore(MessageDescriptor.WaitingForChanges);
        var changeHandled = reporter.RegisterSemaphore(MessageDescriptor.HotReloadChangeHandled);
        var restartNeeded = reporter.RegisterSemaphore(MessageDescriptor.ApplyUpdate_ChangingEntryPoint);
        var restartRequested = reporter.RegisterSemaphore(MessageDescriptor.RestartRequested);

        var hasUpdate = new SemaphoreSlim(initialCount: 0);
        reporter.OnProjectProcessOutput += (projectPath, line) =>
        {
            if (line.Content.Contains("<Updated>"))
            {
                if (projectPath == hostProject)
                {
                    hasUpdate.Release();
                }
                else
                {
                    Assert.Fail("Only service projects should be updated");
                }
            }
        };

        await Task.Delay(TimeSpan.FromSeconds(1));

        // let the host process start:
        await waitingForChanges.WaitAsync();

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
                await changeHandled.WaitAsync();
                break;

            case UpdateLocation.TopFunction:
                // Update top-level function body:
                UpdateSourceFile(hostProgram, content => content.Replace("Waiting", "<Updated>"));

                // Host received Hot Reload updates:
                await changeHandled.WaitAsync();
                break;

            case UpdateLocation.TopLevel:
                // Update top-level code that does not get reloaded until the app restarts:
                UpdateSourceFile(hostProgram, content => content.Replace("Started", "<Updated>"));

                // ⚠ ENC0118: Changing 'top-level code' might not have any effect until the application is restarted. Press "Ctrl + R" to restart.
                await restartNeeded.WaitAsync();

                console.PressKey(new ConsoleKeyInfo('R', ConsoleKey.R, shift: false, alt: false, control: true));

                await restartRequested.WaitAsync();
                break;
        }

        await hasUpdate.WaitAsync();

        // clean up:
        watchCancellationSource.Cancel();
        try
        {
            await watchTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Fact]
    public async Task RudeEditInProjectWithoutRunningProcess()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppMultiProc")
            .WithSource();

        var workingDirectory = testAsset.Path;
        var hostDir = Path.Combine(testAsset.Path, "Host");
        var hostProject = Path.Combine(hostDir, "Host.csproj");
        var serviceDirA = Path.Combine(testAsset.Path, "ServiceA");
        var serviceSourceA2 = Path.Combine(serviceDirA, "A2.cs");
        var serviceProjectA = Path.Combine(serviceDirA, "A.csproj");

        var console = new TestConsole(Logger);
        var reporter = new TestReporter(Logger);

        var program = Program.TryCreate(
            TestOptions.GetCommandLineOptions(["--verbose", "--non-interactive", "--project", hostProject]),
            console,
            TestOptions.GetEnvironmentOptions(workingDirectory, TestContext.Current.ToolsetUnderTest.DotNetHostPath),
            reporter,
            out var errorCode);

        Assert.Equal(0, errorCode);
        Assert.NotNull(program);

        TestRuntimeProcessLauncher? service = null;
        var factory = new TestRuntimeProcessLauncher.Factory(s =>
        {
            service = s;
        });

        var watcher = Assert.IsType<HotReloadDotNetWatcher>(program.CreateWatcher(factory));

        var watchCancellationSource = new CancellationTokenSource();
        var watchTask = watcher.WatchAsync(watchCancellationSource.Token);

        var waitingForChanges = reporter.RegisterSemaphore(MessageDescriptor.WaitingForChanges);

        var changeHandled = reporter.RegisterSemaphore(MessageDescriptor.HotReloadChangeHandled);
        var sessionStarted = reporter.RegisterSemaphore(MessageDescriptor.HotReloadSessionStarted);

        // let the host process start:
        await waitingForChanges.WaitAsync();

        // service should have been created before Hot Reload session started:
        Assert.NotNull(service);

        var runningProject = await Launch(serviceProjectA, service, workingDirectory, watchCancellationSource.Token);
        await sessionStarted.WaitAsync();

        // Terminate the process:
        await service.ProjectLauncher.TerminateProcessAsync(runningProject, CancellationToken.None);

        // rude edit in A (changing assembly level attribute):
        UpdateSourceFile(serviceSourceA2, """
            [assembly: System.Reflection.AssemblyMetadata("TestAssemblyMetadata", "2")]
            """);

        await changeHandled.WaitAsync();

        reporter.ProcessOutput.Contains("verbose ⌚ Rude edits detected but do not affect any running process");
        reporter.ProcessOutput.Contains($"verbose ❌ {serviceSourceA2}(1,12): error ENC0003: Updating 'attribute' requires restarting the application.");

        // clean up:
        watchCancellationSource.Cancel();
        try
        {
            await watchTask;
        }
        catch (OperationCanceledException)
        {
        }
    }
}
