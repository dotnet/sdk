// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copyright (c) Microsoft Corporation. All rights reserved.
// Adapted from https://github.com/microsoft/testfx/blob/main/src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal/TestFrameworkDisplay.cs

using System.Globalization;

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal static class TestFrameworkDisplay
{
    public static string GetDisplayName(string? targetFramework, string? architecture, string? runtimeFramework = null)
    {
        string primary = string.IsNullOrWhiteSpace(targetFramework)
            ? string.IsNullOrWhiteSpace(architecture) ? string.Empty : architecture
            : string.IsNullOrWhiteSpace(architecture)
                ? targetFramework
                : $"{targetFramework}|{architecture}";

        if (string.IsNullOrWhiteSpace(runtimeFramework))
        {
            return primary;
        }

        if (!string.IsNullOrWhiteSpace(targetFramework) && IsKnownRuntimeFramework(runtimeFramework))
        {
            int platformSeparator = targetFramework.IndexOf('-');
            string baseTargetFramework = platformSeparator < 0 ? targetFramework : targetFramework[..platformSeparator];
            if (string.Equals(baseTargetFramework, runtimeFramework, StringComparison.OrdinalIgnoreCase))
            {
                return primary;
            }
        }

        string runtime = string.Format(CultureInfo.CurrentCulture, CliCommandStrings.RuntimeFramework, runtimeFramework);
        return primary.Length == 0 ? runtime : $"{primary}, {runtime}";
    }

    private static bool IsKnownRuntimeFramework(string framework)
    {
        if (framework.StartsWith("netcoreapp", StringComparison.OrdinalIgnoreCase))
        {
            return Version.TryParse(framework[10..], out _);
        }

        if (!framework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string version = framework[3..];
        return Version.TryParse(version, out _)
            || (version.Length is 2 or 3 && version[0] == '4' && int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out _));
    }
}
