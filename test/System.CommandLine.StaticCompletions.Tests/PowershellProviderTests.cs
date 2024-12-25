// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.CommandLine.StaticCompletions.Tests;

using System.CommandLine.StaticCompletions.Shells;
using EmptyFiles;
using Xunit;

public class PowershellProviderTests
{
    private readonly VerifySettings _settings;
    public PowershellProviderTests()
    {
        _settings = new VerifySettings();
        _settings.UseDirectory("snapshots/pwsh");
        FileExtensions.AddTextExtension("ps1");


    }
    [Fact]
    public async Task GenericCompletions()
    {
        var provider = new PowershellShellProvider();
        var completions = provider.GenerateCompletions(new("mycommand"));
        await Verify(target: completions, extension: "ps1", settings: _settings);
    }

    [Fact]
    public async Task SimpleOptionCompletion()
    {
        var provider = new PowershellShellProvider();
        var completions = provider.GenerateCompletions(new("mycommand") {
            new CliOption<string>("--name")
        });
        await Verify(target: completions, extension: "ps1", settings: _settings);
    }

    [Fact]
    public async Task SubcommandAndOptionInTopLevelList()
    {
        var provider = new PowershellShellProvider();
        var completions = provider.GenerateCompletions(
            new("mycommand") {
                new CliOption<string>("--name"),
                new CliCommand("subcommand")
            }
        );
        await Verify(target: completions, extension: "ps1", settings: _settings);
    }

    [Fact]
    public async Task NestedSubcommandCompletion()
    {
        var provider = new PowershellShellProvider();
        var completions = provider.GenerateCompletions(new("mycommand") {
            new CliCommand("subcommand") {
                new CliCommand("nested")
            }
        });
        await Verify(target: completions, extension: "ps1", settings: _settings);
    }
}
