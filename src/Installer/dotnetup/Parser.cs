// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Reflection;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.DefaultInstall;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Dotnet;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.ElevatedAdminPath;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Info;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.List;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.PrintEnvScript;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Uninstall;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Update;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Theme;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Walkthrough;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class Parser
{
    public static ParserConfiguration ParserConfiguration { get; } = new()
    {
        EnablePosixBundling = false,
        //ResponseFileTokenReplacer = TokenPerLine
    };

    public static InvocationConfiguration InvocationConfiguration { get; } = new()
    {
        //EnableDefaultExceptionHandler = false,
    };

    public static ParseResult Parse(string[] args) => RootCommand.Parse(args, ParserConfiguration);
    public static int Invoke(ParseResult parseResult) => parseResult.Invoke(InvocationConfiguration);
    public static int Invoke(string[] args) => Invoke(Parse(args));

    private static RootCommand RootCommand { get; } = ConfigureCommandLine(new()
    {
        Description = Strings.RootCommandDescription,
        Directives = { new DiagramDirective(), new SuggestDirective(), new EnvironmentVariablesDirective() }
    });

    /// <summary>
    /// Gets the version string from the dotnetup assembly.
    /// </summary>
    public static string Version { get; } = typeof(Parser).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    private static RootCommand ConfigureCommandLine(RootCommand rootCommand)
    {
        rootCommand.Subcommands.Add(InfoCommandParser.GetCommand());
        rootCommand.Subcommands.Add(SdkCommandParser.GetCommand());
        rootCommand.Subcommands.Add(RuntimeCommandParser.GetCommand());
        rootCommand.Subcommands.Add(SdkInstallCommandParser.GetRootInstallCommand());
        rootCommand.Subcommands.Add(SdkUpdateCommandParser.GetRootUpdateCommand());
        rootCommand.Subcommands.Add(SdkUninstallCommandParser.GetRootUninstallCommand());
        rootCommand.Subcommands.Add(ElevatedAdminPathCommandParser.GetCommand());
        rootCommand.Subcommands.Add(DefaultInstallCommandParser.GetCommand());
        rootCommand.Subcommands.Add(PrintEnvScriptCommandParser.GetCommand());
        rootCommand.Subcommands.Add(ListCommandParser.GetCommand());
        rootCommand.Subcommands.Add(DotnetCommandParser.GetCommand());
        rootCommand.Subcommands.Add(DotnetCommandParser.GetAliasCommand());
        rootCommand.Subcommands.Add(WalkthroughCommandParser.GetCommand());
        rootCommand.Subcommands.Add(ThemeCommandParser.GetCommand());

        ConfigureHelp(rootCommand);

        rootCommand.SetAction(parseResult =>
        {
            return new WalkthroughCommand(parseResult).Execute();
        });

        return rootCommand;
    }

    private static void ConfigureHelp(RootCommand rootCommand)
    {
        // Hide --info (shown in options section instead) and do (alias for dotnet)
        foreach (Command cmd in rootCommand.Subcommands)
        {
            if (cmd.Name is "--info" or "do")
            {
                cmd.Hidden = true;
            }
        }

        // Replace the help option's action with our grouped help writer
        foreach (Option option in rootCommand.Options)
        {
            if (option is HelpOption helpOption)
            {
                helpOption.Action = new GroupedHelpAction(rootCommand);
                break;
            }
        }
    }

    private sealed class GroupedHelpAction(RootCommand rootCommand) : SynchronousCommandLineAction
    {
        private static readonly (string Heading, string[] CommandNames)[] s_commandGroups =
        [
            (Strings.HelpInstallCommandsTitle, ["sdk", "runtime", "install", "update", "uninstall"]),
            (Strings.HelpQueryCommandsTitle, ["list"]),
            (Strings.HelpConfigCommandsTitle, ["print-env-script", "defaultinstall", "theme"]),
            (Strings.HelpUtilityCommandsTitle, ["dotnet", "walkthrough"]),
        ];

        public override int Invoke(ParseResult parseResult)
        {
            TextWriter output = parseResult.InvocationConfiguration.Output;
            Command command = parseResult.CommandResult.Command;

            // Description
            if (!string.IsNullOrWhiteSpace(command.Description))
            {
                output.WriteLine(Strings.HelpDescriptionLabel);
                output.WriteLine($"  {command.Description}");
                output.WriteLine();
            }

            // Usage
            output.WriteLine(Strings.HelpUsageLabel);
            output.Write("  ");
            output.Write(command.Name);
            if (command.Subcommands.Any(c => !c.Hidden))
            {
                output.Write(" [command]");
            }
            if (command.Options.Any(o => !o.Hidden))
            {
                output.Write(" [options]");
            }
            output.WriteLine();
            output.WriteLine();

            // Options (including --info)
            WriteOptionsSection(output, command, rootCommand);

            // Command groups
            foreach ((string heading, string[] names) in s_commandGroups)
            {
                WriteCommandGroup(output, heading, rootCommand, names);
            }

            return 0;
        }

        private static void WriteOptionsSection(TextWriter output, Command command, RootCommand rootCommand)
        {
            List<(string Label, string Description)> rows = [];

            // --info is a hidden subcommand shown as an option
            Command? infoCommand = rootCommand.Subcommands.FirstOrDefault(c => c.Name == "--info");
            if (infoCommand is not null)
            {
                rows.Add((infoCommand.Name, infoCommand.Description ?? ""));
            }

            foreach (Option option in command.Options)
            {
                if (!option.Hidden)
                {
                    rows.Add((FormatOptionLabel(option), option.Description ?? ""));
                }
            }

            if (rows.Count > 0)
            {
                output.WriteLine(Strings.HelpOptionsTitle);
                WriteTwoColumnRows(output, rows);
                output.WriteLine();
            }
        }

        private static void WriteCommandGroup(TextWriter output, string heading, RootCommand rootCommand, string[] commandNames)
        {
            List<(string Label, string Description)> rows = [];

            foreach (string name in commandNames)
            {
                Command? cmd = rootCommand.Subcommands.FirstOrDefault(c => c.Name == name);
                if (cmd is not null && !cmd.Hidden)
                {
                    rows.Add((cmd.Name, cmd.Description ?? ""));
                }
            }

            if (rows.Count > 0)
            {
                output.WriteLine(heading);
                WriteTwoColumnRows(output, rows);
                output.WriteLine();
            }
        }

        private static string FormatOptionLabel(Option option)
        {
            string label = option.Name;
            if (option.Aliases.Count > 0)
            {
                IEnumerable<string> aliases = option.Aliases.Where(a => a != option.Name);
                string joined = string.Join(", ", aliases);
                if (!string.IsNullOrEmpty(joined))
                {
                    label = $"{joined}, {label}";
                }
            }
            return label;
        }

        private static void WriteTwoColumnRows(TextWriter output, List<(string Label, string Description)> rows)
        {
            int maxLabelWidth = rows.Max(r => r.Label.Length);
            int padding = maxLabelWidth + 4; // 2 indent + 2 gap

            foreach ((string label, string description) in rows)
            {
                output.Write("  ");
                output.Write(label);
                if (!string.IsNullOrEmpty(description))
                {
                    output.Write(new string(' ', padding - label.Length - 2));
                    output.Write(description);
                }
                output.WriteLine();
            }
        }
    }
}
