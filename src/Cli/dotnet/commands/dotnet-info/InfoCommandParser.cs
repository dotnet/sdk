// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tools.Info
{
    internal static class InfoCommandParser
    {
        public static readonly string DocsLink = "TODO";

        public enum FormatOptions
        {
            text,
            json
        }

        public static readonly CliOption<FormatOptions> FormatOption = new("--format", "-f")
        {
            Description = ""
        };

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            DocumentedCommand command = new("info", DocsLink);
            command.Options.Add(FormatOption);
            command.SetAction(InfoCommand.Run);

            return command;
        }
    }
}

