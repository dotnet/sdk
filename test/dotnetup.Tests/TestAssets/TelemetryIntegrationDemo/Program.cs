// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace TelemetryIntegrationDemo;

/// <summary>
/// Minimal example showing how to capture telemetry activities from Microsoft.Dotnet.Installation.
///
/// NOTE: If you collect telemetry in production, you are responsible for:
/// - Displaying a first-run notice to users
/// - Honoring DOTNET_CLI_TELEMETRY_OPTOUT
/// - Providing telemetry documentation
/// See: https://learn.microsoft.com/dotnet/core/tools/telemetry
/// </summary>
public static class Program
{
    private const string InstallationActivitySourceName = "Microsoft.Dotnet.Installation";

    public static void Main(string[] args)
    {
        // Set up ActivityListener to capture activities from the library
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == InstallationActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity =>
            {
                // Add your tool's identifier
                activity.SetTag("caller", "MyTool");
                Console.WriteLine($"Activity started: {activity.DisplayName}");
            },
            ActivityStopped = activity =>
            {
                Console.WriteLine($"Activity stopped: {activity.DisplayName}, Duration: {activity.Duration.TotalMilliseconds:F1}ms");
                foreach (var tag in activity.Tags)
                {
                    Console.WriteLine($"  {tag.Key}: {tag.Value}");
                }
            }
        };
        ActivitySource.AddActivityListener(listener);

        Console.WriteLine("Listener attached. Use the library to see activities captured.");

        // Example: Use InstallerFactory.Create() and perform installations
        // Activities will be automatically captured by the listener above
    }
}
