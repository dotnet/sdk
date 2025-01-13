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
        if (!Directory.Exists(sourceFileDir))
        {
            throw new DirectoryNotFoundException($"The directory ({sourceFileDir}) containing the source file ({sourceFile}) does not exist - Verify is going to try to recreate the directory and that won't work in CI.");
        }
        settings.UseDirectory(Path.Combine(sourceFileDir, "snapshots", provider.ArgumentName));
        await Verifier.Verify(target: completions, extension: provider.Extension, settings: settings, sourceFile: sourceFile);
    }
}
