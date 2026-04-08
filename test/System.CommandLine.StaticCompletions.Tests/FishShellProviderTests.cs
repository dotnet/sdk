// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace System.CommandLine.StaticCompletions.Tests;

using System.CommandLine.StaticCompletions.Shells;

public class FishShellProviderTests(ITestOutputHelper log)
{
    private IShellProvider provider = new FishShellProvider();

    [Fact]
    public async Task GenericCompletions()
    {
        await provider.Verify(new("mycommand"), log);
    }

    [Fact]
    public async Task SimpleOptionCompletion()
    {
        await provider.Verify(new("mycommand") {
            new Option<string>("--name")
        }, log);
    }

    [Fact]
    public async Task SubcommandAndOptionInTopLevelList()
    {
        await provider.Verify(new("mycommand") {
                new Option<string>("--name"),
                new Command("subcommand")
            }, log);
    }

    [Fact]
    public async Task NestedSubcommandCompletion()
    {
        await provider.Verify(new("mycommand") {
            new Command("subcommand") {
                new Command("nested")
            }
        }, log);
    }

    [Fact]
    public async Task DynamicCompletionsGeneration()
    {
        var dynamicOption = new Option<int>("--dynamic")
        {
            IsDynamic = true
        };
        var dynamicArg = new Argument<int>("values")
        {
            IsDynamic = true
        };
        Command command = new Command("mycommand")
        {
            dynamicOption,
            dynamicArg
        };
        await provider.Verify(command, log);
    }

    [Fact]
    public async Task StaticOptionValues()
    {
        var staticOption = new Option<int>("--verbosity");
        staticOption.AcceptOnlyFromAmong("quiet", "minimal", "normal", "detailed", "diagnostic");
        Command command = new Command("mycommand")
        {
            staticOption
        };
        await provider.Verify(command, log);
    }

    [Fact]
    public async Task BoundedMultiValueOption()
    {
        var multiOption = new Option<string[]>("--sources")
        {
            Arity = new ArgumentArity(1, 3)
        };
        multiOption.AcceptOnlyFromAmong("src1", "src2", "src3", "src4");
        Command command = new Command("mycommand")
        {
            multiOption,
            new Command("subcommand")
        };
        await provider.Verify(command, log);
    }

    [Fact]
    public async Task UnboundedMultiValueOption()
    {
        var unboundedOption = new Option<string[]>("--items")
        {
            Arity = ArgumentArity.ZeroOrMore
        };
        unboundedOption.AcceptOnlyFromAmong("a", "b", "c");
        Command command = new Command("mycommand")
        {
            unboundedOption,
            new Option<string>("--name"),
            new Command("subcommand")
        };
        await provider.Verify(command, log);
    }

    [Fact]
    public async Task MixedArityOptions()
    {
        var singleOption = new Option<string>("--config");
        singleOption.AcceptOnlyFromAmong("debug", "release");

        var multiOption = new Option<string[]>("--framework", "-f")
        {
            Arity = new ArgumentArity(1, 3)
        };
        multiOption.AcceptOnlyFromAmong("net8.0", "net9.0", "net10.0");

        var unboundedOption = new Option<string[]>("--sources")
        {
            Arity = ArgumentArity.OneOrMore
        };

        Command command = new Command("mycommand")
        {
            singleOption,
            multiOption,
            unboundedOption,
            new Command("build")
        };
        await provider.Verify(command, log);
    }
}
