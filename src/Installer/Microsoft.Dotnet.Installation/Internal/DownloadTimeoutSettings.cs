// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Resolves archive download timeout settings from the process environment.
/// Assumes timeout overrides are positive integer second values; invalid values fall back to defaults.
/// </summary>
internal sealed class DownloadTimeoutSettings
{
    public const string IdleTimeoutSecondsEnvVar = "DOTNETUP_DOWNLOAD_IDLE_TIMEOUT_SECONDS";
    public const string TotalTimeoutSecondsEnvVar = "DOTNETUP_DOWNLOAD_TIMEOUT_SECONDS";

    private static readonly TimeSpan s_defaultIdleTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan s_defaultTotalTimeout = TimeSpan.FromMinutes(15);

    public DownloadTimeoutSettings(TimeSpan idleTimeout, TimeSpan totalTimeout)
    {
        if (idleTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(idleTimeout), idleTimeout, "Timeout must be positive.");
        }

        if (totalTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(totalTimeout), totalTimeout, "Timeout must be positive.");
        }

        IdleTimeout = idleTimeout;
        TotalTimeout = totalTimeout;
    }

    public TimeSpan IdleTimeout { get; }

    public TimeSpan TotalTimeout { get; }

    public static DownloadTimeoutSettings FromEnvironment() => new(
        ReadTimeoutFromEnvironment(IdleTimeoutSecondsEnvVar, s_defaultIdleTimeout),
        ReadTimeoutFromEnvironment(TotalTimeoutSecondsEnvVar, s_defaultTotalTimeout));

    private static TimeSpan ReadTimeoutFromEnvironment(string variableName, TimeSpan defaultValue)
    {
        string? value = Environment.GetEnvironmentVariable(variableName);
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return defaultValue;
    }
}