// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.CommandLine.StaticCompletions.Tests;

using System.CommandLine.Help;
using System.CommandLine.StaticCompletions.Shells;
using Xunit;

public class ZshShellProviderTests
{
    private IShellProvider _provider = new ZshShellProvider();
    [Fact]
    public async Task GenericCompletions()
    {
        var provider = new ZshShellProvider();
        CliCommand command = new CliCommand("my-app") {
            new CliOption<bool>("-c") {
                Arity = ArgumentArity.Zero,
                Recursive = true
            },
            new CliOption<bool>("-v") {
                Arity = ArgumentArity.Zero
            },
            new HelpOption(),
            new CliCommand("test", "Subcommand\nwith a second line") {
                new CliOption<bool>("--debug", "-d")
                {
                    Arity = ArgumentArity.Zero
                }
            },
            new CliCommand("help", "Print this message or the help of the given subcommand(s)") {
                new CliCommand("test")
            }
        };
        await VerifyExtensions.Verify(command, _provider);
    }
}
