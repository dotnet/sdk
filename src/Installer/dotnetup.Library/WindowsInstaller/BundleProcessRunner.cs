// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Tools.Bootstrapper.WindowsInstaller;

/// <summary>Runs a Windows installer bundle and waits for it to exit.</summary>
internal interface IBundleProcessRunner
{
    /// <summary>Runs the bundle to completion and returns its exit code.</summary>
    /// <param name="bundlePath">The installer bundle to run.</param>
    /// <param name="arguments">Command-line arguments for the bundle.</param>
    /// <param name="onCancelRequested">
    /// Invoked when the user asks to cancel (Ctrl+C) while the installer runs. The installer keeps running;
    /// the callback lets the caller avoid starting further work.
    /// </param>
    /// <exception cref="System.ComponentModel.Win32Exception">The process could not be started.</exception>
    int Run(string bundlePath, IReadOnlyList<string> arguments, Action onCancelRequested);
}

/// <summary>
/// Starts the bundle directly (no shell). The bundle's manifest requests <c>asInvoker</c>; the Burn engine
/// raises its own UAC prompt for the per-machine work, so dotnetup itself never elevates. There is no
/// timeout and Ctrl+C does not stop the installer: abandoning an installer mid-transaction can leave the
/// machine in a partially installed state, so dotnetup waits for the installer to finish and reports the
/// cancellation request to the caller instead.
/// </summary>
internal sealed class BundleProcessRunner : IBundleProcessRunner
{
    public int Run(string bundlePath, IReadOnlyList<string> arguments, Action onCancelRequested)
    {
        ArgumentNullException.ThrowIfNull(onCancelRequested);
        var startInfo = new ProcessStartInfo(bundlePath) { UseShellExecute = false };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        void WaitForInstaller(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            onCancelRequested();
            Console.Error.WriteLine("The .NET installer is still running and cannot be cancelled from dotnetup. Waiting for it to finish; no further installs will be started...");
        }

        Console.CancelKeyPress += WaitForInstaller;
        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"The installer '{bundlePath}' did not start.");
            process.WaitForExit();
            return process.ExitCode;
        }
        finally
        {
            Console.CancelKeyPress -= WaitForInstaller;
        }
    }
}