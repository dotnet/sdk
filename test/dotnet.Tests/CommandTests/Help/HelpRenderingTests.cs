// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Hidden.List;
using Microsoft.DotNet.Cli.Help;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
public class HelpRenderingTests
{
    [TestMethod]
    public void ConfiguredParserHelpDoesNotMutateSharedDefinition()
    {
        Action assertUnchanged = CaptureState(Parser.RootCommand);
        HelpBuilder builder = Parser.DotnetHelpBuilder.Instance.Value;
        Render(builder, Parser.RootCommand.AddCommand.PackageCommand);
        Render(builder, Parser.RootCommand.PackageCommand.AddCommand);
        Render(builder, Parser.RootCommand.WorkloadCommand.SearchCommand);
        Render(builder, Parser.RootCommand.WorkloadCommand.SearchCommand.VersionCommand);
        Render(builder, Parser.RootCommand.BuildCommand);
        assertUnchanged();
    }

    [TestMethod]
    public void HelpPreservesSharedSymbolsAndCompletions()
    {
        DotNetCommandDefinition root = new();
        Parser.DotnetHelpBuilder builder = new();
        Parser.DotnetHelpBuilder.ConfigureHelp(root, builder);
        root.AddCommand.ProjectOrFileArgument.CompletionSources.Add(_ => [new CompletionItem("project.csproj")]);
        root.AddCommand.PackageCommand.PackageIdArgument.CompletionSources.Add(_ => [new CompletionItem("Example.Package")]);
        root.PackageCommand.AddCommand.PackageIdArgument.CompletionSources.Add(_ => [new CompletionItem("Example.Package")]);
        Action assertUnchanged = CaptureState(root);
        string versionDescription = root.WorkloadCommand.SearchCommand.VersionCommand.Description!;

        foreach (Command command in new Command[]
        {
            root.AddCommand,
            root.AddCommand.PackageCommand,
            root.PackageCommand.AddCommand,
            root.ListCommand.ReferenceCommand,
            root.WorkloadCommand.SearchCommand,
            root.WorkloadCommand.SearchCommand.VersionCommand,
            root.BuildCommand
        })
        {
            string help = Render(builder, command);
            Assert.AreEqual(help, Render(builder, command));
            assertUnchanged();
        }

        Assert.AreSequenceEqual(["project.csproj"], root.AddCommand.ProjectOrFileArgument.GetCompletions(CompletionContext.Empty).Select(c => c.Label).ToArray());
        Assert.AreSequenceEqual(["Example.Package"], root.AddCommand.PackageCommand.PackageIdArgument.GetCompletions(CompletionContext.Empty).Select(c => c.Label).ToArray());
        Assert.AreSequenceEqual(["Example.Package"], root.PackageCommand.AddCommand.PackageIdArgument.GetCompletions(CompletionContext.Empty).Select(c => c.Label).ToArray());
        Assert.DoesNotContain("Example.Package", Render(builder, root.AddCommand.PackageCommand));
        Assert.DoesNotContain("Example.Package", Render(builder, root.PackageCommand.AddCommand));
        Assert.Contains(CliStrings.ShortWorkloadSearchVersionDescription, Render(builder, root.WorkloadCommand.SearchCommand));
        Assert.Contains(versionDescription.Split('\n')[0].TrimEnd('\r'), Render(builder, root.WorkloadCommand.SearchCommand.VersionCommand));
    }

    [TestMethod]
    public void ParentHelpDoesNotInvokeCustomizedArgumentCompletions()
    {
        DotNetCommandDefinition root = new();
        Parser.DotnetHelpBuilder builder = new();
        Parser.DotnetHelpBuilder.ConfigureHelp(root, builder);
        int completionCalls = 0;
        root.AddCommand.PackageCommand.PackageIdArgument.CompletionSources.Add(_ =>
        {
            completionCalls++;
            return [new CompletionItem("Example.Package")];
        });
        root.PackageCommand.AddCommand.PackageIdArgument.CompletionSources.Add(_ =>
        {
            completionCalls++;
            return [new CompletionItem("Example.Package")];
        });
        Action assertUnchanged = CaptureState(root);

        Assert.DoesNotContain("Example.Package", Render(builder, root.AddCommand));
        Assert.DoesNotContain("Example.Package", Render(builder, root.PackageCommand));
        Assert.AreEqual(0, completionCalls);
        assertUnchanged();
    }

    [TestMethod]
    public void HelpParseAndCompletionAreIndependentOfRenderOrder()
    {
        DotNetCommandDefinition root = new();
        Parser.DotnetHelpBuilder builder = new();
        Parser.DotnetHelpBuilder.ConfigureHelp(root, builder);
        Command[] commands =
        [
            root.ListCommand,
            root.ListCommand.ReferenceCommand,
            root.ListCommand.PackageCommand,
            root.WorkloadCommand.SearchCommand,
            root.WorkloadCommand.SearchCommand.VersionCommand,
            root.AddCommand.PackageCommand,
            root.PackageCommand.AddCommand
        ];
        string[] initialHelp = commands.Select(command => Render(builder, command)).ToArray();
        string[] completion = root.Parse("package add --").GetCompletions().Select(c => c.Label).ToArray();

        for (int i = commands.Length - 1; i >= 0; i--)
        {
            Assert.AreEqual(initialHelp[i], Render(builder, commands[i]));
            ParseResult legacy = root.Parse("add project.csproj package Example.Package@1.2.3");
            Assert.IsEmpty(legacy.Errors);
            Assert.AreEqual("project.csproj", legacy.GetValue(root.AddCommand.ProjectOrFileArgument));
            Assert.AreEqual("Example.Package", legacy.GetValue(root.AddCommand.PackageCommand.PackageIdArgument).Id);
            ParseResult current = root.Parse("package add Example.Package@1.2.3 --project project.csproj");
            Assert.IsEmpty(current.Errors);
            Assert.AreEqual("project.csproj", current.GetValue(root.PackageCommand.AddCommand.ProjectOption));
            Assert.AreEqual("Example.Package", current.GetValue(root.PackageCommand.AddCommand.PackageIdArgument).Id);
            Assert.AreSequenceEqual(completion, root.Parse("package add --").GetCompletions().Select(c => c.Label).ToArray());
        }
    }

    [TestMethod]
    public void ListReferenceHelpCustomizesOnlyTheDisplayedParentArgument()
    {
        DotNetCommandDefinition root = new();
        Argument<string> argument = ListCommandDefinition.CreateSlnOrProjectArgument(CliStrings.SolutionOrProjectArgumentName, "Original description");
        root.ListCommand.Arguments[0] = argument;
        Parser.DotnetHelpBuilder builder = new();
        Parser.DotnetHelpBuilder.ConfigureHelp(root, builder);
        Action assertUnchanged = CaptureState(root);
        string parentHelp = Render(builder, root.ListCommand);
        string packageHelp = Render(builder, root.ListCommand.PackageCommand);

        string referenceHelp = Render(builder, root.ListCommand.ReferenceCommand);

        Assert.Contains($"[<{CliStrings.ProjectArgumentName}>]", referenceHelp);
        Assert.Contains(CliStrings.ProjectArgumentDescription, referenceHelp);
        Assert.DoesNotContain("Original description", referenceHelp);
        Assert.AreEqual(parentHelp, Render(builder, root.ListCommand));
        Assert.AreEqual(packageHelp, Render(builder, root.ListCommand.PackageCommand));
        ParseResult parsed = root.Parse("list project.csproj reference");
        Assert.IsEmpty(parsed.Errors);
        Assert.AreEqual("project.csproj", parsed.GetValue(argument));
        assertUnchanged();
    }

    [TestMethod]
    public void InheritedOptionsDoNotGainHelpNamesFromParentHelp()
    {
        Option<string> value = new("--value") { Recursive = true };
        Option<string> explicitName = new("--explicit") { Recursive = true, HelpName = "VALUE" };
        Command child = new("child");
        Command parent = new("parent") { Options = { value, explicitName }, Subcommands = { child } };
        HelpBuilder builder = new();
        Action assertUnchanged = CaptureState(parent);

        string childHelp = Render(builder, child);
        Assert.Contains("--explicit <VALUE>", childHelp);
        Assert.DoesNotContain("--value <value>", childHelp);
        Assert.Contains("--value <value>", Render(builder, parent));
        Assert.AreEqual(childHelp, Render(builder, child));
        assertUnchanged();
    }

    [TestMethod]
    public void OptionLabelsDoNotSetHelpNamesOrChangeAliases()
    {
        Option<string> value = new("--value", "-v");
        Option<int> count = new("/count");
        Option<bool> flag = new("--flag");
        Option<bool?> nullableFlag = new("--nullable");
        Option<string> zeroArity = new("--project") { Arity = ArgumentArity.Zero };
        Option<string> helpOption = new("--help", "-h", "-?") { Arity = ArgumentArity.Zero };
        Option<string> explicitName = new("--explicit") { HelpName = "VALUE" };
        Option<string> choice = new("--choice");
        choice.CompletionSources.Add(_ => [new CompletionItem("one"), new CompletionItem("two")]);
        Command command = new("example") { Options = { value, count, flag, nullableFlag, zeroArity, helpOption, explicitName, choice } };
        Action assertUnchanged = CaptureState(command);
        HelpBuilder builder = new();

        string help = Render(builder, command);

        Assert.Contains("-v, --value <value>", help);
        Assert.Contains("/count <count>", help);
        Assert.AreEqual("--flag", HelpBuilder.Default.GetOptionUsageLabel(flag));
        Assert.AreEqual("--nullable", HelpBuilder.Default.GetOptionUsageLabel(nullableFlag));
        Assert.AreEqual("--project", HelpBuilder.Default.GetOptionUsageLabel(zeroArity));
        Assert.AreEqual("-?, -h, --help", HelpBuilder.Default.GetOptionUsageLabel(helpOption));
        Assert.Contains("--explicit <VALUE>", help);
        Assert.Contains("--choice <one|two>", help);
        Assert.AreEqual("--detached <detached>", builder.GetTwoColumnRow(
            new Option<string>("--detached"), new HelpContext(builder, command, TextWriter.Null)).FirstColumnText);
        Assert.AreEqual(help, Render(builder, command));
        Assert.IsEmpty(command.Parse("-v text /count 2 --flag --choice one").Errors);
        assertUnchanged();
    }

    private static string Render(HelpBuilder builder, Command command)
    {
        using StringWriter output = new();
        builder.Write(new HelpContext(builder, command, output));
        return output.ToString();
    }

    private static Action CaptureState(Command root)
    {
        List<Action> assertions = [];
        HashSet<Symbol> visited = [];
        Capture(root);
        return () =>
        {
            foreach (Action assertion in assertions)
            {
                assertion();
            }
        };

        void Capture(Symbol symbol)
        {
            if (!visited.Add(symbol))
            {
                return;
            }

            string name = symbol.Name;
            string? description = symbol.Description;
            bool hidden = symbol.Hidden;
            Symbol[] parents = symbol.Parents.ToArray();
            assertions.Add(() =>
            {
                Assert.AreEqual(name, symbol.Name);
                Assert.AreEqual(description, symbol.Description);
                Assert.AreEqual(hidden, symbol.Hidden);
                Assert.AreSequenceEqual(parents, symbol.Parents.ToArray());
            });

            switch (symbol)
            {
                case Command command:
                    Argument[] arguments = command.Arguments.ToArray();
                    Option[] options = command.Options.ToArray();
                    Command[] subcommands = command.Subcommands.ToArray();
                    string[] commandAliases = command.Aliases.ToArray();
                    assertions.Add(() =>
                    {
                        Assert.AreSequenceEqual(arguments, command.Arguments.ToArray());
                        Assert.AreSequenceEqual(options, command.Options.ToArray());
                        Assert.AreSequenceEqual(subcommands, command.Subcommands.ToArray());
                        Assert.AreSequenceEqual(commandAliases, command.Aliases.ToArray());
                    });
                    foreach (Symbol child in command.Children)
                    {
                        Capture(child);
                    }
                    break;
                case Argument argument:
                    string? argumentHelpName = argument.HelpName;
                    ArgumentArity argumentArity = argument.Arity;
                    var argumentCompletions = argument.CompletionSources.ToArray();
                    var argumentValidators = argument.Validators.ToArray();
                    assertions.Add(() =>
                    {
                        Assert.AreEqual(argumentHelpName, argument.HelpName);
                        Assert.AreEqual(argumentArity, argument.Arity);
                        Assert.AreSequenceEqual(argumentCompletions, argument.CompletionSources.ToArray());
                        Assert.AreSequenceEqual(argumentValidators, argument.Validators.ToArray());
                    });
                    break;
                case Option option:
                    string? optionHelpName = option.HelpName;
                    ArgumentArity optionArity = option.Arity;
                    string[] aliases = option.Aliases.ToArray();
                    var optionCompletions = option.CompletionSources.ToArray();
                    var optionValidators = option.Validators.ToArray();
                    assertions.Add(() =>
                    {
                        Assert.AreEqual(optionHelpName, option.HelpName);
                        Assert.AreEqual(optionArity, option.Arity);
                        Assert.AreSequenceEqual(aliases, option.Aliases.ToArray());
                        Assert.AreSequenceEqual(optionCompletions, option.CompletionSources.ToArray());
                        Assert.AreSequenceEqual(optionValidators, option.Validators.ToArray());
                    });
                    break;
            }
        }
    }
}
