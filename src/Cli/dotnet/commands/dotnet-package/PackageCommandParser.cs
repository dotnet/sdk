// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Package.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal class PackageCommandParser
    {
        private const string DocsLink = "https://aka.ms/dotnet-package";

        public static CliCommand GetCommand()
        {
            CliCommand command = new DocumentedCommand("package", DocsLink, LocalizableStrings.AppFullName);
            command.SetAction((parseResult) => parseResult.HandleMissingCommand());
            command.Subcommands.Add(PackageSearchCommandParser.GetCommand());

            return command;
        }
    }
}
