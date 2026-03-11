// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

public class RuntimeProcessLauncherTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    public enum TriggerEvent
    {
        HotReloadSessionStarting,
        HotReloadSessionStarted,
        WaitingForChanges,
    }

    [Theory]
    [CombinatorialData]
    public async Task UpdateAndRudeEdit(TriggerEvent trigger)
    {
        var testAsset = CopyTestAsset("WatchAppMultiProc", [trigger]);

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

        await using var w = CreateInProcWatcher(testAsset, ["--non-interactive", "--project", hostProject], workingDirectory);

        var launchCompletionA = w.CreateCompletionSource();
        var launchCompletionB = w.CreateCompletionSource();

        w.Observer.RegisterAction(trigger switch
        {
            TriggerEvent.HotReloadSessionStarting => MessageDescriptor.HotReloadSessionStartingNotification,
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

            w.Service.Launch(serviceProjectA, workingDirectory, w.ShutdownSource.Token).Wait();
            launchCompletionA.TrySetResult();

            w.Service.Launch(serviceProjectB, workingDirectory, w.ShutdownSource.Token).Wait();
            launchCompletionB.TrySetResult();
        });

        var waitingForChanges = w.Observer.RegisterSemaphore(MessageDescriptor.WaitingForChanges);

        var changeHandled = w.Observer.RegisterSemaphore(MessageDescriptor.ManagedCodeChangesApplied);
        var projectsRestarted = w.Observer.RegisterSemaphore(MessageDescriptor.ProjectsRestarted);
        var sessionStarted = w.Observer.RegisterSemaphore(MessageDescriptor.HotReloadSessionStarted);
        var projectBaselinesUpdated = w.Observer.RegisterSemaphore(MessageDescriptor.ProjectsRebuilt);

        w.Start();

        await launchCompletionA.Task;
        await launchCompletionB.Task;

        // let the host process start:
        Log("Waiting for changes...");
        await waitingForChanges.WaitAsync(w.ShutdownSource.Token);

        Log("Waiting for session started...");
        await sessionStarted.WaitAsync(w.ShutdownSource.Token);

        await MakeRudeEditChange();

        Log("Waiting for projects restarted ...");
        await projectsRestarted.WaitAsync(w.ShutdownSource.Token);

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
        var testAsset = CopyTestAsset("WatchAppMultiProc", [sharedOutput]);
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
        var libSource = Path.Combine(libDir, "Lib.cs");

        await using var w = CreateInProcWatcher(testAsset, ["--non-interactive", "--project", hostProject], workingDirectory);

        var waitingForChanges = w.Observer.RegisterSemaphore(MessageDescriptor.WaitingForChanges);
        var changeHandled = w.Observer.RegisterSemaphore(MessageDescriptor.ManagedCodeChangesApplied);
        var updatesApplied = w.Observer.RegisterSemaphore(MessageDescriptor.UpdateBatchCompleted);

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

        w.Start();

        // let the host process start:
        Log("Waiting for changes...");
        await waitingForChanges.WaitAsync(w.ShutdownSource.Token);

        // service should have been created before Hot Reload session started:
        Assert.NotNull(w.Service);

        await w.Service.Launch(serviceProjectA, workingDirectory, w.ShutdownSource.Token);

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

        await w.Service.Launch(serviceProjectB, workingDirectory, w.ShutdownSource.Token);

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
        var testAsset = CopyTestAsset("WatchAppMultiProc", [updateLocation]);
        var tfm = ToolsetInfo.CurrentTargetFramework;

        var workingDirectory = testAsset.Path;
        var hostDir = Path.Combine(testAsset.Path, "Host");
        var hostProject = Path.Combine(hostDir, "Host.csproj");
        var hostProgram = Path.Combine(hostDir, "Program.cs");
        var lib = Path.Combine(testAsset.Path, "Lib2", "Lib2.cs");

        await using var w = CreateInProcWatcher(testAsset, args: ["--project", hostProject], workingDirectory);

        var waitingForChanges = w.Observer.RegisterSemaphore(MessageDescriptor.WaitingForChanges);
        var changeHandled = w.Observer.RegisterSemaphore(MessageDescriptor.ManagedCodeChangesApplied);
        var restartNeeded = w.Observer.RegisterSemaphore(MessageDescriptor.ApplyUpdate_ChangingEntryPoint);
        var restartRequested = w.Observer.RegisterSemaphore(MessageDescriptor.RestartRequested);

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

        w.Start();

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

        await using var w = CreateInProcWatcher(testAsset, ["--non-interactive", "--project", hostProject], workingDirectory);

        var waitingForChanges = w.Observer.RegisterSemaphore(MessageDescriptor.WaitingForChanges);

        var projectsRebuilt = w.Observer.RegisterSemaphore(MessageDescriptor.ProjectsRebuilt);
        var sessionStarted = w.Observer.RegisterSemaphore(MessageDescriptor.HotReloadSessionStarted);
        var applyUpdateVerbose = w.Observer.RegisterSemaphore(MessageDescriptor.ApplyUpdate_AutoVerbose);

        w.Start();

        // let the host process start:
        Log("Waiting for changes...");
        await waitingForChanges.WaitAsync(w.ShutdownSource.Token);

        // service should have been created before Hot Reload session started:
        Assert.NotNull(w.Service);

        var runningProject = await w.Service.Launch(serviceProjectA, workingDirectory, w.ShutdownSource.Token);
        Log("Waiting for session started ...");
        await sessionStarted.WaitAsync(w.ShutdownSource.Token);

        // Terminate the process:
        Log($"Terminating process {runningProject.ProjectNode.GetDisplayName()} ...");
        await runningProject.Process.TerminateAsync();

        // rude edit in A (changing assembly level attribute):
        UpdateSourceFile(serviceSourceA2, """
            [assembly: System.Reflection.AssemblyMetadata("TestAssemblyMetadata", "2")]
            """);

        Log("Waiting for projects rebuilt ...");
        await projectsRebuilt.WaitAsync(w.ShutdownSource.Token);

        Log("Waiting for verbose rude edit reported ...");
        await applyUpdateVerbose.WaitAsync(w.ShutdownSource.Token);
    }

    [Fact]
    public async Task RelaunchOnCrash()
    {
        var testAsset = CopyTestAsset("WatchAppMultiProc");

        var workingDirectory = testAsset.Path;
        var hostDir = Path.Combine(testAsset.Path, "Host");
        var hostProject = Path.Combine(hostDir, "Host.csproj");
        var serviceDirA = Path.Combine(testAsset.Path, "ServiceA");
        var serviceProjectA = Path.Combine(serviceDirA, "A.csproj");
        var libDir = Path.Combine(testAsset.Path, "Lib");
        var libSource = Path.Combine(libDir, "Lib.cs");

        await using var w = CreateInProcWatcher(testAsset, ["--project", hostProject], workingDirectory);

        var waitingForChanges = w.Observer.RegisterSemaphore(MessageDescriptor.WaitingForChanges);
        var processCrashedAndWillBeRelaunched = w.Observer.RegisterSemaphore(MessageDescriptor.ProcessCrashedAndWillBeRelaunched);
        var projectRelaunched = w.Observer.RegisterSemaphore(MessageDescriptor.ProjectRelaunched);

        var hasCrashed = new SemaphoreSlim(initialCount: 0);
        var hasUpdate = new SemaphoreSlim(initialCount: 0);
        w.Reporter.OnProcessOutput += line =>
        {
            if (line.Content.Contains("<Crashed>"))
            {
                hasCrashed.Release();
            }
            else if (line.Content.Contains("<Updated>"))
            {
                hasUpdate.Release();
            }
        };

        w.Start();

        // let the host process start:
        Log("Waiting for changes...");
        await waitingForChanges.WaitAsync(w.ShutdownSource.Token);

        // service should have been created before Hot Reload session started:
        Assert.NotNull(w.Service);

        await w.Service.Launch(serviceProjectA, workingDirectory, w.ShutdownSource.Token);

        UpdateSourceFile(libSource, """
            using System;

            public class Lib
            {
                public static void Common()
                    => throw new Exception("<Crashed>");
            }
            """);

        Log("Waiting <Crashed> in output ...");
        await hasCrashed.WaitAsync(w.ShutdownSource.Token);

        Log("Waiting for process crashed ...");
        await processCrashedAndWillBeRelaunched.WaitAsync(w.ShutdownSource.Token);

        // file change triggers relaunch:
        UpdateSourceFile(libSource,"""
            using System;

            public class Lib
            {
                public static void Common()
                    => Console.WriteLine("<Updated>");
            }
            """);

        Log("Waiting for A to relaunch ...");
        await projectRelaunched.WaitAsync(w.ShutdownSource.Token);

        Log("Waiting for <Updated> in output ...");
        await hasUpdate.WaitAsync(w.ShutdownSource.Token);
    }
}
