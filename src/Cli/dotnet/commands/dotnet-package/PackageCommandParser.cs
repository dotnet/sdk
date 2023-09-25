// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CommandLine;
using System.Reflection.Metadata.Ecma335;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Cli
{
    internal static class PackageCommandParser
    {
        public static readonly string DocsLink = "https://aka.ns/dotnet-package";

        private static readonly CliCommand command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new DocumentedCommand("package", DocsLink);
            command.Subcommands.Add(PackageSearchCommandParser.GetCommand());
            return command;
        }
    }
}
