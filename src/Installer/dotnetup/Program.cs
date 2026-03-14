// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class DotnetupProgram
{
    public static int Main(string[] args)
    {
        // Handle --debug flag using the standard .NET SDK pattern
        // This is DEBUG-only and removes the --debug flag from args
        DotnetupDebugHelper.HandleDebugSwitch(ref args);

        // Ensure UTF-8 output so Unicode glyphs (spinners, progress bars) render correctly.
        // Follows the same pattern as the .NET SDK CLI (src/Cli/dotnet/Program.cs).
        ConfigureConsoleEncoding();

        // Disable Spectre.Console line wrapping when output is redirected (piped),
        // since wrapping is not useful for non-interactive consumers.
        if (Console.IsOutputRedirected)
        {
            AnsiConsole.Profile.Width = int.MaxValue;
        }

        // Set up callback to notify user when waiting for another dotnetup process
        ScopedMutex.OnWaitingForMutex = () =>
        {
            Console.WriteLine("Another dotnetup process is running. Waiting for it to finish...");
        };

        // Show first-run telemetry notice if needed
        FirstRunNotice.ShowIfFirstRun(DotnetupTelemetry.Instance.Enabled);

        // Start root activity for the entire process
        using var rootActivity = DotnetupTelemetry.Instance.Enabled
            ? DotnetupTelemetry.CommandSource.StartActivity("dotnetup", ActivityKind.Internal)
            : null;

        try
        {
            var exitCode = InvokeParser(args, rootActivity);

            return exitCode;
        }
        catch (Exception ex)
        {
            // Catch-all for unhandled exceptions
            DotnetupTelemetry.Instance.RecordException(rootActivity, ex);
            rootActivity?.SetTag(TelemetryTagNames.ExitCode, 1);

            // Log the error and return non-zero exit code
            Console.Error.WriteLine($"Error: {ex.Message}");
#if DEBUG
            Console.Error.WriteLine(ex.StackTrace);
#endif
            return 1;
        }
        finally
        {
            // Stop the root activity before disposing so the console/Azure Monitor
            // exporters see it.  The 'using' dispose that follows is a no-op on
            // an already-stopped Activity.
            rootActivity?.Stop();
            DisposeTelemetry();
        }
    }

    private static int InvokeParser(string[] args, Activity? rootActivity)
    {
        var result = Parser.Invoke(args);
        rootActivity?.SetTag(TelemetryTagNames.ExitCode, result);
        rootActivity?.SetStatus(result == 0 ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        return result;
    }

    private static void DisposeTelemetry()
    {
        // The Azure Monitor exporter has built-in offline storage
        // (%LOCALAPPDATA%\Microsoft\AzureMonitor) so unsent telemetry
        // survives process exit and is retried on the next run.
        // Dispose on a background thread with a short timeout so we
        // never block the user waiting for a network round-trip.
        // This mirrors the pattern used by the .NET CLI, which writes
        // telemetry to disk and sends it asynchronously.
        try
        {
            Task.Run(DotnetupTelemetry.Instance.Dispose).Wait(TimeSpan.FromMilliseconds(100));
        }
        catch
        {
            // Telemetry should never delay or crash the process exit.
        }
    }

    /// <summary>
    /// Sets the console output encoding to UTF-8 so Unicode glyphs render correctly.
    /// Mirrors the logic in the .NET SDK CLI (src/Cli/dotnet/Program.cs) and
    /// UILanguageOverride.OperatingSystemSupportsUtf8().
    /// </summary>
    private static void ConfigureConsoleEncoding()
    {
        if (Environment.GetEnvironmentVariable("DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING") == "1")
        {
            return;
        }

        if (!OperatingSystem.IsIOS() &&
            !OperatingSystem.IsAndroid() &&
            !OperatingSystem.IsTvOS() &&
            !OperatingSystem.IsBrowser() &&
            (!OperatingSystem.IsWindows() || OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18363)))
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
    }
}
