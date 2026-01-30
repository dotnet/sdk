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
