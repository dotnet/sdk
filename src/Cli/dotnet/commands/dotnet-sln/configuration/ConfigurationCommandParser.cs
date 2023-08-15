// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.CommandFactory;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Resources;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class SlnConfigurationParser
    {
        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("configuration", LocalizableStrings.ConfigurationCommand);

            command.Subcommands.Add(ConfigurationAddParser.GetCommand());

            /*
            command.Subcommands.Add(ConfigureRenameParser.GetCommand());
            command.Subcommands.Add(ConfigureRemoveParser.GetCommand());
            command.Subcommands.Add(ConfigureUpdateParser.GetCommand());
            */

            command.SetAction((parseResult) => parseResult.HandleMissingCommand());

            return command;
        }
    }
}

