// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.Add;
using Microsoft.DotNet.Cli.Commands.Package.List;
using Microsoft.DotNet.Cli.Commands.Package.Remove;
using Microsoft.DotNet.Cli.Commands.Package.Search;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Package;

internal class PackageCommandParser
{
    private const string DocsLink = "https://aka.ms/dotnet-package";

    public static readonly Option<string> ProjectOption = new Option<string>("--project")
    {
        Recursive = true,
        DefaultValueFactory = _ => Environment.CurrentDirectory,
        Description = CliStrings.ProjectArgumentDescription
    };

    public static Command GetCommand()
    {
        Command command = new DocumentedCommand("package", DocsLink);
        command.SetAction((parseResult) => parseResult.HandleMissingCommand());
        command.Subcommands.Add(PackageSearchCommandParser.GetCommand());
        command.Subcommands.Add(PackageAddCommandParser.GetCommand());
        command.Subcommands.Add(PackageListCommandParser.GetCommand());
        command.Subcommands.Add(PackageRemoveCommandParser.GetCommand());

        return command;
    }
} 
