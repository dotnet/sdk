// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Help;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal static class ParserFactory
    {
        internal static readonly ParserConfiguration ParserConfiguration = new()
        {
            EnablePosixBundling = false,
        };

        internal static Command CreateParser(Command command, bool disableHelp = false)
        {
            // {
            //     EnablePosixBundling = false
            // };

            if (!disableHelp)
            {
                command.Options.Add(new HelpOption());
            }
            return command;
        }
    }
}
