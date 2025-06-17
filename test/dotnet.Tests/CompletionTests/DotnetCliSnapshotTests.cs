// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.StaticCompletions;
using System.CommandLine.StaticCompletions.Shells;
using Microsoft.TemplateEngine.Cli;

namespace Microsoft.DotNet.Cli.Completions.Tests;

public class DotnetCliSnapshotTests : SdkTest
{
    public DotnetCliSnapshotTests(ITestOutputHelper log) : base(log) { }

    [MemberData(nameof(ShellNames))]
    [Theory]
    public async Task VerifyCompletions(string shellName)
    {
        var provider = CompletionsCommand.DefaultShells.Single(x => x.ArgumentName == shellName);
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

    public static IEnumerable<object[]> ShellNames = CompletionsCommand.DefaultShells.Select<IShellProvider, object[]>(x => [x.ArgumentName]);

    [Fact]
    public void AllOptionsHaveHelpNameSet()
    {
        List<string> optionsWithoutHelpName = new();

        foreach (var command in Parser.Instance.RootCommand.Subcommands)
        {
            foreach (var option in command.HierarchicalOptions())
            {
                // make sure we have HelpName set explicitly for all options by checking whether EnsureHelpName adds a default
                if (option.EnsureHelpName())
                {
                    optionsWithoutHelpName.Add($"{command.Name} {option.Name}");
                }
            }
        }

        if (optionsWithoutHelpName.Count > 0)
        {
            Assert.Fail($"The following options do not have HelpName set: {Environment.NewLine + string.Join(Environment.NewLine, optionsWithoutHelpName)}");
        }
    }
}
