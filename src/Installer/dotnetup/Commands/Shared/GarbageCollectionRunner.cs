// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Shared helper for running garbage collection and displaying results.
/// </summary>
internal static class GarbageCollectionRunner
{
    /// <summary>
    /// Runs garbage collection for a dotnet root, printing what was deleted.
    /// </summary>
    /// <param name="manifestPath">Path to the manifest file.</param>
    /// <param name="installRoot">The dotnet install root to clean.</param>
    /// <param name="showEmptyMessage">If true, shows a message when nothing was deleted.</param>
    /// <returns>The list of deleted subcomponent paths.</returns>
    public static List<string> RunAndDisplay(string? manifestPath, DotnetInstallRoot installRoot, bool showEmptyMessage = false)
    {
        AnsiConsole.WriteLine("Removing unused installations...");
        var gc = new GarbageCollector(new DotnetupSharedManifest(manifestPath));
        var deleted = gc.Collect(installRoot);

        if (deleted.Count > 0)
        {
            foreach (var d in deleted)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  Removed [dim]{d}[/]");
            }
        }
        else if (showEmptyMessage)
        {
            AnsiConsole.MarkupLine("[dim]No files were removed because they are still in use by other tracked installations.[/]");
        }

        return deleted;
    }
}
