// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Help;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal static class ParserFactory
    {
        internal static CommandLineConfiguration CreateParser(Command command, bool disableHelp = false)
        {
            CommandLineConfiguration config = new(command)
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
