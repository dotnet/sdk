// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
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
        var gc = new GarbageCollector(new DotnetupSharedManifest(manifestPath));
        return DisplayResults(() => gc.Collect(installRoot), showEmptyMessage);
    }

    internal static List<string> ApplyAndDisplay(
        GarbageCollector collector, GarbageCollectionPlan plan)
    {
        return DisplayResults(() => collector.Apply(plan), showEmptyMessage: true);
    }

    private static List<string> DisplayResults(Func<List<string>> collect, bool showEmptyMessage)
    {
        Debug.Assert(ScopedMutex.CurrentThreadHoldsMutex, "GarbageCollectionRunner.RunAndDisplay must be called while holding the mutex.");
        AnsiConsole.WriteLine("Removing unused installations...");

        var deleted = collect();

        if (deleted.Count > 0)
        {
            foreach (var d in deleted)
            {
                AnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  Removed {0}", DotnetupTheme.Dim(d.EscapeMarkup())));
            }
        }
        else if (showEmptyMessage)
        {
            AnsiConsole.MarkupLine(DotnetupTheme.Dim("No files were removed."));
        }

        return deleted;
    }
}
