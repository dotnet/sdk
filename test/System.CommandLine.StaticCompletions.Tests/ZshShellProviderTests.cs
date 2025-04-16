// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.CommandLine.StaticCompletions.Tests;

using System.CommandLine.Help;
using System.CommandLine.StaticCompletions.Shells;
using Xunit;
using Xunit.Abstractions;

public class ZshShellProviderTests(ITestOutputHelper log)
{
    private IShellProvider _provider = new ZshShellProvider();

    [Fact]
    public async Task GenericCompletions()
    {
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
        await _provider.Verify(command, log);
    }

    [Fact]
    public async Task DynamicCompletionsGeneration()
    {
        var staticOption = new DynamicOption<int>("--static");
        staticOption.AcceptOnlyFromAmong("1", "2", "3");
        var dynamicArg = new DynamicArgument<int>("--dynamic");
        dynamicArg.CompletionSources.Add((context) =>
        {
            return [
                new ("4"),
                new ("5"),
                new ("6")
            ];
        });
        CliCommand command = new CliCommand("my-app")
        {
            staticOption,
            dynamicArg
        };
        await _provider.Verify(command, log);
    }

    [Fact]
    public async Task CustomStaticCompletionsGeneration()
    {
        var staticOption = new CliOption<int>("--static");
        staticOption.AcceptOnlyFromAmong("1", "2", "3");
        var dynamicArg = new CliArgument<int>("--dynamic");
        dynamicArg.CompletionSources.Add((context) =>
        {
            return [
                new ("4"),
                new ("5"),
                new ("6")
            ];
        });
        CliCommand command = new CliCommand("my-app")
        {
            staticOption,
            dynamicArg
        };
        await _provider.Verify(command, log);
    }
}
