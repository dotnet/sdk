// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.CommandLine.StaticCompletions.Tests;

using System.CommandLine.StaticCompletions.Shells;
using Xunit;

public class BashShellProviderTests
{
    private readonly VerifySettings _settings;
    public BashShellProviderTests()
    {
        _settings = new VerifySettings();
        _settings.UseDirectory("snapshots/bash");


    }
    [Fact]
    public async Task GenericCompletions()
    {
        var provider = new BashShellProvider();
        var completions = provider.GenerateCompletions(new("mycommand"));
        await Verify(target: completions, extension: "sh", settings: _settings);
    }

    [Fact]
    public async Task SimpleOptionCompletion()
    {
        var provider = new BashShellProvider();
        var completions = provider.GenerateCompletions(new("mycommand") {
            new CliOption<string>("--name")
        });
        await Verify(target: completions, extension: "sh", settings: _settings);
    }

    [Fact]
    public async Task SubcommandAndOptionInTopLevelList()
    {
        var provider = new BashShellProvider();
        var completions = provider.GenerateCompletions(
            new("mycommand") {
                new CliOption<string>("--name"),
                new CliCommand("subcommand")
            }
        );
        await Verify(target: completions, extension: "sh", settings: _settings);
    }

    [Fact]
    public async Task NestedSubcommandCompletion()
    {
        var provider = new BashShellProvider();
        var completions = provider.GenerateCompletions(new("mycommand") {
            new CliCommand("subcommand") {
                new CliCommand("nested")
            }
        });
        await Verify(target: completions, extension: "sh", settings: _settings);
    }
}
