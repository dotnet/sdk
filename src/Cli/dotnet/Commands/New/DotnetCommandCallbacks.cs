// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Hidden.Add;
using Microsoft.DotNet.Cli.Commands.Package.Add;
using Microsoft.DotNet.Cli.Commands.Reference.Add;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Solution;
using Microsoft.DotNet.Cli.Commands.Solution.Add;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.New;

internal static class DotnetCommandCallbacks
{
    internal static bool AddPackageReference(string projectPath, string packageName, string? version)
    {
        PathUtility.EnsureAllPathsExist([projectPath], CliStrings.CommonFileNotFound, allowDirectories: false);
        IEnumerable<string> commandArgs = ["add", projectPath, "package", packageName];

        var packageAddCommandDef = new PackageAddCommandDefinition();
        if (!string.IsNullOrWhiteSpace(version))
        {
            commandArgs = commandArgs.Append(packageAddCommandDef.VersionOption.Name).Append(version);
        }

        var addCommand = new AddCommandDefinition();
        AddCommandParser.ConfigureCommand(addCommand);

        var addPackageReferenceCommand = new PackageAddCommand(addCommand.Parse([.. commandArgs]));
        return addPackageReferenceCommand.Execute() == 0;
    }

    internal static bool AddProjectReference(string projectPath, string projectToAdd)
    {
        PathUtility.EnsureAllPathsExist([projectPath], CliStrings.CommonFileNotFound, allowDirectories: false);
        PathUtility.EnsureAllPathsExist([projectToAdd], CliStrings.CommonFileNotFound, allowDirectories: false);
        IEnumerable<string> commandArgs = ["add", projectPath, "reference", projectToAdd];

        var addCommand = new AddCommandDefinition();
        AddCommandParser.ConfigureCommand(addCommand);

        var addProjectReferenceCommand = new ReferenceAddCommand(addCommand.Parse([.. commandArgs]));
        return addProjectReferenceCommand.Execute() == 0;
    }

    internal static bool RestoreProject(string pathToRestore)
    {
        PathUtility.EnsureAllPathsExist([pathToRestore], CliStrings.CommonFileNotFound, allowDirectories: true);
        // for the implicit restore we do not want the terminal logger to emit any output unless there are errors
        return RestoreCommand.Run([pathToRestore, "-tlp:verbosity=quiet", "--no-logo"]) == 0;
    }

    internal static bool AddProjectsToSolution(string solutionPath, IReadOnlyList<string> projectsToAdd, string? solutionFolder, bool? inRoot)
    {
        PathUtility.EnsureAllPathsExist([solutionPath], CliStrings.CommonFileNotFound, allowDirectories: false);
        PathUtility.EnsureAllPathsExist(projectsToAdd, CliStrings.CommonFileNotFound, allowDirectories: false);
        IEnumerable<string> commandArgs = new[] { "solution", solutionPath, "add" }.Concat(projectsToAdd);
        if (!string.IsNullOrWhiteSpace(solutionFolder))
        {
            commandArgs = commandArgs.Append(SolutionAddCommandDefinition.SolutionFolderOptionName).Append(solutionFolder);
        }

        if (inRoot is true)
        {
            commandArgs = commandArgs.Append(SolutionAddCommandDefinition.InRootOptionName);
        }

        var solutionCommand = new SolutionCommandDefinition();
        SolutionCommandParser.ConfigureCommand(solutionCommand);

        var addProjectToSolutionCommand = new SolutionAddCommand(solutionCommand.Parse([.. commandArgs]));
        return addProjectToSolutionCommand.Execute() == 0;
    }
}
