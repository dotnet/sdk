// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Help;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal static class ParserFactory
    {
        internal static CliConfiguration CreateParser(CliCommand command, bool disableHelp = false)
        {
            CliConfiguration config = new(command)
            //TODO: decide if it's needed to implement it; and implement if needed
            //.UseParseDirective()
            //.UseSuggestDirective()
            {
                EnableParseErrorReporting = true,
                EnablePosixBundling = false
            };

            for (int i = 0; i < command.Options.Count; i++)
            {
                if (command.Options[i] is HelpOption)
                {
                    if (disableHelp)
                    {
                        command.Options.RemoveAt(i);
                    }

                    return config;
                }
            }

            if (!disableHelp)
            {
                command.Options.Add(new HelpOption());
            }

            return config;
        }
    }
}
