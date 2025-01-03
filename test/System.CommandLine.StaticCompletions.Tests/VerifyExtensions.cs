// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.StaticCompletions.Shells;

namespace System.CommandLine.StaticCompletions.Tests;

public static class VerifyExtensions
{
    public static async Task Verify(CliCommand command, IShellProvider provider)
    {
        var completions = provider.GenerateCompletions(command);
        var settings = new VerifySettings();
        settings.UseDirectory(Path.Combine("snapshots", provider.ArgumentName));
        await Verifier.Verify(target: completions, extension: provider.Extension, settings: settings);
    }
}
