// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using Microsoft.NET.TestFramework.Commands;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class TerminationTests : DotNetWatchTestBase
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ForcedTermination_Windows_KillsDescendants(bool isUserApplication)
    {
        var testAsset = CopyTestAsset("WatchHotReloadApp", [isUserApplication]);
        UpdateSourceFile(Path.Combine(testAsset.Path, "Program.cs"), """
            using System;
            using System.Diagnostics;
            using System.Threading;

            Console.CancelKeyPress += (_, e) => e.Cancel = true;

            if (args.Length == 0)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath,
                    Arguments = "child",
                    UseShellExecute = false,
                });
            }
            else if (args[0] == "child")
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath,
                    Arguments = "grandchild",
                    UseShellExecute = false,
                });
                Console.WriteLine($"Child: {Environment.ProcessId}");
            }
            else
            {
                Console.WriteLine($"Grandchild: {Environment.ProcessId}");
            }

            Thread.Sleep(Timeout.Infinite);
            """);

        new DotnetBuildCommand(Logger, testAsset.Path)
            .Execute()
            .Should().Pass();

        var childStarted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var grandchildStarted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processSpec = new ProcessSpec
        {
            Executable = Path.Combine(testAsset.Path, "bin", "Debug", ToolsetInfo.CurrentTargetFramework, "WatchHotReloadApp.exe"),
            WorkingDirectory = testAsset.Path,
            IsUserApplication = isUserApplication,
            OnOutput = line =>
            {
                if (line.Content.StartsWith("Child: ", StringComparison.Ordinal))
                {
                    childStarted.TrySetResult(int.Parse(line.Content["Child: ".Length..]));
                }
                else if (line.Content.StartsWith("Grandchild: ", StringComparison.Ordinal))
                {
                    grandchildStarted.TrySetResult(int.Parse(line.Content["Grandchild: ".Length..]));
                }
            },
        };
        SdkTestContext.Current.AddTestEnvironmentVariables(processSpec.EnvironmentVariables);

        using var terminationSource = new CancellationTokenSource();
        var runner = new ProcessRunner(processCleanupTimeout: TimeSpan.FromSeconds(1));
        var runTask = runner.RunAsync(processSpec, new TestLogger(Logger), launchResult: null, terminationSource.Token);
        Process child = null;
        Process grandchild = null;
        try
        {
            var childId = await childStarted.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
            child = Process.GetProcessById(childId);
            var grandchildId = await grandchildStarted.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
            grandchild = Process.GetProcessById(grandchildId);
            terminationSource.Cancel();

            await runTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
            Assert.IsTrue(child.WaitForExit(10_000), "Force termination must not leave the child process running.");
            Assert.IsTrue(grandchild.WaitForExit(10_000), "Force termination must not leave the grandchild process running.");
        }
        finally
        {
            terminationSource.Cancel();
            try
            {
                await runTask.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
            }
            finally
            {
                foreach (var process in new[] { child, grandchild })
                {
                    if (process != null)
                    {
                        try
                        {
                            if (!process.HasExited)
                            {
                                process.Kill(entireProcessTree: true);
                                await process.WaitForExitAsync(CancellationToken.None);
                            }
                        }
                        finally
                        {
                            process.Dispose();
                        }
                    }
                }
            }
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task GracefulTermination_Windows()
    {
        var tfm = ToolsetInfo.CurrentTargetFramework;

        var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
           .WithSource();

        var programPath = Path.Combine(testAsset.Path, "Program.cs");

        UpdateSourceFile(programPath, src => src.Replace("// <metadata update handler placeholder>", """
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("Ctrl+C detected! Performing cleanup...");
                Environment.Exit(0);
            };
            """));

        App.Start(testAsset, [], testFlags: TestFlags.ReadKeyFromStdin);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        await App.WaitUntilOutputContains($"dotnet watch 🕵️ [WatchHotReloadApp ({tfm})] Windows Ctrl+C handling enabled.");

        await App.WaitUntilOutputContains("Started");

        App.SendControlC();

        await App.WaitUntilOutputContains("Ctrl+C detected! Performing cleanup...");
        await App.WaitUntilOutputContains("exited with exit code 0.");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task GracefulTermination_Unix()
    {
        var tfm = ToolsetInfo.CurrentTargetFramework;

        var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
           .WithSource();

        var programPath = Path.Combine(testAsset.Path, "Program.cs");

        UpdateSourceFile(programPath, src => src.Replace("// <metadata update handler placeholder>", """
            using var termSignalRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ =>
            {
                Console.WriteLine("SIGTERM detected! Performing cleanup...");
            });
            """));

        App.Start(testAsset, [], testFlags: TestFlags.ReadKeyFromStdin);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        await App.WaitUntilOutputContains($"dotnet watch 🕵️ [WatchHotReloadApp ({tfm})] Posix signal handlers registered.");

        await App.WaitUntilOutputContains("Started");

        App.SendControlC();

        await App.WaitUntilOutputContains("SIGTERM detected! Performing cleanup...");
        await App.WaitUntilOutputContains("exited with exit code 0.");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task GracefulTermination_WinExe()
    {
        // Test that WinExe apps (WinForms, WPF, MAUI) are terminated gracefully when dotnet-watch
        // receives Ctrl+C. The `dotnet run` process receives a targeted Ctrl+Break and calls CloseMainWindow() on the
        // WinForms app. See https://github.com/dotnet/sdk/issues/52473

        var testAsset = TestAssets.CopyTestAsset("WinExeApp")
           .WithSource();

        App.Start(testAsset, [], testFlags: TestFlags.ReadKeyFromStdin);

        await App.WaitForOutputLineContaining(MessageDescriptor.WaitingForChanges);

        // Wait for the WinForms app to start and show its window
        await App.WaitUntilOutputContains("Started");

        App.SendControlC();

        // The app should close gracefully via CloseMainWindow
        await App.WaitForOutputLineContaining("Closing gracefully: True");

        // The dotnet run process should exit with code 0, not be force-killed after timeout
        await App.WaitUntilOutputContains("exited with exit code 0.");
        App.AssertOutputDoesNotContain("(Kill)");
    }
}
