// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Fsi;

namespace Microsoft.DotNet.Cli
{
    internal static class FsiCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-fsi";

        public static readonly Argument<string[]> Arguments = new("arguments");

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            DocumentedCommand command = new("fsi", DocsLink) { Arguments };

            command.SetAction((ParseResult parseResult) => FsiCommand.Run(parseResult.GetValue(Arguments)));

            return command;
        }
    }
}
