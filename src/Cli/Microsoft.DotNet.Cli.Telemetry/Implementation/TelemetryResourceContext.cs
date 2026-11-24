// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Telemetry.Implementation;

/// <summary>
/// Immutable, per-process telemetry resource attributes used to populate the
/// Application Insights context tags of every telemetry envelope (cloud role,
/// role instance, application version, SDK version).
/// </summary>
internal sealed record TelemetryResourceContext(
    string? RoleName,
    string? RoleInstance,
    string? ApplicationVersion,
    string SdkVersion);
