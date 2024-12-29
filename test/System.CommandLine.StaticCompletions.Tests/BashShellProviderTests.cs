// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.CommandLine.StaticCompletions.Tests;

using System.CommandLine.StaticCompletions.Shells;
using Xunit;

public class BashShellProviderTests
{
    [Fact]
    public async Task GenericCompletions()
    {
        await VerifyExtensions.Verify(new("mycommand"), new BashShellProvider());
    }

    [Fact]
    public async Task SimpleOptionCompletion()
    {
        await VerifyExtensions.Verify(new("mycommand") {
            new CliOption<string>("--name")
        }, new BashShellProvider());
    }

    [Fact]
    public async Task SubcommandAndOptionInTopLevelList()
    {
        await VerifyExtensions.Verify(new("mycommand") {
                new CliOption<string>("--name"),
                new CliCommand("subcommand")
            }, new BashShellProvider());
    }

    [Fact]
    public async Task NestedSubcommandCompletion()
    {
        await VerifyExtensions.Verify(new("mycommand") {
            new CliCommand("subcommand") {
                new CliCommand("nested")
            }
        }, new BashShellProvider());
    }
}
