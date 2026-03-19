// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.NuGet;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Package.Remove;

internal sealed class PackageRemoveCommand(ParseResult parseResult) : CommandBase(parseResult)
{
    private readonly PackageRemoveCommandDefinitionBase _definition = (PackageRemoveCommandDefinitionBase)parseResult.CommandResult.Command;

    public override int Execute()
    {
        var arguments = _parseResult.GetValue(_definition.CmdPackageArgument) ?? [];

        if (arguments is not [{ } packageToRemove])
        {
            throw new GracefulException(CliCommandStrings.PackageRemoveSpecifyExactlyOnePackageReference);
        }

        var (fileOrDirectory, allowedAppKinds) = PackageCommandParser.ProcessPathOptions(_definition.FileOption, _definition.ProjectOption, projectOrFileArgument: null, _parseResult);

        bool isFileBasedApp = allowedAppKinds.HasFlag(AppKinds.FileBased) && VirtualProjectBuilder.IsValidEntryPointPath(fileOrDirectory);

        Debug.Assert(isFileBasedApp || allowedAppKinds.HasFlag(AppKinds.ProjectBased));

        string projectFilePath;
        if (!File.Exists(fileOrDirectory))
        {
            Debug.Assert(!isFileBasedApp);
            projectFilePath = MsbuildProject.GetProjectFileFromDirectory(fileOrDirectory);
        }
        else
        {
            projectFilePath = fileOrDirectory;
        }

        projectFilePath = Path.GetFullPath(projectFilePath);

        var result = NuGetCommand.Run(TransformArgs(packageToRemove, projectFilePath), isFileBasedApp);

        return result;
    }

    private string[] TransformArgs(string packageId, string projectFilePath)
    {
        var args = new List<string>()
        {
            "package",
            "remove",
            "--package",
            packageId,
            "--project",
            projectFilePath
        };

        args.AddRange(_parseResult
            .OptionValuesToBeForwarded(new PackageRemoveCommandDefinition())
            .SelectMany(a => a.Split(' ')));

        return [.. args];
    }
}
