// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.Add;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Help;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests;

[TestClass]
public class GenericCommandHelpParserTests
{
    private static readonly HashSet<string> s_externalHelpCommands =
    [
        "test",
        "msbuild",
        "nuget",
        "vstest",
        "format",
        "fsi",
    ];

    private static readonly HashSet<string> s_networkCompletionHelpCommands =
    [
        "add",
        "package",
    ];

    [TestMethod]
    public void SafelyGetValueForOption_ReturnsDefaultWhenOptionIsNotInCommandTree()
    {
        ParseResult parseResult = new RootCommand().Parse([]);

        Assert.IsNull(parseResult.SafelyGetValueForOption<string>("--missing"));
    }

    [TestMethod]
    public void SafelyGetValueForOption_ReturnsValueWhenOptionExists()
    {
        RootCommand rootCommand = new();
        Command childCommand = new("child");
        Option<string> option = new("--value");
        childCommand.Options.Add(option);
        rootCommand.Subcommands.Add(childCommand);
        ParseResult parseResult = rootCommand.Parse(["child", "--value", "expected"]);

        Assert.AreEqual("expected", parseResult.SafelyGetValueForOption<string>("--value"));
    }

    [TestMethod]
    public void TryParseGenericCommandHelp_CoversEveryEligibleTopLevelCommandAndAlias()
    {
        foreach (Command command in Parser.RootCommand.Subcommands)
        {
            bool expected = command.Name != "test";

            foreach (string token in GetCommandTokens(command))
            {
                string[] args = [token, "--help"];
                Assert.AreEqual(
                    expected,
                    Parser.TryParseGenericCommandHelp(args, out ParseResult? parseResult),
                    token);

                if (expected)
                {
                    Assert.IsNotNull(parseResult, token);
                    AssertMinimalCommandTreeParity(args, parseResult);
                }
                else
                {
                    Assert.IsNull(parseResult, token);
                }
            }
        }
    }

    [TestMethod]
    [DataRow("new -h")]
    [DataRow("new /h")]
    [DataRow("new --help")]
    [DataRow("new -?")]
    [DataRow("new /?")]
    [DataRow("new create -h")]
    [DataRow("new create /h")]
    [DataRow("new create --help")]
    [DataRow("new create -?")]
    [DataRow("new create /?")]
    [DataRow("build -h")]
    [DataRow("build /h")]
    [DataRow("build --help")]
    [DataRow("build -?")]
    [DataRow("build /?")]
    public void TryParseGenericCommandHelp_UsesMinimalCommandTree(string commandLine)
    {
        string[] args = commandLine.Split(' ');

        Assert.IsTrue(Parser.TryParseGenericCommandHelp(args, out ParseResult? parseResult));
        Assert.IsNotNull(parseResult);
        AssertMinimalCommandTreeParity(args, parseResult);
    }

    [TestMethod]
    [DataRow("new")]
    [DataRow("new console --help")]
    [DataRow("new install --help")]
    [DataRow("new --help --debug:ephemeral-hive")]
    [DataRow("build")]
    [DataRow("build --help --no-restore")]
    [DataRow("test --help")]
    public void TryParseGenericCommandHelp_RejectsOtherCommandShapes(string commandLine)
    {
        Assert.IsFalse(Parser.TryParseGenericCommandHelp(commandLine.Split(' '), out ParseResult? parseResult));
        Assert.IsNull(parseResult);
    }

    [TestMethod]
    [DataRow("add")]
    [DataRow("package")]
    public void TryParseGenericCommandHelp_PreservesPackageIdCompletionSources(string commandName)
    {
        Assert.IsTrue(Parser.TryParseGenericCommandHelp([commandName, "--help"], out ParseResult? parseResult));

        Command minimalCommand = parseResult.RootCommandResult.Command.Subcommands.Single();
        Command fullCommand = Parser.RootCommand.Subcommands.Single(command => command.Name == commandName);

        Assert.AreEqual(
            GetPackageIdCompletionSourceCount(fullCommand),
            GetPackageIdCompletionSourceCount(minimalCommand));
    }

    [TestMethod]
    [DataRow("add")]
    [DataRow("package")]
    public void TryParseGenericCommandHelp_PreservesPackageIdCompletionsInParentHelp(string commandName)
    {
        Assert.IsTrue(Parser.TryParseGenericCommandHelp([commandName, "--help"], out ParseResult? parseResult));

        Argument packageIdArgument = GetPackageIdArgument(parseResult.RootCommandResult.Command.Subcommands.Single());
        packageIdArgument.CompletionSources.Clear();
        packageIdArgument.CompletionSources.Add(_ => [new CompletionItem("Example.Package")]);

        (int exitCode, string output, string error) = InvokeWithCapture(parseResult);

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Example.Package", output);
        Assert.IsEmpty(error);
        Assert.HasCount(1, packageIdArgument.CompletionSources);
    }

    [TestMethod]
    [DataRow("build")]
    [DataRow("sln")]
    public void IsDotnetBuiltInCommand_RecognizesBuiltInCommands(string commandName)
    {
        Assert.IsTrue(Parser.Parse([commandName]).IsDotnetBuiltInCommand());
    }

    [TestMethod]
    public void IsDotnetBuiltInCommand_RejectsExternalCommand()
        => Assert.IsFalse(Parser.Parse(["external-command"]).IsDotnetBuiltInCommand());

    [TestMethod]
    public void TryParseGenericCommandHelp_PreservesHelpSurfaceMetadataForAllInProcessCommands()
    {
        HashSet<string> rootCommandNames = Parser.RootCommand.Subcommands.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        Assert.IsTrue(s_externalHelpCommands.IsSubsetOf(rootCommandNames));

        foreach (Command fullCommand in Parser.RootCommand.Subcommands)
        {
            if (s_externalHelpCommands.Contains(fullCommand.Name))
            {
                continue;
            }

            foreach (string token in GetCommandTokens(fullCommand))
            {
                Assert.IsTrue(Parser.TryParseGenericCommandHelp([token, "--help"], out ParseResult? minimalParseResult), token);
                Command minimalCommand = minimalParseResult.RootCommandResult.Command.Subcommands.Single();
                AssertHelpSurfaceParity(fullCommand, minimalCommand, token);
            }
        }
    }

    [TestMethod]
    public void TryParseGenericCommandHelp_RendersSameOutputAsFullParserForAllInProcessCommands()
    {
        HashSet<string> rootCommandNames = Parser.RootCommand.Subcommands.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        Assert.IsTrue(s_externalHelpCommands.IsSubsetOf(rootCommandNames));
        Assert.IsTrue(s_networkCompletionHelpCommands.IsSubsetOf(rootCommandNames));

        foreach (Command command in Parser.RootCommand.Subcommands)
        {
            if (s_externalHelpCommands.Contains(command.Name) || s_networkCompletionHelpCommands.Contains(command.Name))
            {
                continue;
            }

            foreach (string token in GetCommandTokens(command))
            {
                AssertRendersSameOutputAsFullParser([token, "--help"], token);
            }
        }
    }

    [TestMethod]
    [DataRow("new -h")]
    [DataRow("new /h")]
    [DataRow("new --help")]
    [DataRow("new -?")]
    [DataRow("new /?")]
    [DataRow("new create -h")]
    [DataRow("new create /h")]
    [DataRow("new create --help")]
    [DataRow("new create -?")]
    [DataRow("new create /?")]
    [DataRow("build -h")]
    [DataRow("build /h")]
    [DataRow("build -?")]
    [DataRow("build /?")]
    public void TryParseGenericCommandHelp_RendersSameOutputAsFullParser(string commandLine)
        => AssertRendersSameOutputAsFullParser(commandLine.Split(' '), commandLine);

    private static IEnumerable<string> GetCommandTokens(Command command)
    {
        yield return command.Name;
        foreach (string alias in command.Aliases)
        {
            yield return alias;
        }
    }

    private static void AssertMinimalCommandTreeParity(string[] args, ParseResult parseResult)
    {
        string context = string.Join(' ', args);
        Assert.IsEmpty(parseResult.Errors, context);
        Assert.AreEqual("PrintHelpAction", parseResult.Action?.GetType().Name, context);
        Assert.IsTrue(parseResult.IsDotnetBuiltInCommand(), context);
        Assert.HasCount(1, parseResult.RootCommandResult.Command.Subcommands, context);

        ParseResult fullParseResult = Parser.Parse(args);
        Command minimalCommand = parseResult.RootCommandResult.Command.Subcommands.Single();
        Command fullCommand = fullParseResult.RootCommandResult.Command.Subcommands.Single(command => command.Name == minimalCommand.Name);
        Assert.AreEqual(fullParseResult.RootSubCommandResult(), parseResult.RootSubCommandResult(), context);
        Assert.AreEqual(fullCommand.GetType(), minimalCommand.GetType(), context);
        Assert.AreEqual(fullParseResult.GetCommandName(), parseResult.GetCommandName(), context);
        Assert.AreEqual(fullParseResult.CommandResult.Command.Name, parseResult.CommandResult.Command.Name, context);
        Assert.AreEqual(fullParseResult.Action?.GetType(), parseResult.Action?.GetType(), context);
    }

    private static void AssertRendersSameOutputAsFullParser(string[] args, string context)
    {
        Assert.IsTrue(Parser.TryParseGenericCommandHelp(args, out ParseResult? parseResult), context);

        (int fastExitCode, string fastOutput, string fastError) = InvokeWithCapture(parseResult);
        (int fullExitCode, string fullOutput, string fullError) = InvokeWithCapture(Parser.Parse(args));

        Assert.AreEqual(fullExitCode, fastExitCode, context);
        Assert.AreEqual(fullOutput, fastOutput, context);
        Assert.AreEqual(fullError, fastError, context);
    }

    private static void AssertHelpSurfaceParity(Command fullCommand, Command minimalCommand, string context)
    {
        Assert.AreEqual(fullCommand is ICustomHelp, minimalCommand is ICustomHelp, context);
        Assert.AreEqual(fullCommand.Description, minimalCommand.Description, context);
        Assert.AreSequenceEqual(
            fullCommand.Aliases.OrderBy(a => a, StringComparer.Ordinal).ToArray(),
            minimalCommand.Aliases.OrderBy(a => a, StringComparer.Ordinal).ToArray(),
            context);

        AssertArgumentsMatch(fullCommand.Arguments, minimalCommand.Arguments, context);

        Option[] fullOptions = fullCommand.Options.Where(o => !o.Hidden).ToArray();
        Option[] minimalOptions = minimalCommand.Options.Where(o => !o.Hidden).ToArray();
        Assert.HasCount(fullOptions.Length, minimalOptions, context);
        for (int i = 0; i < fullOptions.Length; i++)
        {
            string optionContext = $"{context} option '{fullOptions[i].Name}'";
            Assert.AreEqual(fullOptions[i].Name, minimalOptions[i].Name, optionContext);
            Assert.AreSequenceEqual(
                fullOptions[i].Aliases.OrderBy(a => a, StringComparer.Ordinal).ToArray(),
                minimalOptions[i].Aliases.OrderBy(a => a, StringComparer.Ordinal).ToArray(),
                optionContext);
            Assert.AreEqual(fullOptions[i].Description, minimalOptions[i].Description, optionContext);
            Assert.AreEqual(fullOptions[i].Required, minimalOptions[i].Required, optionContext);
            Assert.AreEqual(fullOptions[i].Arity, minimalOptions[i].Arity, optionContext);
            Assert.AreEqual(fullOptions[i].HasDefaultValue, minimalOptions[i].HasDefaultValue, optionContext);
            Assert.AreEqual(
                GetHelpCompletionSourceCount(fullOptions[i], fullOptions[i].HelpName, fullOptions[i].ValueType, fullOptions[i].Arity),
                GetHelpCompletionSourceCount(minimalOptions[i], minimalOptions[i].HelpName, minimalOptions[i].ValueType, minimalOptions[i].Arity),
                optionContext);
        }

        Command[] fullSubcommands = fullCommand.Subcommands.Where(c => !c.Hidden).ToArray();
        Command[] minimalSubcommands = minimalCommand.Subcommands.Where(c => !c.Hidden).ToArray();
        Assert.HasCount(fullSubcommands.Length, minimalSubcommands, context);
        for (int i = 0; i < fullSubcommands.Length; i++)
        {
            string subcommandContext = $"{context} subcommand '{fullSubcommands[i].Name}'";
            Assert.AreEqual(fullSubcommands[i].Name, minimalSubcommands[i].Name, subcommandContext);
            Assert.AreSequenceEqual(
                fullSubcommands[i].Aliases.OrderBy(a => a, StringComparer.Ordinal).ToArray(),
                minimalSubcommands[i].Aliases.OrderBy(a => a, StringComparer.Ordinal).ToArray(),
                subcommandContext);
            Assert.AreEqual(fullSubcommands[i].Description, minimalSubcommands[i].Description, subcommandContext);
            AssertArgumentsMatch(fullSubcommands[i].Arguments, minimalSubcommands[i].Arguments, subcommandContext);
        }
    }

    private static void AssertArgumentsMatch(IList<Argument> fullArguments, IList<Argument> minimalArguments, string context)
    {
        Argument[] visibleFull = fullArguments.Where(a => !a.Hidden).ToArray();
        Argument[] visibleMinimal = minimalArguments.Where(a => !a.Hidden).ToArray();
        Assert.HasCount(visibleFull.Length, visibleMinimal, context);
        for (int i = 0; i < visibleFull.Length; i++)
        {
            string argumentContext = $"{context} argument '{visibleFull[i].Name}'";
            Assert.AreEqual(visibleFull[i].Name, visibleMinimal[i].Name, argumentContext);
            Assert.AreEqual(visibleFull[i].HelpName, visibleMinimal[i].HelpName, argumentContext);
            Assert.AreEqual(visibleFull[i].Description, visibleMinimal[i].Description, argumentContext);
            Assert.AreEqual(visibleFull[i].Arity, visibleMinimal[i].Arity, argumentContext);
            Assert.AreEqual(visibleFull[i].HasDefaultValue, visibleMinimal[i].HasDefaultValue, argumentContext);
            Assert.AreEqual(
                GetHelpCompletionSourceCount(visibleFull[i], visibleFull[i].HelpName, visibleFull[i].ValueType, visibleFull[i].Arity),
                GetHelpCompletionSourceCount(visibleMinimal[i], visibleMinimal[i].HelpName, visibleMinimal[i].ValueType, visibleMinimal[i].Arity),
                argumentContext);
        }
    }

    private static int GetHelpCompletionSourceCount(Symbol symbol, string? helpName, Type valueType, ArgumentArity arity)
        => string.IsNullOrWhiteSpace(helpName)
            && valueType != typeof(bool)
            && valueType != typeof(bool?)
            && arity.MaximumNumberOfValues > 0
            ? symbol switch
            {
                Argument argument => argument.CompletionSources.Count,
                Option option => option.CompletionSources.Count,
                _ => 0
            }
            : 0;

    private static (int ExitCode, string Output, string Error) InvokeWithCapture(ParseResult parseResult)
    {
        StringWriter output = new();
        StringWriter error = new();
        InvocationConfiguration configuration = new()
        {
            Output = output,
            Error = error
        };

        return (parseResult.Invoke(configuration), output.ToString(), error.ToString());
    }

    private static int GetPackageIdCompletionSourceCount(Command command) => GetPackageIdArgument(command).CompletionSources.Count;

    private static Argument GetPackageIdArgument(Command command) => command switch
    {
        AddCommandDefinition add => add.PackageCommand.PackageIdArgument,
        PackageCommandDefinition package => package.AddCommand.PackageIdArgument,
        _ => throw new ArgumentException($"Unexpected command '{command.Name}'.", nameof(command))
    };
}
