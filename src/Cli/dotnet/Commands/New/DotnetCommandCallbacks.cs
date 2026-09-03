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
        if (!string.IsNullOrWhiteSpace(version))
        {
            commandArgs = commandArgs.Append(PackageAddCommandParser.VersionOption.Name).Append(version);
        }
        var addPackageReferenceCommand = new PackageAddCommand(AddCommandParser.GetCommand().Parse([.. commandArgs]));
        return addPackageReferenceCommand.Execute() == 0;
    }

    internal static bool AddProjectReference(string projectPath, string projectToAdd)
    {
        PathUtility.EnsureAllPathsExist([projectPath], CliStrings.CommonFileNotFound, allowDirectories: false);
        PathUtility.EnsureAllPathsExist([projectToAdd], CliStrings.CommonFileNotFound, allowDirectories: false);
        IEnumerable<string> commandArgs = ["add", projectPath, "reference", projectToAdd];
        var addProjectReferenceCommand = new ReferenceAddCommand(AddCommandParser.GetCommand().Parse([.. commandArgs]));
        return addProjectReferenceCommand.Execute() == 0;
    }

    internal static bool RestoreProject(string pathToRestore)
    {
        PathUtility.EnsureAllPathsExist([pathToRestore], CliStrings.CommonFileNotFound, allowDirectories: true);
        // for the implicit restore we do not want the terminal logger to emit any output unless there are errors
        return RestoreCommand.Run([pathToRestore, "-tlp:verbosity=quiet"]) == 0;
    }

    internal static bool AddProjectsToSolution(string solutionPath, IReadOnlyList<string> projectsToAdd, string? solutionFolder, bool? inRoot)
    {
        PathUtility.EnsureAllPathsExist([solutionPath], CliStrings.CommonFileNotFound, allowDirectories: false);
        PathUtility.EnsureAllPathsExist(projectsToAdd, CliStrings.CommonFileNotFound, allowDirectories: false);
        IEnumerable<string> commandArgs = new[] { "solution", solutionPath, "add" }.Concat(projectsToAdd);
        if (!string.IsNullOrWhiteSpace(solutionFolder))
        {
            commandArgs = commandArgs.Append(SolutionAddCommandParser.SolutionFolderOption.Name).Append(solutionFolder);
        }

        if (inRoot is true)
        {
            commandArgs = commandArgs.Append(SolutionAddCommandParser.InRootOption.Name);
        }
        var addProjectToSolutionCommand = new SolutionAddCommand(SolutionCommandParser.GetCommand().Parse([.. commandArgs]));
        return addProjectToSolutionCommand.Execute() == 0;
    }
}
