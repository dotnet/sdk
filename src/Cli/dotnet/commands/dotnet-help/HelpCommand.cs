// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

using NuGetDocumentedCommand = NuGet.CommandLine.XPlat.Commands.DocumentedCommand;

namespace Microsoft.DotNet.Tools.Help
{
    public class HelpCommand(string[] helpArgs)
    {
        public static int Run(ParseResult result)
        {
            result.HandleDebugSwitch();

            result.ShowHelpOrErrorIfAppropriate();

            if (result.GetValue(HelpCommandParser.Argument) is string[] args && args is not [])
            {
                return new HelpCommand(args).Execute();
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
            ProcessStartInfo psInfo;
            if (OperatingSystem.IsWindows())
            {
                psInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                    Arguments = $"/c start {docUrl}"
                };
            }
            else if (OperatingSystem.IsMacOS())
            {
                psInfo = new ProcessStartInfo
                {
                    FileName = @"/usr/bin/open",
                    Arguments = docUrl
                };
            }
            else
            {
                var fileName = File.Exists(@"/usr/bin/xdg-open") ? @"/usr/bin/xdg-open" :
                               File.Exists(@"/usr/sbin/xdg-open") ? @"/usr/sbin/xdg-open" :
                               File.Exists(@"/sbin/xdg-open") ? @"/sbin/xdg-open" :
                               "xdg-open";
                psInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = docUrl
                };
            }

            return new Process
            {
                StartInfo = psInfo
            };
        }

        public int Execute()
        {
            if (TryGetDocsLink(
                helpArgs,
                out var docsLink) &&
                !string.IsNullOrEmpty(docsLink))
            {
                var process = ConfigureProcess(docsLink);
                process.Start();
                process.WaitForExit();
                return 0;
            }
            else
            {
                Reporter.Error.WriteLine(
                    string.Format(
                        LocalizableStrings.CommandDoesNotExist,
                        helpArgs).Red());
                Reporter.Output.WriteLine(HelpUsageText.UsageText);
                return 1;
            }
        }

        private bool TryGetDocsLink(string[] command, out string docsLink)
        {
            var parsedCommand = Parser.Instance.Parse(["dotnet", .. command]);
            if (parsedCommand?.CommandResult?.Command is DocumentedCommand dc)
            {
                docsLink = dc.DocsLink;
                return true;
            }
            else if (parsedCommand?.CommandResult?.Command is NuGetDocumentedCommand ndc)
            {
                docsLink = ndc.HelpUrl;
                return true;
            }
            docsLink = null;
            return false;
        }
    }
}
