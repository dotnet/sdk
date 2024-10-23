﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Sln.Add;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    public static class SlnMigrateCommandParser
    {
        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("migrate", LocalizableStrings.MigrateAppFullName);

            command.Arguments.Add(SlnCommandParser.SlnArgument);

            command.SetAction((parseResult) => new SlnMigrateCommand(parseResult).Execute());

            return command;
        }
    }
}
