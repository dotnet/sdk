// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.Add;
using Microsoft.DotNet.Cli.Commands.Package.List;
using Microsoft.DotNet.Cli.Commands.Package.Remove;
using Microsoft.DotNet.Cli.Commands.Package.Search;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli.Commands.Package;

internal class PackageCommandDefinition
{
    public const string Name = "package";
    private const string DocsLink = "https://aka.ms/dotnet-package";

    public static readonly Option<string?> ProjectOption = new("--project")
    {
        Recursive = true,
        Description = CliStrings.ProjectArgumentDescription
    };

    public static readonly Option<string?> FileOption = new("--file")
    {
        Recursive = true,
        Description = CliStrings.FileArgumentDescription
    };

    // Used by the legacy 'add/remove package' commands.
    public static readonly Argument<string> ProjectOrFileArgument = new Argument<string>(CliStrings.ProjectOrFileArgumentName)
    {
        Description = CliStrings.ProjectOrFileArgumentDescription
    }.DefaultToCurrentDirectory();

    public static Command Create()
    {
        Command command = new Command("package")
        {
            DocsLink = DocsLink
        };

        command.Subcommands.Add(PackageSearchCommandDefinition.Create());
        command.Subcommands.Add(PackageAddCommandDefinition.Create());
        command.Subcommands.Add(PackageListCommandDefinition.Create());
        command.Subcommands.Add(PackageRemoveCommandDefinition.Create());

        return command;
    }

    public static (string Path, AppKinds AllowedAppKinds) ProcessPathOptions(ParseResult parseResult)
    {
        bool hasFileOption = parseResult.HasOption(FileOption);
        bool hasProjectOption = parseResult.HasOption(ProjectOption);

        return (hasFileOption, hasProjectOption) switch
        {
            (false, false) => parseResult.GetValue(ProjectOrFileArgument) is { } projectOrFile
                ? (projectOrFile, AppKinds.Any)
                : (Environment.CurrentDirectory, AppKinds.ProjectBased),
            (true, false) => (parseResult.GetValue(FileOption)!, AppKinds.FileBased),
            (false, true) => (parseResult.GetValue(ProjectOption)!, AppKinds.ProjectBased),
            (true, true) => throw new GracefulException(CliCommandStrings.CannotCombineOptions, FileOption.Name, ProjectOption.Name),
        };
    }
}
