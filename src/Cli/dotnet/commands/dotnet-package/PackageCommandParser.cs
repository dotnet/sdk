// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.NuGet;

namespace Microsoft.DotNet.Cli
{
    internal static class PackageCommandParser
    {
        public static readonly string DocsLink = "";

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new DocumentedCommand("package", DocsLink)
            {
                // some subcommands are not defined here and just forwarded to NuGet app
                TreatUnmatchedTokensAsErrors = false
            };

            command.SetAction(NuGetCommand.Run);

            return command;
        }
    }
}
