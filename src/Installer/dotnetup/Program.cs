// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class DotnetupProgram
{
    public static int Main(string[] args)
    {
        // Handle --debug flag using the standard .NET SDK pattern
        // This is DEBUG-only and removes the --debug flag from args
        DotnetupDebugHelper.HandleDebugSwitch(ref args);

        // Show first-run telemetry notice if needed
        FirstRunNotice.ShowIfFirstRun(DotnetupTelemetry.Instance.Enabled);

        // Start root activity for the entire process
        using var rootActivity = DotnetupTelemetry.Instance.Enabled
            ? DotnetupTelemetry.CommandSource.StartActivity("dotnetup", ActivityKind.Internal)
            : null;

        try
        {
            var result = Parser.Invoke(args);
            rootActivity?.SetTag("exit.code", result);
            rootActivity?.SetStatus(result == 0 ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            return result;
        }
        catch (Exception ex)
        {
            // Catch-all for unhandled exceptions
            DotnetupTelemetry.Instance.RecordException(rootActivity, ex);
            rootActivity?.SetTag("exit.code", 1);

            // Re-throw to preserve original behavior (or handle as appropriate)
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
