// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;
using System.CommandLine.StaticCompletions;

namespace Microsoft.DotNet.Cli.ShellShim;

internal static class ZshDetector
{
    /// <summary>
    /// Returns true if the `SHELL` environment variable ends with `zsh` for the filename.
    /// </summary>
    public static bool IsZshTheUsersShell(IEnvironmentProvider environmentProvider)
    {
        string environmentVariable = environmentProvider.GetEnvironmentVariable("SHELL");
        if (string.IsNullOrWhiteSpace(environmentVariable))
        {
            return false;
        }

        return ShellInfo.GetShellType(environmentVariable) == ShellType.Zsh;
    }
}
