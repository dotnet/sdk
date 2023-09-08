// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Help
{
    public class HelpCommand
    {
        private readonly ParseResult _parseResult;

        public HelpCommand(ParseResult parseResult)
        {
            _parseResult = parseResult;
        }

        public static int Run(ParseResult result)
        {
            result.HandleDebugSwitch();

            result.ShowHelpOrErrorIfAppropriate();

            if (!string.IsNullOrEmpty(result.GetValue(HelpCommandParser.Argument)))
            {
                return new HelpCommand(result).Execute();
            }

            PrintHelp();
            return 0;
        }

        public static void PrintHelp()
        {
            PrintVersionHeader();
            Reporter.Output.WriteLine(HelpUsageText.UsageText);
        }

        public static void PrintVersionHeader()
        {
            var versionString = string.IsNullOrEmpty(Product.Version) ? string.Empty : $" ({Product.Version})";
            Reporter.Output.WriteLine(Product.LongName + versionString);
        }

        public static Process ConfigureProcess(string docUrl)
        {
            return Process.Start(new ProcessStartInfo()
            {
                FileName = docUrl
            });
        }


        /// <summary>
        /// Opens the HTML help for one of our <see cref="DocumentedCommand"/>s. If the command requested isn't a
        /// <see cref="DocumentedCommand"/>, or if it doesn't have a <see cref="DocumentedCommand.DocsLink" /> we'll just call <see cref="CliCommand.ShowHelp"/> on it.
        /// </summary>
        /// <returns></returns>
        public int Execute()
        {
            if (TryGetKnownCommand(_parseResult.GetValue(HelpCommandParser.Argument), out CliCommand command))
            {
                if (TryGetEnhancedDocCommand(command, out DocumentedCommand documentedCommand))
                {
                    if (!string.IsNullOrEmpty(documentedCommand.DocsLink))
                    {
                        var process = ConfigureProcess(documentedCommand.DocsLink);
                        process.Start();
                        process.WaitForExit();
                        if (process.ExitCode != 0)
                        {
                            // if the 'open browser' process fails, we should fallback to
                            // calling `--help` for that command and outputting that
                            return TryCallHelp(documentedCommand);
                        }
                        else
                        {
                            return process.ExitCode;
                        }
                    }
                    else
                    {
                        return TryCallHelp(documentedCommand);
                    }
                }
                else
                {
                    return TryCallHelp(command);
                }
            }
            else
            {
                Reporter.Error.WriteLine(
                    string.Format(
                        LocalizableStrings.CommandDoesNotExist,
                        _parseResult.GetValue(HelpCommandParser.Argument)).Red());
                Reporter.Output.WriteLine(HelpUsageText.UsageText);
                return 1;
            }
        }

        private bool TryGetKnownCommand(string commandName, out System.CommandLine.CliCommand command)
        {
            if (Cli.Parser.GetBuiltInCommand(commandName) is CliCommand builtInCommand)
            {
                command = builtInCommand;
                return true;
            }
            command = null;
            return false;
        }

        private bool TryGetEnhancedDocCommand(CliCommand command, out DocumentedCommand docsCommand)
        {
            if (command is DocumentedCommand docCmd)
            {
                docsCommand = docCmd;
                return true;
            }
            docsCommand = null;
            return false;
        }

        private int TryCallHelp(CliCommand command)
        {
            ParseResult p = Cli.Parser.Instance.Parse(["dotnet", command.Name]);
            return p.ShowHelp();
        }
    }
}
