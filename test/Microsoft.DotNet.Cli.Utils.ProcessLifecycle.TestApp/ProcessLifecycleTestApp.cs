// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Utils.TestApp;

public static class ProcessLifecycleTestApp
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

        CancellationToken cancellationToken = ProcessLifecycle.CancellationToken;
        File.WriteAllText(markerPath, "ready");

        if (childMode == "uncooperative")
        {
            Thread.Sleep(Timeout.Infinite);
        }

        cancellationToken.WaitHandle.WaitOne();
        File.WriteAllText(markerPath, "cancelled");
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
                () => File.Exists(markerPath) && File.ReadAllText(markerPath) == expectedValue,
                TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException($"Timed out waiting for marker '{expectedValue}'.");
        }
    }
}
