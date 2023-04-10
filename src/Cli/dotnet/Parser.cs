// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Help;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Format;
using Microsoft.DotNet.Tools.Help;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.NuGet;
using CommandResult = System.CommandLine.Parsing.CommandResult;

namespace Microsoft.DotNet.Cli
{
    public static class Parser
    {
        public static readonly CliRootCommand RootCommand = new();

        internal static Dictionary<CliOption, Dictionary<CliCommand, string>> HelpDescriptionCustomizations = new();

        public static readonly CliCommand InstallSuccessCommand = InternalReportinstallsuccessCommandParser.GetCommand();

        // Subcommands
        public static readonly CliCommand[] Subcommands = new CliCommand[]
        {
            AddCommandParser.GetCommand(),
            BuildCommandParser.GetCommand(),
            BuildServerCommandParser.GetCommand(),
            CleanCommandParser.GetCommand(),
            FormatCommandParser.GetCommand(),
            CompleteCommandParser.GetCommand(),
            FsiCommandParser.GetCommand(),
            ListCommandParser.GetCommand(),
            MSBuildCommandParser.GetCommand(),
            NewCommandParser.GetCommand(),
            NuGetCommandParser.GetCommand(),
            PackCommandParser.GetCommand(),
            ParseCommandParser.GetCommand(),
            PublishCommandParser.GetCommand(),
            RemoveCommandParser.GetCommand(),
            RestoreCommandParser.GetCommand(),
            RunCommandParser.GetCommand(),
            SlnCommandParser.GetCommand(),
            StoreCommandParser.GetCommand(),
            TestCommandParser.GetCommand(),
            ToolCommandParser.GetCommand(),
            VSTestCommandParser.GetCommand(),
            HelpCommandParser.GetCommand(),
            SdkCommandParser.GetCommand(),
            InstallSuccessCommand,
            WorkloadCommandParser.GetCommand()
        };

        public static readonly CliOption<bool> DiagOption = CommonOptionsFactory.CreateDiagnosticsOption(recursive: false);

        public static readonly CliOption<bool> VersionOption = new("--version");

        public static readonly CliOption<bool> InfoOption = new("--info");

        public static readonly CliOption<bool> ListSdksOption = new("--list-sdks");

        public static readonly CliOption<bool> ListRuntimesOption = new("--list-runtimes");

        // Argument
        public static readonly CliArgument<string> DotnetSubCommand = new("subcommand") { Arity = ArgumentArity.ZeroOrOne, Hidden = true };

        private static CliCommand ConfigureCommandLine(CliCommand rootCommand)
        {
            foreach (CliOption option in rootCommand.Options)
            {
                if (option is HelpOption)
                {
                    option.Description = CommandLineValidation.LocalizableStrings.ShowHelpInfo;
                    break;
                }
            }

            // Add subcommands
            foreach (var subcommand in Subcommands)
            {
                rootCommand.Subcommands.Add(subcommand);
            }

            // Add options
            rootCommand.Options.Add(DiagOption);
            rootCommand.Options.Add(VersionOption);
            rootCommand.Options.Add(InfoOption);
            rootCommand.Options.Add(ListSdksOption);
            rootCommand.Options.Add(ListRuntimesOption);

            // Add argument
            rootCommand.Arguments.Add(DotnetSubCommand);

            return rootCommand;
        }

        private static CliConfiguration DisablePosixBinding(this CliConfiguration config)
        {
            config.EnablePosixBundling = false;
            return config;
        }

        public static CliCommand GetBuiltInCommand(string commandName)
        {
            return Subcommands
                .FirstOrDefault(c => c.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Implements token-per-line response file handling for the CLI. We use this instead of the built-in S.CL handling
        /// to ensure backwards-compatibility with MSBuild.
        /// </summary>
        public static bool TokenPerLine(string tokenToReplace, out IReadOnlyList<string> replacementTokens, out string errorMessage) {
            var filePath = Path.GetFullPath(tokenToReplace);
            if (File.Exists(filePath)) {
                var lines = File.ReadAllLines(filePath);
                var trimmedLines =
                    lines
                        // Remove content in the lines that start with # after trimmer leading whitespace
                        .Select(line => line.TrimStart().StartsWith('#') ? string.Empty : line)
                        // trim leading/trailing whitespace to not pass along dead spaces
                        .Select(x => x.Trim())
                        // Remove empty lines
                        .Where(line => line.Length > 0);
                replacementTokens = trimmedLines.ToArray();
                errorMessage = null;
                return true;
            } else {
                replacementTokens = null;
                errorMessage = string.Format(CommonLocalizableStrings.ResponseFileNotFound, tokenToReplace);
                return false;
            }
        }

        public static CliConfiguration Instance { get; } = new(ConfigureCommandLine(RootCommand))
        {
            EnableDefaultExceptionHandler = false,
            EnableParseErrorReporting = true,
            EnablePosixBundling = false,
            Directives = { new DiagramDirective(), new SuggestDirective() },
            ResponseFileTokenReplacer = TokenPerLine
        };

        internal class DotnetHelpBuilder : HelpBuilder
        {
            private DotnetHelpBuilder(int maxWidth = int.MaxValue) : base(maxWidth) { }

            public static Lazy<HelpBuilder> Instance = new(() => {
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

                SetHelpCustomizations(dotnetHelpBuilder);

                return dotnetHelpBuilder;
            });

            private static void SetHelpCustomizations(HelpBuilder builder)
            {
                foreach (var option in HelpDescriptionCustomizations.Keys)
                {
                    Func<HelpContext, string> descriptionCallback = (HelpContext context) =>
                    {
                        foreach (var (command, helpText) in HelpDescriptionCustomizations[option])
                        {
                            if (context.ParseResult.CommandResult.Command.Equals(command))
                            {
                                return helpText;
                            }
                        }
                        return null;
                    };
                    builder.CustomizeSymbol(option, secondColumnText: descriptionCallback);
                }
            }

            public void additionalOption(HelpContext context)
            {
                List<TwoColumnHelpRow> options = new();
                HashSet<CliOption> uniqueOptions = new();
                foreach (CliOption option in context.Command.Options)
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

                context.Output.WriteLine(CommonLocalizableStrings.MSBuildAdditionalOptionTitle);
                context.HelpBuilder.WriteColumns(options, context);
                context.Output.WriteLine();
            }

            public override void Write(HelpContext context)
            {
                var command = context.Command;
                var helpArgs = new string[] { "--help" };
                if (command.Equals(RootCommand))
                {
                    Console.Out.WriteLine(HelpUsageText.UsageText);
                }
                else if (command.Name.Equals(NuGetCommandParser.GetCommand().Name))
                {
                    NuGetCommand.Run(context.ParseResult);
                }
                else if (command.Name.Equals(MSBuildCommandParser.GetCommand().Name))
                {
                    new MSBuildForwardingApp(helpArgs).Execute();
                    context.Output.WriteLine();
                    additionalOption(context);
                }
                else if (command.Name.Equals(VSTestCommandParser.GetCommand().Name))
                {
                    new VSTestForwardingApp(helpArgs).Execute();
                }
                else if (command.Name.Equals(FormatCommandParser.GetCommand().Name))
                {
                    var argumetns = context.ParseResult.GetValue(FormatCommandParser.Arguments);
                    new DotnetFormatForwardingApp(argumetns.Concat(helpArgs).ToArray()).Execute();
                }
                else if (command.Name.Equals(FsiCommandParser.GetCommand().Name))
                {
                    new FsiForwardingApp(helpArgs).Execute();
                }
                else if (command is Microsoft.TemplateEngine.Cli.Commands.ICustomHelp helpCommand)
                {
                    var blocks = helpCommand.CustomHelpLayout();
                    foreach (var block in blocks)
                    {
                        block(context);
                    }
                }
                else if (command.Name.Equals(FormatCommandParser.GetCommand().Name))
                {
                    new DotnetFormatForwardingApp(helpArgs).Execute();
                }
                else if (command.Name.Equals(FsiCommandParser.GetCommand().Name))
                {
                    new FsiForwardingApp(helpArgs).Execute();
                }
                else
                {
                    if (command.Name.Equals(ListProjectToProjectReferencesCommandParser.GetCommand().Name))
                    {
                        ListCommandParser.SlnOrProjectArgument.HelpName = CommonLocalizableStrings.ProjectArgumentName;
                        ListCommandParser.SlnOrProjectArgument.Description = CommonLocalizableStrings.ProjectArgumentDescription;
                    }
                    else if (command.Name.Equals(AddPackageParser.GetCommand().Name) || command.Name.Equals(AddCommandParser.GetCommand().Name))
                    {
                        // Don't show package completions in help
                        AddPackageParser.CmdPackageArgument.CompletionSources.Clear();
                    }

                    base.Write(context);
                }
            }



        }
    }
}
