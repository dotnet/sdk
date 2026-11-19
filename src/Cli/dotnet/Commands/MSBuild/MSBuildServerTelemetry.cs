// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.DotNet.Cli.Commands.MSBuild;

internal static class MSBuildServerTelemetry
{
    private const string StartParameter = "DotNetSdkAotServerStartTicks=";
    private const string ForwardingLoggerSeparator = "*Microsoft.DotNet.Cli.Commands.MSBuild.MSBuildForwardingLogger,";

    internal static string AddClientStartToLogger(string loggerArgument, DateTime startedAt)
    {
        int separator = loggerArgument.LastIndexOf(ForwardingLoggerSeparator, StringComparison.Ordinal);
        if (separator < 0)
        {
            throw new InvalidOperationException("The SDK telemetry logger has no forwarding logger.");
        }

        return loggerArgument.Insert(separator, ";" + StartParameter + startedAt.Ticks.ToString(CultureInfo.InvariantCulture));
    }

    internal static IDictionary<string, string?> CompleteBuildProperties(
        IDictionary<string, string?> properties,
        string? parameters,
        DateTime finishedAt)
    {
        if (parameters is null
            || !parameters.StartsWith(StartParameter, StringComparison.Ordinal)
            || !long.TryParse(parameters.AsSpan(StartParameter.Length), NumberStyles.None, CultureInfo.InvariantCulture, out long ticks)
            || ticks < DateTime.MinValue.Ticks
            || ticks > finishedAt.Ticks)
        {
            return properties;
        }

        var completed = new Dictionary<string, string?>(properties);
        completed.TryAdd("ServerEnableReason", "EnvVar");
        completed.TryAdd("ResponseFileUsed", bool.FalseString);
        completed["BuildDurationInMilliseconds"] =
            TimeSpan.FromTicks(finishedAt.Ticks - ticks).TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
        return completed;
    }
}
