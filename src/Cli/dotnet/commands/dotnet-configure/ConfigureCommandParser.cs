// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.CommandFactory;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Resources;
using LocalizableStrings = Microsoft.DotNet.Tools.Configure.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ConfigureCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-sln"; /* TBD - how to get the doc added for doctnet-configure */

        public static readonly Argument<string> ConfigureArgument = new Argument<string>(LocalizableStrings.ConfigureArgumentSlnName)
        {
            Description = LocalizableStrings.ConfigureArgumentSlnDescription,
            Arity = ArgumentArity.ExactlyOne
        }.DefaultToCurrentDirectory();

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("configure", DocsLink, LocalizableStrings.ConfigureCommand);

            command.AddArgument(ConfigureArgument);
            command.AddCommand(ConfigureAddParser.GetCommand());
            /*
            command.AddCommand(ConfigureRenameParser.GetCommand());
            command.AddCommand(ConfigureRemoveParser.GetCommand());
            command.AddCommand(ConfigureUpdateParser.GetCommand());
            */
            command.SetHandler((parseResult) => parseResult.HandleMissingCommand());

            return command;
        }
    }
}

