// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.StaticCompletions.Shells;
using System.Runtime.CompilerServices;

namespace System.CommandLine.StaticCompletions.Tests;

public static class VerifyExtensions
{
    public static async Task Verify(CliCommand command, IShellProvider provider, [CallerFilePath] string sourceFile = "")
    {
        // Can't use sourceFile directly because in CI the file may be rooted at a different location than the compile-time location
        // We do have the source code available, just at a different root, so we can use that compute
        var settings = new VerifySettings();
        var runtimeSnapshotDir = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "snapshots", provider.ArgumentName));
        var closestExistingDirectory = GetClosestExistingDirectory(runtimeSnapshotDir);
        if (!runtimeSnapshotDir.Exists)
        {
            throw new DirectoryNotFoundException($"The directory ({runtimeSnapshotDir}) containing the source file ({sourceFile}) does not exist.\nVerify is going to try to recreate the directory and that won't work in CI.\nThe closest existing directory is ({closestExistingDirectory}). The current directory is ({Environment.CurrentDirectory}).");
        }
        settings.UseDirectory(runtimeSnapshotDir.FullName);
        var completions = provider.GenerateCompletions(command);
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
