// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Help;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Dotnet_new3
{
    internal static class ParserFactory
    {
        internal static CliConfiguration CreateParser(CliCommand command, bool disableHelp = false)
        {
            CliConfiguration config = new(command)
            //.UseExceptionHandler(ExceptionHandler)
            //.UseLocalizationResources(new CommandLineValidationMessages())
            //TODO: decide if it's needed to implement it; and implement if needed
            //.UseParseDirective()
            //.UseSuggestDirective()
            {
                EnablePosixBundling = false
            };

            for (int i = 0; i < command.Options.Count; i++)
            {
                if (command.Options[i] is HelpOption)
                {
                    if (disableHelp)
                    {
                        command.Options.RemoveAt(i);
                        return config;
                    }

                    command.Options[i] = CreateCustomHelp();
                    return config;
                }
            }

            if (!disableHelp)
            {
                command.Options.Add(CreateCustomHelp());
            }

            return config;

            static HelpOption CreateCustomHelp()
            {
                HelpOption helpOption = new();
                HelpAction helpAction = (HelpAction)helpOption.Action!;
                helpAction.Builder.CustomizeLayout(CustomHelpLayout);
                return helpOption;
            }
        }

        private static IEnumerable<Func<HelpContext, bool>> CustomHelpLayout(HelpContext context)
        {
            if (context.ParseResult.CommandResult.Command is ICustomHelp custom)
            {
                foreach (var layout in custom.CustomHelpLayout())
                {
                    yield return hc =>
                    {
                        layout(hc);
                        return true;
                    };
                }
            }
            else
            {
                foreach (var layout in HelpBuilder.Default.GetLayout())
                {
                    yield return layout;
                }
            }
        }
    }
}
