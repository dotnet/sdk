// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if CLI_AOT
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Combines cached, launch-profile, and application arguments for a Native AOT run invocation.
/// </summary>
internal static class AotRunArguments
{
    /// <summary>
    /// Computes the command arguments using the managed run command's precedence rules.
    /// </summary>
    /// <param name="baseArguments">Arguments from validated run properties.</param>
    /// <param name="applicationArguments">Explicit application arguments.</param>
    /// <param name="launchProfileArguments">Arguments from the selected launch profile.</param>
    /// <param name="appendApplicationArgumentsToBase">Whether explicit arguments should be appended to non-empty base arguments.</param>
    /// <returns>The escaped command arguments.</returns>
    internal static string Combine(
        string? baseArguments,
        string[] applicationArguments,
        string? launchProfileArguments,
        bool appendApplicationArgumentsToBase = false)
    {
        if (applicationArguments.Length != 0)
        {
            string escapedArguments = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(applicationArguments);
            return appendApplicationArgumentsToBase && !string.IsNullOrEmpty(baseArguments)
                ? $"{baseArguments} {escapedArguments}"
                : escapedArguments;
        }

        return string.IsNullOrEmpty(baseArguments) && launchProfileArguments is not null
            ? launchProfileArguments
            : baseArguments ?? string.Empty;
    }
}
#endif
