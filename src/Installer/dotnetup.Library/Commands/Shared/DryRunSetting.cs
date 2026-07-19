// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Resolves whether dry-run (preview) mode is requested via the <c>DOTNETUP_DRY_RUN</c> environment
/// variable. Dry-run lets the init form and the settings it would apply be previewed without
/// installing anything or changing the environment. The environment variable is a test hook that
/// also covers the first-run onboarding path and CI/screenshot testing; the interactive
/// <c>--dry-run</c> option covers explicit <c>dotnetup init</c> use.
/// </summary>
internal static class DryRunSetting
{
    internal const string EnvironmentVariableName = "DOTNETUP_DRY_RUN";

    /// <summary>
    /// True when <c>DOTNETUP_DRY_RUN</c> is set to a truthy value (<c>1</c>, <c>true</c>, or
    /// <c>yes</c>, case-insensitive).
    /// </summary>
    public static bool IsEnabledFromEnvironment()
    {
        string? value = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        return value is not null
            && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }
}
