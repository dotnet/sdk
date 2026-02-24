// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class DotnetupProgram
{
    public static int Main(string[] args)
    {
        // Handle --debug flag using the standard .NET SDK pattern
        // This is DEBUG-only and removes the --debug flag from args
        DotnetupDebugHelper.HandleDebugSwitch(ref args);

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
            var result = Parser.Invoke(args);
            rootActivity?.SetTag(TelemetryTagNames.ExitCode, result);
            rootActivity?.SetStatus(result == 0 ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            return result;
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
            // Ensure telemetry is flushed before exit
            DotnetupTelemetry.Instance.Flush();
            DotnetupTelemetry.Instance.Dispose();
        }
    }
}
