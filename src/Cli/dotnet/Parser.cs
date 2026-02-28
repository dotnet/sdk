// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.StaticCompletions;
using System.Reflection;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Build;
using Microsoft.DotNet.Cli.Commands.BuildServer;
using Microsoft.DotNet.Cli.Commands.Clean;
using Microsoft.DotNet.Cli.Commands.Dnx;
using Microsoft.DotNet.Cli.Commands.Format;
using Microsoft.DotNet.Cli.Commands.Fsi;
using Microsoft.DotNet.Cli.Commands.Help;
using Microsoft.DotNet.Cli.Commands.Hidden.Add;
using Microsoft.DotNet.Cli.Commands.Hidden.Add.Package;
using Microsoft.DotNet.Cli.Commands.Hidden.Complete;
using Microsoft.DotNet.Cli.Commands.Hidden.InternalReportInstallSuccess;
using Microsoft.DotNet.Cli.Commands.Hidden.List;
using Microsoft.DotNet.Cli.Commands.Hidden.List.Reference;
using Microsoft.DotNet.Cli.Commands.Hidden.Parse;
using Microsoft.DotNet.Cli.Commands.Hidden.Remove;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.New;
using Microsoft.DotNet.Cli.Commands.NuGet;
using Microsoft.DotNet.Cli.Commands.Pack;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Commands.Project;
using Microsoft.DotNet.Cli.Commands.Publish;
using Microsoft.DotNet.Cli.Commands.Reference;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Run.Api;
using Microsoft.DotNet.Cli.Commands.Sdk;
using Microsoft.DotNet.Cli.Commands.Solution;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Tool;
using Microsoft.DotNet.Cli.Commands.Tool.Store;
using Microsoft.DotNet.Cli.Commands.VSTest;
using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Commands.Workload.Search;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Help;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Cli.Help;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli;

public static class Parser
{
    private static DotNetCommandDefinition CreateCommand()
    {
        var rootCommand = new DotNetCommandDefinition();

        for (int i = rootCommand.Options.Count - 1; i >= 0; i--)
        {
            Option option = rootCommand.Options[i];

            if (option is VersionOption)
            {
                rootCommand.Options.RemoveAt(i);
            }
            else if (option is System.CommandLine.Help.HelpOption helpOption)
            {
                helpOption.Action = new DotnetHelpAction()
                {
                    Builder = DotnetHelpBuilder.Instance.Value
                };

                option.Description = CliStrings.ShowHelpDescription;
            }
        }

        // Augment the definition of each subcommand with command-specific actions and completions.

        AddCommandParser.ConfigureCommand(rootCommand.AddCommand);
        BuildCommandParser.ConfigureCommand(rootCommand.BuildCommand);
        BuildServerCommandParser.ConfigureCommand(rootCommand.BuildServerCommand);
        CleanCommandParser.ConfigureCommand(rootCommand.CleanCommand);
        DnxCommandParser.ConfigureCommand(rootCommand.DnxCommand);
        FormatCommandParser.ConfigureCommand(rootCommand.FormatCommand);
        CompleteCommandParser.ConfigureCommand(rootCommand.CompleteCommand);
        FsiCommandParser.ConfigureCommand(rootCommand.FsiCommand);
        ListCommandParser.ConfigureCommand(rootCommand.ListCommand);
        MSBuildCommandParser.ConfigureCommand(rootCommand.MSBuildCommand);

        // Currently `new` command implementation replaces the definition entirely:
        rootCommand.Subcommands[rootCommand.Subcommands.IndexOf(rootCommand.NewCommand)] = NewCommandParser.ConfigureCommand(rootCommand.NewCommand);

        // TODO: https://github.com/dotnet/sdk/issues/52661
        // https://github.com/NuGet/NuGet.Client/blob/bf048eb714eb6b1912ba868edca4c7cfec454841/src/NuGet.Core/NuGet.CommandLine.XPlat/Commands/Why/WhyCommand.cs
        // Add `why` subcommand to the definition instead.
        var nugetCommand = rootCommand.NuGetCommand;
        NuGet.CommandLine.XPlat.Commands.Why.WhyCommand.GetWhyCommand(nugetCommand);

        NuGetCommandParser.ConfigureCommand(nugetCommand);

        PackCommandParser.ConfigureCommand(rootCommand.PackCommand);
        PackageCommandParser.ConfigureCommand(rootCommand.PackageCommand);
        ParseCommandParser.ConfigureCommand(rootCommand.ParseCommand);
        ProjectCommandParser.ConfigureCommand(rootCommand.ProjectCommand);
        PublishCommandParser.ConfigureCommand(rootCommand.PublishCommand);
        ReferenceCommandParser.ConfigureCommand(rootCommand.ReferenceCommand);
        RemoveCommandParser.ConfigureCommand(rootCommand.RemoveCommand);
        RestoreCommandParser.ConfigureCommand(rootCommand.RestoreCommand);
        RunCommandParser.ConfigureCommand(rootCommand.RunCommand);
        RunApiCommandParser.ConfigureCommand(rootCommand.RunApiCommand);
        SolutionCommandParser.ConfigureCommand(rootCommand.SolutionCommand);
        StoreCommandParser.ConfigureCommand(rootCommand.StoreCommand);
        TestCommandParser.ConfigureCommand(rootCommand.TestCommand);
        ToolCommandParser.ConfigureCommand(rootCommand.ToolCommand);
        VSTestCommandParser.ConfigureCommand(rootCommand.VSTestCommand);
        HelpCommandParser.ConfigureCommand(rootCommand.HelpCommand);
        SdkCommandParser.ConfigureCommand(rootCommand.SdkCommand);
        InternalReportInstallSuccessCommandParser.ConfigureCommand(rootCommand.InternalReportInstallSuccessCommand);
        WorkloadCommandParser.ConfigureCommand(rootCommand.WorkloadCommand);
        CompletionsCommandParser.ConfigureCommand(rootCommand.CompletionsCommand);

        rootCommand.CliSchemaOption.Action = new PrintCliSchemaAction();

        // TODO: https://github.com/dotnet/sdk/issues/52661
        // https://github.com/NuGet/NuGet.Client/blob/bf048eb714eb6b1912ba868edca4c7cfec454841/src/NuGet.Core/NuGet.CommandLine.XPlat/NuGetCommands.cs
        // Add `package` subcommands to the definition instead.
        NuGet.CommandLine.XPlat.NuGetCommands.Add(rootCommand, CommonOptions.CreateInteractiveOption(acceptArgument: true));

        rootCommand.SetAction(parseResult =>
        {
            if (parseResult.GetValue(rootCommand.DiagOption) && parseResult.Tokens.Count == 1)
            {
                // when user does not specify any args except of diagnostics ("dotnet -d"), we do nothing
                // as Program.ProcessArgs already enabled the diagnostic output
                return 0;
            }
            else
            {
                // when user does not specify any args (just "dotnet"), a usage needs to be printed
                parseResult.InvocationConfiguration.Output.WriteLine(CliUsage.HelpText);
                return 0;
            }
        });

        return rootCommand;
    }

    public static Command GetBuiltInCommand(string commandName) =>
        RootCommand.Subcommands.FirstOrDefault(c => c.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Implements token-per-line response file handling for the CLI. We use this instead of the built-in S.CL handling
    /// to ensure backwards-compatibility with MSBuild.
    /// </summary>
    public static bool TokenPerLine(string tokenToReplace, out IReadOnlyList<string> replacementTokens, out string errorMessage)
    {
        var filePath = Path.GetFullPath(tokenToReplace);
        if (File.Exists(filePath))
        {
            var lines = File.ReadAllLines(filePath);
            var trimmedLines =
                lines
                    // Remove content in the lines that start with # after trimmer leading whitespace
                    .Select(line => line.TrimStart().StartsWith('#') ? string.Empty : line)
                    // trim leading/trailing whitespace to not pass along dead spaces
                    .Select(x => x.Trim())
                    // Remove empty lines
                    .Where(line => line.Length > 0);
            replacementTokens = [.. trimmedLines];
            errorMessage = null;
            return true;
        }
        else
        {
            replacementTokens = null;
            errorMessage = string.Format(CliStrings.ResponseFileNotFound, tokenToReplace);
            return false;
        }
    }

    public static ParserConfiguration ParserConfiguration { get; } = new()
    {
        EnablePosixBundling = false,
        ResponseFileTokenReplacer = TokenPerLine
    };

    public static InvocationConfiguration InvocationConfiguration { get; } = new()
    {
        EnableDefaultExceptionHandler = false,
    };

    /// <summary>
    /// The root command for the .NET CLI.
    /// </summary>
    /// <remarks>
    /// If you use this Command directly, you _must_ use <see cref="ParserConfiguration"/>
    /// and <see cref="InvocationConfiguration"/> to ensure that the command line parser
    /// and invoker are configured correctly.
    /// </remarks>
    internal static DotNetCommandDefinition RootCommand { get; } = CreateCommand();

    /// <summary>
    /// You probably want to use <see cref="Parse(string[])"/> instead of this method.
    /// This has to internally split the string into an array of arguments
    /// before parsing, which is not as efficient as using the array overload.
    /// And also won't always split tokens the way the user will expect on their shell.
    /// </summary>
    public static ParseResult Parse(string commandLineUnsplit) => RootCommand.Parse(commandLineUnsplit, ParserConfiguration);
    public static ParseResult Parse(string[] args) => RootCommand.Parse(args, ParserConfiguration);
    public static int Invoke(ParseResult parseResult) => parseResult.Invoke(InvocationConfiguration);
    public static Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default) => parseResult.InvokeAsync(InvocationConfiguration, cancellationToken);
    public static int Invoke(string[] args) => Invoke(Parse(args));
    public static Task<int> InvokeAsync(string[] args, CancellationToken cancellationToken = default) => InvokeAsync(Parse(args), cancellationToken);

    internal static int ExceptionHandler(Exception exception, ParseResult parseResult)
    {
        if (exception is TargetInvocationException)
        {
            exception = exception.InnerException;
        }

        if (exception is GracefulException)
        {
            Reporter.Error.WriteLine(CommandLoggingContext.IsVerbose ?
                exception.ToString().Red().Bold() :
                exception.Message.Red().Bold());
        }
        else if (exception is CommandParsingException)
        {
            Reporter.Error.WriteLine(CommandLoggingContext.IsVerbose ?
                exception.ToString().Red().Bold() :
                exception.Message.Red().Bold());
            parseResult.ShowHelp();
        }
        else if (exception.GetType().Name.Equals("WorkloadManifestCompositionException"))
        {
            Reporter.Error.WriteLine(CommandLoggingContext.IsVerbose ?
                exception.ToString().Red().Bold() :
                exception.Message.Red().Bold());
        }
        else
        {
            Reporter.Error.Write("Unhandled exception: ".Red().Bold());
            Reporter.Error.WriteLine(CommandLoggingContext.IsVerbose ?
                exception.ToString().Red().Bold() :
                exception.Message.Red().Bold());
        }

        return 1;
    }

    internal class DotnetHelpBuilder : HelpBuilder
    {
        private DotnetHelpBuilder(int maxWidth = int.MaxValue) : base(maxWidth) { }

        public static Lazy<HelpBuilder> Instance = new(() =>
        {
            int windowWidth;
            try
            {
                windowWidth = Console.WindowWidth;
            }
            catch
            {
                windowWidth = int.MaxValue;
            }

            DotnetHelpBuilder dotnetHelpBuilder = new(windowWidth);

            return dotnetHelpBuilder;
        });

        public static void additionalOption(HelpContext context)
        {
            List<TwoColumnHelpRow> options = [];
            HashSet<Option> uniqueOptions = [];
            foreach (Option option in context.Command.Options)
            {
                if (!option.Hidden && uniqueOptions.Add(option))
                {
                    options.Add(context.HelpBuilder.GetTwoColumnRow(option, context));
                }
            }

            if (options.Count <= 0)
            {
                return;
            }

            context.Output.WriteLine(CliStrings.MSBuildAdditionalOptionTitle);
            context.HelpBuilder.WriteColumns(options, context);
            context.Output.WriteLine();
        }

        public override void Write(HelpContext context)
        {
            var command = context.Command;
            var helpArgs = new string[] { "--help" };

            // custom help overrides
            if (command.Equals(RootCommand))
            {
                Console.Out.WriteLine(CliUsage.HelpText);
                return;
            }

            // argument/option cleanups specific to help
            foreach (var option in command.Options)
            {
                option.EnsureHelpName();
            }

            if (command.GetRootCommand() is NuGetCommandDefinition)
            {
                NuGetCommand.Run(context.ParseResult);
            }
            else if (command is MSBuildCommandDefinition)
            {
                new MSBuildForwardingApp(MSBuildArgs.ForHelp).Execute();
                context.Output.WriteLine();
                additionalOption(context);
            }
            else if (command is VSTestCommandDefinition)
            {
                new VSTestForwardingApp(helpArgs).Execute();
            }
            else if (command is FormatCommandDefinition format)
            {
                var arguments = context.ParseResult.GetValue(format.Arguments);
                new FormatForwardingApp([.. arguments, .. helpArgs]).Execute();
            }
            else if (command is FsiCommandDefinition)
            {
                new FsiForwardingApp(helpArgs).Execute();
            }
            else if (command is ICustomHelp helpCommand)
            {
                var blocks = helpCommand.CustomHelpLayout();
                foreach (var block in blocks)
                {
                    block(context);
                }
            }
            else
            {
                // TODO: avoid modifying the commands:
                // https://github.com/dotnet/sdk/issues/52136

                if (command.Name.Equals(ListReferenceCommandDefinition.Name))
                {
                    Command listCommand = command.Parents.Single() as Command;

                    for (int i = 0; i < listCommand.Arguments.Count; i++)
                    {
                        if (listCommand.Arguments[i].Name == CliStrings.SolutionOrProjectArgumentName)
                        {
                            // Name is immutable now, so we create a new Argument with the right name..
                            listCommand.Arguments[i] = Commands.Hidden.List.ListCommandDefinition.CreateSlnOrProjectArgument(CliStrings.ProjectArgumentName, CliStrings.ProjectArgumentDescription);
                        }
                    }
                }
                else if (command.Name.Equals(AddPackageCommandDefinition.Name) || command.Name.Equals(AddCommandDefinition.Name))
                {
                    // Don't show package completions in help
                    foreach (var argument in command.Arguments)
                    {
                        argument.CompletionSources.Clear();
                    }
                }
                else if (command is WorkloadSearchCommandDefinition workloadSearchCommand)
                {
                    // Set shorter description for displaying parent command help.
                    workloadSearchCommand.VersionCommand.Description = CliStrings.ShortWorkloadSearchVersionDescription;
                }

                base.Write(context);
            }
        }
    }

    private class PrintCliSchemaAction : SynchronousCommandLineAction
    {
        public override bool Terminating => true;

        public override int Invoke(ParseResult parseResult)
        {
            CliSchema.PrintCliSchema(parseResult.CommandResult, parseResult.InvocationConfiguration.Output, Program.TelemetryClient);
            return 0;
        }
    }
}
