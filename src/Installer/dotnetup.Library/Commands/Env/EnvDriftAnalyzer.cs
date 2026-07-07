// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

/// <summary>
/// Comparison of desired versus observed config. Produces human-readable descriptions. Uses unit-testable
/// snapshots.
/// </summary>
internal static class EnvDriftAnalyzer
{
    public static IReadOnlyList<string> Compare(DotnetupConfigData config, ObservedEnvironmentState observed)
    {
        var drift = new List<string>();

        bool expectsProfileDotnet = config.AccessMode is DotnetAccessMode.Shell or DotnetAccessMode.Full;
        bool expectsProfileBlock = expectsProfileDotnet || config.DotnetupOnPath;
        bool expectsDotnetEnvVars = config.AccessMode == DotnetAccessMode.Full;

        // Profile-block presence: only assert when the profile state is known.
        if (observed.ProfileBlockPresent is bool profileBlockPresent)
        {
            if (expectsProfileBlock && !profileBlockPresent)
            {
                drift.Add(Strings.EnvDriftProfileBlockMissing);
            }
            else if (!expectsProfileBlock && profileBlockPresent)
            {
                drift.Add(Strings.EnvDriftProfileBlockUnexpected);
            }
        }

        if (OperatingSystem.IsWindows())
        {
            if (expectsDotnetEnvVars && !observed.DotnetUserEnvVarsComplete)
            {
                drift.Add(Strings.EnvDriftFullModeEnvVarsIncomplete);
            }
            else if (!expectsDotnetEnvVars && observed.DotnetUserEnvVarsPresent)
            {
                drift.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    Strings.EnvDriftFullModeEnvVarsUnexpected,
                    config.AccessMode.ToString().ToLowerInvariant()));
            }

            // The user-scope PATH is authoritative for dotnetupOnPath on Windows (the profile
            // block copy is just a convenience).
            if (config.DotnetupOnPath && !observed.DotnetupOnUserPath)
            {
                drift.Add(Strings.EnvDriftDotnetupOnPathMissing);
            }
            else if (!config.DotnetupOnPath && observed.DotnetupOnUserPath)
            {
                drift.Add(Strings.EnvDriftDotnetupOnPathUnexpected);
            }
        }

        return drift;
    }
}
