// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.StaticCompletions.Shells;
using System.Runtime.CompilerServices;

namespace System.CommandLine.StaticCompletions.Tests;

public static class VerifyExtensions
{
    public static async Task Verify(CliCommand command, IShellProvider provider, [CallerFilePath] string sourceFile = "")
    {
        var completions = provider.GenerateCompletions(command);
        var settings = new VerifySettings();
        var sourceFileDir = Path.GetDirectoryName(sourceFile)!;
        var snapshotDirectory = new DirectoryInfo(Path.Combine(sourceFileDir, "snapshots", provider.ArgumentName));
        var closestExistingDirectory = GetClosestExistingDirectory(snapshotDirectory);
        if (!snapshotDirectory.Exists)
        {
            throw new DirectoryNotFoundException($"The directory ({snapshotDirectory}) containing the source file ({sourceFile}) does not exist.\nVerify is going to try to recreate the directory and that won't work in CI.\nThe closest existing directory is ({closestExistingDirectory}). The current directory is ({Environment.CurrentDirectory})");
        }
        settings.UseDirectory(snapshotDirectory.FullName);
        await Verifier.Verify(target: completions, extension: provider.Extension, settings: settings, sourceFile: sourceFile);
    }

    private static DirectoryInfo? GetClosestExistingDirectory(DirectoryInfo? path)
    {
        while (path is not null && !path.Exists)
        {
            path = path.Parent;
        }
        return path;
    }
}
