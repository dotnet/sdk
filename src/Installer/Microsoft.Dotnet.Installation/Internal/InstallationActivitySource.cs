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
/// <strong>Important:</strong> If you use this library and collect telemetry, you are responsible
/// for complying with the .NET SDK telemetry policy. This includes:
/// </para>
/// <list type="bullet">
///   <item><description>Displaying a first-run notice to users explaining what data is collected</description></item>
///   <item><description>Honoring the <c>DOTNET_CLI_TELEMETRY_OPTOUT</c> environment variable</description></item>
///   <item><description>Providing documentation about your telemetry practices</description></item>
/// </list>
/// <para>
/// See the .NET SDK telemetry documentation for guidance:
/// https://learn.microsoft.com/dotnet/core/tools/telemetry
/// </para>
/// <para>
/// Library consumers can hook into installation telemetry by subscribing to this ActivitySource.
/// The following activities are emitted:
/// </para>
/// <list type="bullet">
///   <item><term>download</term><description>SDK/runtime archive download. Tags: download.version, download.url, download.bytes, download.from_cache</description></item>
///   <item><term>extract</term><description>Archive extraction. Tags: download.version</description></item>
/// </list>
/// <para>
/// When activities originate from dotnetup CLI, they include the tag <c>caller=dotnetup</c>.
/// Library consumers can use this to distinguish CLI-originated vs direct library calls.
/// </para>
/// <para>
/// For a working example of how to integrate with this ActivitySource, see the
/// TelemetryIntegrationDemo project in test/dotnetup.Tests/TestAssets/.
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
