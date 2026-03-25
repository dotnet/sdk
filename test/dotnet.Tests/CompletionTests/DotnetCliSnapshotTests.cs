// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.StaticCompletions;

namespace Microsoft.DotNet.Cli.Completions.Tests;

public class DotnetCliSnapshotTests(ITestOutputHelper log) : SdkTest(log)
{
    [MemberData(nameof(TestCases))]
    [Theory(Skip = "https://github.com/dotnet/sdk/issues/48817")]
    public async Task VerifyCompletions(string shellName)
    {
        if (!shellName.Equals("zsh") || !SdkTestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild)
        {
            // This has been unstable lately; skipping
            return;
        }

        var provider = CompletionsCommandParser.ShellProviders[shellName];
        var completions = provider.GenerateCompletions(Parser.RootCommand);
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
        await Verify(target: completions, extension: provider.Extension, settings: settings);
    }

    public static IEnumerable<object[]> TestCases = ShellNames.All.Select(x => new object[] { x });
}
