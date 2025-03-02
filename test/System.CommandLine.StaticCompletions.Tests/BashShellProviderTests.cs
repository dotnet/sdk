// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.CommandLine.StaticCompletions.Tests;

using System.CommandLine.StaticCompletions.Shells;
using Xunit;
using Xunit.Abstractions;

public class BashShellProviderTests(ITestOutputHelper log)
{
    private IShellProvider provider = new BashShellProvider();
    [Fact]
    public async Task GenericCompletions()
    {
        await provider.Verify(new("mycommand"), log);
    }

    [Fact]
    public async Task SimpleOptionCompletion()
    {
        await provider.Verify(new("mycommand") {
            new CliOption<string>("--name")
        }, log);
    }

    [Fact]
    public async Task SubcommandAndOptionInTopLevelList()
    {
        await provider.Verify(new("mycommand") {
                new CliOption<string>("--name"),
                new CliCommand("subcommand")
            }, log);
    }

    [Fact]
    public async Task NestedSubcommandCompletion()
    {
        await provider.Verify(new("mycommand") {
            new CliCommand("subcommand") {
                new CliCommand("nested")
            }
        }, log);
    }
}
