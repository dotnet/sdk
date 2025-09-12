// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli;

/// <summary>
/// Creates common options.
/// </summary>
internal static class CommonOptionsFactory
{
    /// <summary>
    /// Creates common diagnostics option (-d|--diagnostics).
    /// </summary>
    public static Option<bool> CreateDiagnosticsOption(bool recursive) => new("--diagnostics", "-d")
    {
        Description = CliStrings.SDKDiagnosticsCommandDefinition,
        Recursive = recursive,
        Arity = ArgumentArity.Zero
    };
}
