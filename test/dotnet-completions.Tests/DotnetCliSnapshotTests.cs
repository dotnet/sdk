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
        DumpEnv(Log);
        var provider = System.CommandLine.StaticCompletions.CompletionsCommand.DefaultShells.Single(x => x.ArgumentName == shellName);
        var completions = provider.GenerateCompletions(Microsoft.DotNet.Cli.Parser.RootCommand);
        var settings = new VerifySettings();
        if (Environment.GetEnvironmentVariable("CI") is string ci && ci.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            Log.WriteLine($"CI environment detected, using snapshots directory in the current working directory {Environment.CurrentDirectory}");
            settings.UseDirectory(Path.Combine(Environment.CurrentDirectory, "snapshots", provider.ArgumentName));
        }
        else
        {
            Log.WriteLine($"Using snapshots from local repository");
            settings.UseDirectory(Path.Combine("snapshots", provider.ArgumentName));
        }
        await Verifier.Verify(target: completions, extension: provider.Extension, settings: settings);
    }


    private static void DumpEnv(ITestOutputHelper log)
    {
        log.WriteLine("Environment Variables:");
        foreach (System.Collections.DictionaryEntry de in Environment.GetEnvironmentVariables())
        {
            log.WriteLine($"  {de.Key} = {de.Value}");
        }
    }

    public static IEnumerable<object[]> ShellNames = System.CommandLine.StaticCompletions.CompletionsCommand.DefaultShells.Select<IShellProvider, object[]>(x => [x.ArgumentName]);
}
