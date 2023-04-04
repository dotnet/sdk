// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#nullable enable

using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Help;

namespace Microsoft.TemplateEngine.EndToEndTestHarness
{
    internal static class ParserFactory
    {
        internal static CliConfiguration CreateParser(CliCommand command, bool disableHelp = false)
        {
            CliConfiguration config = new(command)
            {
                EnablePosixBundling = false,
                Directives = { new DiagramDirective(), new SuggestDirective() }
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
