// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.CommandLine.StaticCompletions.Shells;

/// <summary>
/// Provides and manages completions scripts for a specific shell.
/// </summary>
public interface IShellProvider
{
    /// <summary>
    /// The name of this shell as exposed on the completion command line arguments.
    /// </summary>
    string ArgumentName { get; }

    /// <summary>
    /// Generates a shell-specific completions script for the given command tree.
    /// </summary>
    /// <param name="rootCommand"></param>
    /// <returns></returns>
    string GenerateCompletions(System.CommandLine.CliCommand command);
}


