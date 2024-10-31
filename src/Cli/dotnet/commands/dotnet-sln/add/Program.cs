// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;

namespace Microsoft.DotNet.Tools.Sln.Add
{
    internal class AddProjectToSolutionCommand : CommandBase
    {
        private readonly string _fileOrDirectory;
        private readonly bool _inRoot;
        private readonly IList<string> _relativeRootSolutionFolders;
        private readonly IReadOnlyCollection<string> _projects;

        public AddProjectToSolutionCommand(ParseResult parseResult) : base(parseResult)
        {
            _fileOrDirectory = parseResult.GetValue(SlnCommandParser.SlnArgument);

            _projects = parseResult.GetValue(SlnAddParser.ProjectPathArgument)?.ToArray() ?? (IReadOnlyCollection<string>)Array.Empty<string>();

            _inRoot = parseResult.GetValue(SlnAddParser.InRootOption);
            string relativeRoot = parseResult.GetValue(SlnAddParser.SolutionFolderOption);

            SlnArgumentValidator.ParseAndValidateArguments(_fileOrDirectory, _projects, SlnArgumentValidator.CommandType.Add, _inRoot, relativeRoot);

            bool hasRelativeRoot = !string.IsNullOrEmpty(relativeRoot);

            if (hasRelativeRoot)
            {
                relativeRoot = PathUtility.GetPathWithDirectorySeparator(relativeRoot);
                _relativeRootSolutionFolders = relativeRoot.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                _relativeRootSolutionFolders = null;
            }
        }

        public override int Execute()
        {
            var solutionFileFullPath = SlnCommandParser.GetSlnFileFullPath(_fileOrDirectory);

            if (_projects.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneProjectToAdd);
            }
            PathUtility.EnsureAllPathsExist(_projects, CommonLocalizableStrings.CouldNotFindProjectOrDirectory, true);
            var fullProjectPaths = _projects.Select(project =>
            {
                var fullPath = Path.GetFullPath(project);
                return Directory.Exists(fullPath) ? MsbuildProject.GetProjectFileFromDirectory(fullPath).FullName : fullPath;
            }).ToArray();

            try
            {
                AddProjectsToSolutionAsync(solutionFileFullPath, fullProjectPaths, CancellationToken.None).Wait();
                return 0;
            }
            catch (Exception ex)
            {
                throw new GracefulException("TODO: Handle exception", ex);
            }
        }

        private async Task AddProjectsToSolutionAsync(string solutionFileFullPath, string[] projectPaths, CancellationToken cancellationToken)
        {
            ISolutionSerializer serializer = SlnCommandParser.GetSolutionSerializer(solutionFileFullPath);
            SolutionModel solution = await serializer.OpenAsync(solutionFileFullPath, cancellationToken);
            foreach (var projectPath in projectPaths)
            {
                // TODO: Handle solution folder
                // var solutionFolder = string.Join(Path.DirectorySeparatorChar, GetSolutionFoldersFromProjectPath(Path.GetRelativePath(solutionFileFullPath, projectPath)));
                solution.AddProject(Path.GetRelativePath(Path.GetDirectoryName(solutionFileFullPath), projectPath), null, null);
            }
            await serializer.SaveAsync(solutionFileFullPath, solution, cancellationToken);
        }

        private static IList<string> GetSolutionFoldersFromProjectPath(string projectFilePath)
        {
            var solutionFolders = new List<string>();

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

        private static bool IsPathInTreeRootedAtSolutionDirectory(string path)
        {
            return !path.StartsWith("..");
        }
    }
}
