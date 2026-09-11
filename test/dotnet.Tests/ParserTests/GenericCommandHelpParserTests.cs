// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.Add;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Extensions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests;

[TestClass]
public class GenericCommandHelpParserTests
{
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
    public void TryParseGenericCommandHelp_CoversEveryEligibleTopLevelCommand()
    {
        foreach (Command command in Parser.RootCommand.Subcommands)
        {
            bool expected = command.Name != "test";

            Assert.AreEqual(
                expected,
                Parser.TryParseGenericCommandHelp([command.Name, "--help"], out _),
                command.Name);
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
    [DataRow("add --help")]
    [DataRow("build-server --help")]
    [DataRow("clean --help")]
    [DataRow("complete --help")]
    [DataRow("completions --help")]
    [DataRow("dnx --help")]
    [DataRow("format --help")]
    [DataRow("fsi --help")]
    [DataRow("help --help")]
    [DataRow("internal-reportinstallsuccess --help")]
    [DataRow("list --help")]
    [DataRow("msbuild --help")]
    [DataRow("nuget --help")]
    [DataRow("pack --help")]
    [DataRow("package --help")]
    [DataRow("parse --help")]
    [DataRow("project --help")]
    [DataRow("publish --help")]
    [DataRow("reference --help")]
    [DataRow("remove --help")]
    [DataRow("restore --help")]
    [DataRow("run --help")]
    [DataRow("run-api --help")]
    [DataRow("sdk --help")]
    [DataRow("sln --help")]
    [DataRow("solution --help")]
    [DataRow("store --help")]
    [DataRow("tool --help")]
    [DataRow("vstest --help")]
    [DataRow("workload --help")]
    public void TryParseGenericCommandHelp_UsesMinimalCommandTree(string commandLine)
    {
        string[] args = commandLine.Split(' ');

        Assert.IsTrue(Parser.TryParseGenericCommandHelp(args, out ParseResult? parseResult));
        Assert.IsNotNull(parseResult);
        Assert.IsEmpty(parseResult.Errors);
        Assert.AreEqual("PrintHelpAction", parseResult.Action?.GetType().Name);
        Assert.IsTrue(parseResult.IsDotnetBuiltInCommand());
        Assert.HasCount(1, parseResult.RootCommandResult.Command.Subcommands);

        ParseResult fullParseResult = Parser.Parse(args);
        Command minimalCommand = parseResult.RootCommandResult.Command.Subcommands.Single();
        Command fullCommand = fullParseResult.RootCommandResult.Command.Subcommands.Single(command => command.Name == minimalCommand.Name);
        Assert.AreEqual(fullParseResult.RootSubCommandResult(), parseResult.RootSubCommandResult());
        Assert.AreEqual(fullCommand.GetType(), minimalCommand.GetType());
        Assert.AreEqual(fullParseResult.GetCommandName(), parseResult.GetCommandName());
        Assert.AreEqual(fullParseResult.CommandResult.Command.Name, parseResult.CommandResult.Command.Name);
        Assert.AreEqual(fullParseResult.Action?.GetType(), parseResult.Action?.GetType());
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
    [DataRow("new --help")]
    [DataRow("new create --help")]
    [DataRow("build --help")]
    [DataRow("add --help")]
    [DataRow("build-server --help")]
    [DataRow("clean --help")]
    [DataRow("complete --help")]
    [DataRow("completions --help")]
    [DataRow("dnx --help")]
    [DataRow("help --help")]
    [DataRow("internal-reportinstallsuccess --help")]
    [DataRow("list --help")]
    [DataRow("pack --help")]
    [DataRow("package --help")]
    [DataRow("parse --help")]
    [DataRow("project --help")]
    [DataRow("publish --help")]
    [DataRow("reference --help")]
    [DataRow("remove --help")]
    [DataRow("restore --help")]
    [DataRow("run --help")]
    [DataRow("run-api --help")]
    [DataRow("sdk --help")]
    [DataRow("sln --help")]
    [DataRow("solution --help")]
    [DataRow("store --help")]
    [DataRow("tool --help")]
    [DataRow("workload --help")]
    public void TryParseGenericCommandHelp_RendersSameOutputAsFullParser(string commandLine)
    {
        string[] args = commandLine.Split(' ');
        Assert.IsTrue(Parser.TryParseGenericCommandHelp(args, out ParseResult? parseResult));

        (int fastExitCode, string fastOutput, string fastError) = InvokeWithCapture(parseResult);
        (int fullExitCode, string fullOutput, string fullError) = InvokeWithCapture(Parser.Parse(args));

        Assert.AreEqual(fullExitCode, fastExitCode);
        Assert.AreEqual(fullOutput, fastOutput);
        Assert.AreEqual(fullError, fastError);
    }

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

    private static int GetPackageIdCompletionSourceCount(Command command) => command switch
    {
        AddCommandDefinition add => add.PackageCommand.PackageIdArgument.CompletionSources.Count,
        PackageCommandDefinition package => package.AddCommand.PackageIdArgument.CompletionSources.Count,
        _ => throw new ArgumentException($"Unexpected command '{command.Name}'.", nameof(command))
    };
}
