// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.DotNet.Cli.Commands.Project.Convert;

internal sealed class ProjectConvertCommand(ParseResult parseResult) : CommandBase(parseResult)
{
    private readonly string _file = parseResult.GetValue(ProjectConvertCommandParser.FileArgument) ?? string.Empty;
    private readonly string? _outputDirectory = parseResult.GetValue(SharedOptions.OutputOption)?.FullName;
    private readonly bool _force = parseResult.GetValue(ProjectConvertCommandParser.ForceOption);

    public override int Execute()
    {
        string file = Path.GetFullPath(_file);
        if (!VirtualProjectBuildingCommand.IsValidEntryPointPath(file))
        {
            throw new GracefulException(CliCommandStrings.InvalidFilePath, file);
        }

        string targetDirectory = _outputDirectory ?? Path.ChangeExtension(file, null);
        if (Directory.Exists(targetDirectory))
        {
            throw new GracefulException(CliCommandStrings.DirectoryAlreadyExists, targetDirectory);
        }

        // Find directives (this can fail, so do this before creating the target directory).
        var sourceFile = VirtualProjectBuildingCommand.LoadSourceFile(file);
        var directives = VirtualProjectBuildingCommand.FindDirectives(sourceFile, reportAllErrors: !_force, errors: null);

        Directory.CreateDirectory(targetDirectory);

        var targetFile = Path.Join(targetDirectory, Path.GetFileName(file));

        // If there were any directives, remove them from the file.
        if (directives.Length != 0)
        {
            VirtualProjectBuildingCommand.RemoveDirectivesFromFile(directives, sourceFile.Text, targetFile);
            File.Delete(file);
        }
        else
        {
            File.Move(file, targetFile);
        }

        string projectFile = Path.Join(targetDirectory, Path.GetFileNameWithoutExtension(file) + ".csproj");
        using var stream = File.Open(projectFile, FileMode.Create, FileAccess.Write);
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        VirtualProjectBuildingCommand.WriteProjectFile(writer, directives, isVirtualProject: false);

        return 0;
    }
}
