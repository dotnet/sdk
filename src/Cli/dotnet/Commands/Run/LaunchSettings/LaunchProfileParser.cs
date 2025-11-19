// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;

internal abstract class LaunchProfileParser
{
    public abstract LaunchProfileSettings ParseProfile(string launchSettingsPath, string? launchProfileName, string json);

    protected static bool ParseDotNetRunMessages(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    protected static ImmutableDictionary<string, string> ParseEnvironmentVariables(Dictionary<string, string>? values)
    {
        if (values is null or { Count: 0 })
        {
            return [];
        }

        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            // override previously set variables:
            builder[key] = value;
        }

        return builder.ToImmutable();
    }
}
