// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Utils.TestApp;

public static partial class ProcessLifecycleTestApp
{
    public static int Main(string[] args)
    {
        string childMode = args[0];
        string markerPath = args[1];

        if (childMode is "reaper-parent" or "uncooperative-reaper-parent")
        {
            RunReaperParent(args, markerPath, childMode);
            return 0;
        }

        if (childMode == "signal-child")
        {
            RunSignalChild(markerPath);
            return 0;
        }

        if (childMode == "ignoring-signal-child")
        {
            RunIgnoringSignalChild(markerPath);
            return 0;
        }

        if (OperatingSystem.IsWindows())
        {
            // A private console prevents test signals from reaching MTP or other processes.
            if (!FreeConsole() || !AllocConsole())
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            new Thread(() =>
            {
                for (int signal = 1; ; signal++)
                {
                    WaitForFile(markerPath + $".signal-{signal}");
                    if (!GenerateConsoleCtrlEvent(0, 0))
                    {
                        throw new Win32Exception(Marshal.GetLastPInvokeError());
                    }
                }
            }) { IsBackground = true }.Start();
        }

        CancellationToken cancellationToken = ProcessLifecycle.CancellationToken;
        using IDisposable? suppression = childMode == "suppressed"
            ? ProcessLifecycle.SuppressTerminationCancellation()
            : null;
        using CancellationTokenRegistration registration = childMode is "uncooperative" or "repeated"
            ? cancellationToken.Register(() => File.WriteAllText(markerPath, "signal-cancelled"))
            : default;
        if (childMode == "suppressed")
        {
            Console.CancelKeyPress += (_, _) => File.WriteAllText(markerPath, "signal-suppressed");
        }
        if (childMode == "repeated")
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => File.WriteAllText(markerPath + ".managed-exit", "");
        }

        File.WriteAllText(markerPath, "ready");

        if (childMode is "uncooperative" or "repeated")
        {
            Thread.Sleep(Timeout.Infinite);
        }

        if (childMode == "suppressed")
        {
            WaitForFile(markerPath + ".release");
            if (cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException("The suppression lease must preserve child signal ownership.");
            }
            return 0;
        }

        cancellationToken.WaitHandle.WaitOne();
        File.WriteAllText(
            markerPath,
            ProcessLifecycle.IsSignalTerminationRequested ? "signal-cancelled" : "cancelled");
        return 0;
    }

    private static void RunReaperParent(string[] args, string markerPath, string childMode)
    {
        using Process signalChild = CreateChildProcess(
            args,
            childMode == "reaper-parent" ? "signal-child" : "ignoring-signal-child");
        using ProcessReaper reaper = ProcessReaper.Create(signalChild);

        signalChild.Start();
        reaper.NotifyProcessStarted();
        WaitForMarker(markerPath, "grandchild-ready");
        File.WriteAllText(markerPath, "reaper-ready");
        Thread.Sleep(Timeout.Infinite);
    }

    private static Process CreateChildProcess(string[] args, string childMode)
    {
        ProcessStartInfo startInfo = new(args[2])
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--runtimeconfig");
        startInfo.ArgumentList.Add(args[4]);
        startInfo.ArgumentList.Add("--depsfile");
        startInfo.ArgumentList.Add(args[5]);
        startInfo.ArgumentList.Add(args[3]);
        startInfo.ArgumentList.Add(childMode);
        startInfo.ArgumentList.Add(args[1]);
        startInfo.ArgumentList.Add(args[2]);
        startInfo.ArgumentList.Add(args[3]);
        startInfo.ArgumentList.Add(args[4]);
        startInfo.ArgumentList.Add(args[5]);

        return new Process { StartInfo = startInfo };
    }

    private static void RunIgnoringSignalChild(string markerPath)
    {
        using PosixSignalRegistration registration = PosixSignalRegistration.Create(
            PosixSignal.SIGTERM,
            context => context.Cancel = true);

        File.WriteAllText(markerPath, "grandchild-ready");
        Thread.Sleep(Timeout.Infinite);
    }

    private static void RunSignalChild(string markerPath)
    {
        using PosixSignalRegistration registration = PosixSignalRegistration.Create(
            PosixSignal.SIGTERM,
            context =>
            {
                context.Cancel = true;
                File.WriteAllText(markerPath, "forwarded");
                Environment.Exit(43);
            });

        File.WriteAllText(markerPath, "grandchild-ready");
        Thread.Sleep(Timeout.Infinite);
    }

    private static void WaitForMarker(string markerPath, string expectedValue)
    {
        if (!SpinWait.SpinUntil(
                () => ReadMarker(markerPath) == expectedValue,
                TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException($"Timed out waiting for marker '{expectedValue}'.");
        }
    }

    private static string? ReadMarker(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using StreamReader reader = new(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        return reader.ReadToEnd();
    }

    private static void WaitForFile(string path)
    {
        if (!SpinWait.SpinUntil(() => File.Exists(path), TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException($"Timed out waiting for '{path}'.");
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllocConsole();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FreeConsole();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GenerateConsoleCtrlEvent(uint ctrlEvent, uint processGroupId);
}
