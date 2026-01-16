// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace System.CommandLine.StaticCompletions.Tests;

using System.CommandLine.Help;
using System.CommandLine.StaticCompletions.Shells;
using Xunit;
using Xunit.Abstractions;

public class NushellShellProviderTests(ITestOutputHelper log)
{
    private IShellProvider _provider = new NushellShellProvider();

    [Fact]
    public async Task GenericCompletions()
    {
        await _provider.Verify(new("mycommand"), log);
    }

    [Fact]
    public async Task SimpleOptionCompletion()
    {
        await _provider.Verify(new("mycommand")
        {
            new Option<string>("--name")
        }, log);
    }

    [Fact]
    public async Task SubcommandAndOptionInTopLevelList()
    {
        await _provider.Verify(new("mycommand")
        {
            new Option<string>("--name"),
            new Command("subcommand")
        }, log);
    }

    [Fact]
    public async Task NestedSubcommandCompletion()
    {
        await _provider.Verify(new("mycommand")
        {
            new Command("subcommand")
            {
                new Command("nested")
            }
        }, log);
    }

    [Fact]
    public async Task FlagOptionsHaveNoType()
    {
        await _provider.Verify(new("mycommand")
        {
            new Option<bool>("--verbose", "-v")
            {
                Arity = ArgumentArity.Zero
            },
            new Option<bool>("--debug")
            {
                Arity = ArgumentArity.Zero
            }
        }, log);
    }

    [Fact]
    public async Task OptionWithAliasGenerated()
    {
        await _provider.Verify(new("mycommand")
        {
            new Option<string>("--configuration", "-c"),
            new Option<int>("--verbosity", "-v")
        }, log);
    }

    [Fact]
    public async Task RecursiveOptionsInherited()
    {
        await _provider.Verify(new("mycommand")
        {
            new Option<bool>("--verbose", "-v")
            {
                Arity = ArgumentArity.Zero,
                Recursive = true
            },
            new Command("subcommand")
            {
                new Option<string>("--name")
            }
        }, log);
    }

    [Fact]
    public async Task RecursiveOptionsWithCompletions()
    {
        var verbosityOption = new Option<string>("--verbosity", "-v")
        {
            Recursive = true
        };
        verbosityOption.AcceptOnlyFromAmong("quiet", "minimal", "normal", "detailed", "diagnostic");

        await _provider.Verify(new("mycommand")
        {
            verbosityOption,
            new Command("subcommand")
            {
                new Option<string>("--name"),
                new Command("nested")
            }
        }, log);
    }

    [Fact]
    public async Task DynamicCompletionsGeneration()
    {
        var dynamicOption = new Option<string>("--project")
        {
            IsDynamic = true
        };
        var dynamicArg = new Argument<string>("file")
        {
            IsDynamic = true
        };
        Command command = new Command("mycommand")
        {
            dynamicOption,
            dynamicArg
        };
        await _provider.Verify(command, log);
    }

    [Fact]
    public async Task StaticCompletionsWithValues()
    {
        var optionWithCompletions = new Option<string>("--verbosity", "-v");
        optionWithCompletions.AcceptOnlyFromAmong("quiet", "minimal", "normal", "detailed", "diagnostic");

        await _provider.Verify(new("mycommand")
        {
            optionWithCompletions
        }, log);
    }

    [Fact]
    public async Task ArgumentWithCompletions()
    {
        var argWithCompletions = new Argument<string>("framework");
        argWithCompletions.CompletionSources.Add((context) =>
        {
            return [
                new("net6.0"),
                new("net7.0"),
                new("net8.0")
            ];
        });

        await _provider.Verify(new("mycommand")
        {
            argWithCompletions
        }, log);
    }

    [Fact]
    public async Task ComplexCommandHierarchy()
    {
        Command command = new Command("my-app")
        {
            new Option<bool>("-c")
            {
                Arity = ArgumentArity.Zero,
                Recursive = true
            },
            new Option<bool>("-v")
            {
                Arity = ArgumentArity.Zero
            },
            new HelpOption(),
            new Command("test", "Subcommand\nwith a second line")
            {
                new Option<bool>("--debug", "-d")
                {
                    Arity = ArgumentArity.Zero
                }
            },
            new Command("help", "Print this message or the help of the given subcommand(s)")
            {
                new Command("test")
            }
        };
        await _provider.Verify(command, log);
    }

    [Fact]
    public async Task TypeMappingForArguments()
    {
        await _provider.Verify(new("mycommand")
        {
            new Argument<FileInfo>("file"),
            new Argument<DirectoryInfo>("directory"),
            new Argument<int>("count"),
            new Argument<double>("ratio")
        }, log);
    }

    [Fact]
    public async Task OptionalAndVariadicArguments()
    {
        await _provider.Verify(new("mycommand")
        {
            new Argument<string>("required"),
            new Argument<string>("optional")
            {
                Arity = ArgumentArity.ZeroOrOne
            },
            new Argument<string[]>("files")
            {
                Arity = ArgumentArity.ZeroOrMore
            }
        }, log);
    }
}
