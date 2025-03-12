// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Project.Add;

internal sealed class ProjectAddCommand : CommandBase
{
    private readonly string _file;

    public ProjectAddCommand(ParseResult parseResult) : base(parseResult)
    {
        _file = parseResult.GetValue(ProjectAddCommandParser.FileArgument) ?? string.Empty;
    }

    public override int Execute()
    {
        string file = Path.GetFullPath(_file);
        if (!VirtualProjectBuildingCommand.IsValidEntryPointPath(file))
        {
            throw new GracefulException(LocalizableStrings.InvalidFilePath, file);
        }

        if (!VirtualProjectBuildingCommand.HasTopLevelStatements(file))
        {
            throw new GracefulException(LocalizableStrings.NoTopLevelStatements, file);
        }

        string targetDirectory = Path.ChangeExtension(file, null);
        if (Directory.Exists(targetDirectory))
        {
            throw new GracefulException(LocalizableStrings.DirectoryAlreadyExists, targetDirectory);
        }

        Directory.CreateDirectory(targetDirectory);

        string projectFile = Path.Join(targetDirectory, Path.GetFileNameWithoutExtension(file) + ".csproj");
        string projectFileText = VirtualProjectBuildingCommand.GetNonVirtualProjectFileText();
        File.WriteAllText(path: projectFile, contents: projectFileText);

        File.Move(file, Path.Join(targetDirectory, Path.GetFileName(file)));

        return 0;
    }
}
