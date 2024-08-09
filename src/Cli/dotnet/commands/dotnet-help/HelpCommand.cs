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

            if (result.GetValue(HelpCommandParser.Arguments) is { } args)
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
            return new Process() { StartInfo = new ProcessStartInfo()
            {
                FileName = docUrl,
                UseShellExecute = true
            }
            };
        }


        /// <summary>
        /// Opens the HTML help for one of our <see cref="DocumentedCommand"/>s. If the command requested isn't a
        /// <see cref="DocumentedCommand"/>, or if it doesn't have a <see cref="DocumentedCommand.DocsLink" /> we'll just call <see cref="CliCommand.ShowHelp"/> on it.
        /// </summary>
        /// <returns></returns>
        public int Execute()
        {
            var commandAndArgs = _parseResult.GetValue(HelpCommandParser.Arguments);
            var innerParseResult = Cli.Parser.Instance.Parse(["dotnet", .. commandAndArgs]);
            if (innerParseResult.CommandResult is { } commandResult)
            {

                if (commandResult.Command is DocumentedCommand documentedCommand
                    && !string.IsNullOrEmpty(documentedCommand.DocsLink))
                {
                    var process = ConfigureProcess(documentedCommand.DocsLink);
                    try
                    {
                        process.Start();
                        process.WaitForExit();
                        if (process.ExitCode == 0)
                        {
                            return process.ExitCode;
                        }
                    }
                    catch { }
                }

                // if the 'open browser' process fails, we should fallback to
                // calling `--help` for that command and outputting that
                return innerParseResult.ShowHelp();
            }
            else
            {
                Reporter.Error.WriteLine(
                    string.Format(
                        LocalizableStrings.CommandDoesNotExist,
                        String.Join(" ", _parseResult.GetValue(HelpCommandParser.Arguments))
                        ).Red()
                    );
                Reporter.Output.WriteLine(HelpUsageText.UsageText);
                return 1;
            }
        }
    }
}
