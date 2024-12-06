// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.Sln.Add;

internal class AddFolderToSolutionCommand : CommandBase
{
    private readonly string _fileOrDirectory;
    private readonly bool _inRoot;
    private readonly IList<string> _relativeRootSolutionFolders;
    private readonly IReadOnlyList<string> _arguments;

    public AddFolderToSolutionCommand(ParseResult parseResult) : base(parseResult)
    {
        var projectPaths = parseResult.GetValue(SlnAddParser.ProjectPathArgument)?.ToArray() ?? [];
        if (projectPaths.Length != 0)
        {
            throw new GracefulException(LocalizableStrings.ProjectPathArgumentShouldNotBeProvidedForDotnetSlnAddFolder);
        }

        _fileOrDirectory = parseResult.GetValue(SlnCommandParser.SlnArgument);
        _arguments = parseResult.GetValue(SlnAddFolderParser.FolderPathArgument).ToArray();
        _inRoot = parseResult.GetValue(SlnAddFolderParser.InRootOption);
        string relativeRoot = parseResult.GetValue(SlnAddFolderParser.SolutionFolderOption);

        SlnArgumentValidator.ParseAndValidateArguments(_arguments, SlnArgumentValidator.CommandType.Add, _inRoot, relativeRoot, subcommand: "folder");

        if (string.IsNullOrEmpty(relativeRoot))
        {
            _relativeRootSolutionFolders = null;
        }
        else
        {
            relativeRoot = PathUtility.GetPathWithDirectorySeparator(relativeRoot);
            _relativeRootSolutionFolders = relativeRoot.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        }
    }

    public override int Execute()
    {
        SlnFile slnFile = SlnFileFactory.CreateFromFileOrDirectory(_fileOrDirectory);

        if (_arguments.Count == 0)
        {
            throw new GracefulException(Tools.Sln.LocalizableStrings.SpecifyAtLeastOneFolderToAdd);
        }

        // Visual Studio doesn't allow certain solution folder names
        if (!_arguments.All(SlnArgumentValidator.IsValidSolutionFolderName))
        {
            throw new GracefulException(Tools.Sln.LocalizableStrings.SolutionFolderNameCannot);
        }

        var fullSolutionFolderPaths = _arguments.Select(Path.GetFullPath).ToList();

        var preAddProjectCount = slnFile.Projects.Count;

        foreach (var fullSolutionFolderPath in fullSolutionFolderPaths)
        {
            var solutionFolders = DetermineSolutionFolder(slnFile, fullSolutionFolderPath);
            slnFile.AddSolutionFolder(fullSolutionFolderPath, solutionFolders);
        }

        if (slnFile.Projects.Count > preAddProjectCount)
        {
            slnFile.Write();
        }

        return 0;
    }

    private static List<string> GetSolutionFoldersFromProjectPath(string projectFilePath)
    {
        List<string> solutionFolders = [];

        if (!IsPathInTreeRootedAtSolutionDirectory(projectFilePath))
            return solutionFolders;

        var currentDirString = $".{Path.DirectorySeparatorChar}";
        if (projectFilePath.StartsWith(currentDirString))
        {
            projectFilePath = projectFilePath.Substring(currentDirString.Length);
        }

        var projectDirectoryPath = Path.GetDirectoryName(projectFilePath);
        if (string.IsNullOrEmpty(projectDirectoryPath))
            return solutionFolders;

        var solutionFoldersPath = Path.GetDirectoryName(projectDirectoryPath);
        if (string.IsNullOrEmpty(solutionFoldersPath))
            return solutionFolders;

        solutionFolders.AddRange(solutionFoldersPath.Split(Path.DirectorySeparatorChar));

        return solutionFolders;
    }

    private IList<string> DetermineSolutionFolder(SlnFile slnFile, string fullProjectPath)
    {
        if (_inRoot)
        {
            // The user requested all projects go to the root folder
            return null;
        }

        if (_relativeRootSolutionFolders != null)
        {
            // The user has specified an explicit root
            return _relativeRootSolutionFolders;
        }

        // We determine the root for each individual project
        var relativeProjectPath = Path.GetRelativePath(
            PathUtility.EnsureTrailingSlash(slnFile.BaseDirectory),
            fullProjectPath);

        return GetSolutionFoldersFromProjectPath(relativeProjectPath);
    }

    private static bool IsPathInTreeRootedAtSolutionDirectory(string path) => !path.StartsWith("..");
}
