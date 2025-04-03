// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Completions.Tests;

using System.CommandLine.StaticCompletions.Shells;
using VerifyXunit;
using Xunit.Abstractions;

public class DotnetCliSnapshotTests : SdkTest
{
    public DotnetCliSnapshotTests(ITestOutputHelper log) : base(log) { }

    [MemberData(nameof(ShellNames))]
    [Theory]
    public async Task VerifyCompletions(string shellName)
    {
        var provider = System.CommandLine.StaticCompletions.CompletionsCommand.DefaultShells.Single(x => x.ArgumentName == shellName);
        var completions = provider.GenerateCompletions(Microsoft.DotNet.Cli.Parser.RootCommand);
        var settings = new VerifySettings();
        if (Environment.GetEnvironmentVariable("USER") is string user && user.Contains("helix", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("USER")))
        {
            Log.WriteLine($"CI environment detected, using snapshots directory in the current working directory {Environment.CurrentDirectory}");
            settings.UseDirectory(Path.Combine(Environment.CurrentDirectory, "snapshots", provider.ArgumentName));
        }
        else
        {
            Log.WriteLine($"Using snapshots from local repository because $USER {Environment.GetEnvironmentVariable("USER")} is not helix-related");
            settings.UseDirectory(Path.Combine("snapshots", provider.ArgumentName));
        }
        await Verifier.Verify(target: completions, extension: provider.Extension, settings: settings);
    }

    public static IEnumerable<object[]> ShellNames = System.CommandLine.StaticCompletions.CompletionsCommand.DefaultShells.Select<IShellProvider, object[]>(x => [x.ArgumentName]);
}
