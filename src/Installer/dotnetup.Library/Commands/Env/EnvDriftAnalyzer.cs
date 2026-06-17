// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

/// <summary>
/// Pure comparison of the configured env settings against an observed environment snapshot,
/// producing human-readable drift descriptions. Kept free of any environment reads (the
/// <see cref="EnvironmentStateInspector"/> does those) so it can be unit-tested without touching
/// the registry or shell profiles. Used by <see cref="EnvShowCommand"/>.
/// </summary>
internal static class EnvDriftAnalyzer
{
    public static IReadOnlyList<string> Compare(DotnetupConfigData config, ObservedEnvironmentState observed)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(observed);

        var drift = new List<string>();

        bool expectsProfileDotnet = config.Env is PathPreference.Shell or PathPreference.All;
        bool expectsProfileBlock = expectsProfileDotnet || config.DotnetupOnPath;
        bool expectsDotnetEnvVars = config.Env == PathPreference.All;

        // Profile-block presence: only assert when the profile state is known.
        if (observed.ProfileBlockPresent is bool profileBlockPresent)
        {
            if (expectsProfileBlock && !profileBlockPresent)
            {
                drift.Add("Shell profile is missing the dotnetup managed block.");
            }
            else if (!expectsProfileBlock && profileBlockPresent)
            {
                drift.Add("Shell profile contains a dotnetup managed block but neither dotnet exposure nor dotnetup-on-PATH is configured.");
            }
        }

        if (OperatingSystem.IsWindows())
        {
            if (expectsDotnetEnvVars && !observed.DotnetUserEnvVarsComplete)
            {
                drift.Add("Windows user PATH / DOTNET_ROOT / system PATH do not match 'all' mode expectations.");
            }
            else if (!expectsDotnetEnvVars && observed.DotnetUserEnvVarsPresent)
            {
                drift.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Windows user PATH / DOTNET_ROOT still has 'all'-mode wiring (expected dotnet exposure: '{0}').",
                    config.Env.ToString().ToLowerInvariant()));
            }

            // The user-scope PATH is authoritative for dotnetup-on-PATH on Windows (the profile
            // block copy is just a convenience).
            if (config.DotnetupOnPath && !observed.DotnetupOnUserPath)
            {
                drift.Add("dotnetup is configured to be on PATH but is missing from the user PATH.");
            }
            else if (!config.DotnetupOnPath && observed.DotnetupOnUserPath)
            {
                drift.Add("dotnetup is on the user PATH but is configured to be off.");
            }
        }

        return drift;
    }
}
