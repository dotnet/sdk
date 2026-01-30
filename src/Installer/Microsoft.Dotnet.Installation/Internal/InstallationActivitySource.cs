// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Provides the ActivitySource for installation telemetry.
/// This source is listened to by dotnetup's DotnetupTelemetry when running via CLI,
/// and can be subscribed to by other consumers via ActivityListener.
/// </summary>
/// <remarks>
/// <para>
/// Library consumers can hook into installation telemetry by subscribing to this ActivitySource.
/// The following activities are emitted:
/// </para>
/// <list type="bullet">
///   <item><term>download</term><description>SDK/runtime archive download. Tags: download.version, download.url, download.bytes, download.from_cache</description></item>
///   <item><term>extract</term><description>Archive extraction. Tags: download.version</description></item>
/// </list>
/// <para>
/// Example usage:
/// </para>
/// <code>
/// using var listener = new ActivityListener
/// {
///     ShouldListenTo = source => source.Name == "Microsoft.Dotnet.Installation",
///     Sample = (ref ActivityCreationOptions&lt;ActivityContext&gt; _) => ActivitySamplingResult.AllDataAndRecorded,
///     ActivityStarted = activity =>
///     {
///         // Add custom tags (e.g., your tool's name)
///         activity.SetTag("caller", "my-custom-tool");
///     },
///     ActivityStopped = activity =>
///     {
///         // Export to your telemetry system
///         Console.WriteLine($"{activity.DisplayName}: {activity.Duration.TotalMilliseconds}ms");
///     }
/// };
/// ActivitySource.AddActivityListener(listener);
///
/// // Now use the library - activities will be captured
/// var installer = InstallerFactory.Create(progressTarget);
/// installer.Install(root, InstallComponent.Sdk, version);
/// </code>
/// <para>
/// When activities originate from dotnetup CLI, they include the tag <c>caller=dotnetup</c>.
/// Library consumers can use this to distinguish CLI-originated vs direct library calls.
/// </para>
/// </remarks>
internal static class InstallationActivitySource
{
    /// <summary>
    /// The name of the ActivitySource. Must match what consumers listen for.
    /// </summary>
    public const string SourceName = "Microsoft.Dotnet.Installation";

    private static readonly ActivitySource s_activitySource = new(
        SourceName,
        typeof(InstallationActivitySource).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0");

    public static ActivitySource ActivitySource => s_activitySource;
}
