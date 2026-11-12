// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli;

/// <summary>
/// Replaces the definitions' default path forwarding with forwarding that escapes CLI-owned literal paths
/// using MSBuild's API, so characters such as <c>;</c>, <c>%</c>, and <c>$</c> are not reinterpreted by MSBuild.
/// </summary>
internal static class MSBuildOptionExtensions
{
    public static Option<string> ForwardAsLiteralOutputPath(this Option<string> option, string outputPropertyName, bool surroundWithDoubleQuotes = false)
        => option.ForwardAsOutputPath(outputPropertyName, surroundWithDoubleQuotes, MSBuildPropertyEscaping.Escape);

    public static Option<string> ForwardAsLiteralPathProperty(this Option<string> option, string propertyName, bool surroundWithDoubleQuotes = false)
        => option.ForwardAsPathProperty(propertyName, surroundWithDoubleQuotes, MSBuildPropertyEscaping.Escape);

    public static void ForwardPathsAsLiterals(this ImplicitRestoreOptions options)
    {
        options.PackagesOption.ForwardAsLiteralPathProperty("RestorePackagesPath");
        options.ConfigFileOption.ForwardAsLiteralPathProperty("RestoreConfigFile");
    }
}
