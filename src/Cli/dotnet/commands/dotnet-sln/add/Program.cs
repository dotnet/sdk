// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer.SlnV12;

namespace Microsoft.DotNet.Tools.Sln.Add
{
    internal class AddProjectToSolutionCommand : CommandBase
    {
        private readonly string _fileOrDirectory;
        private readonly bool _inRoot;
        private readonly IReadOnlyCollection<string> _projects;
        private readonly string? _solutionFolderPath;

        public AddProjectToSolutionCommand(ParseResult parseResult) : base(parseResult)
        {
            _fileOrDirectory = parseResult.GetValue(SlnCommandParser.SlnArgument);

            _projects = parseResult.GetValue(SlnAddParser.ProjectPathArgument)?.ToArray() ?? (IReadOnlyCollection<string>)Array.Empty<string>();

            _inRoot = parseResult.GetValue(SlnAddParser.InRootOption);
            _solutionFolderPath = parseResult.GetValue(SlnAddParser.SolutionFolderOption);

            SlnArgumentValidator.ParseAndValidateArguments(_fileOrDirectory, _projects, SlnArgumentValidator.CommandType.Add, _inRoot, _solutionFolderPath);
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
                if (ex is SolutionException || ex.InnerException is SolutionException)
                {
                    throw new GracefulException(CommonLocalizableStrings.InvalidSolutionFormatString, solutionFileFullPath, ex.Message);
                }
                throw new GracefulException(ex.Message, ex);
            }
        }

        private async Task AddProjectsToSolutionAsync(string solutionFileFullPath, string[] projectPaths, CancellationToken cancellationToken)
        {
            ISolutionSerializer serializer = SlnCommandParser.GetSolutionSerializer(solutionFileFullPath);
            SolutionModel solution = await serializer.OpenAsync(solutionFileFullPath, cancellationToken);
            // set UTF8 BOM encoding for .sln
            if (serializer is ISolutionSerializer<SlnV12SerializerSettings> v12Serializer)
            {
                solution.SerializerExtension = v12Serializer.CreateModelExtension(new()
                {
                    Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
                }); 
            }
            SolutionFolderModel solutionFolder = (!_inRoot && _solutionFolderPath != null)
                ? solution.AddFolder(GetSolutionFolderPathWithForwardSlashes())
                : null;
            foreach (var projectPath in projectPaths)
            {
                // Get full project path
                var relativePath = Path.GetRelativePath(Path.GetDirectoryName(solutionFileFullPath), projectPath);
                try
                {
                    AddProjectWithDefaultGuid(solution, relativePath, solutionFolder);
                    Reporter.Output.WriteLine(CommonLocalizableStrings.ProjectAddedToTheSolution, relativePath);
                }
                catch (Exception ex)
                {
                    // TODO: Update with error codes from vs-solutionpersistence
                    // Duplicate project
                    if (Regex.Match(ex.Message, @"Project name '.*' already exists in the solution folder\.").Success || Regex.Match(ex.Message, @"Duplicate item '.*' of type 'Project'\.").Success)
                    {
                        Reporter.Output.WriteLine(
                            string.Format(CommonLocalizableStrings.SolutionAlreadyContainsProject, solutionFileFullPath, relativePath));
                        continue;
                    }
                    // Invalid project
                    throw new GracefulException(ex.Message, ex);
                }
            }
            await serializer.SaveAsync(solutionFileFullPath, solution, cancellationToken);
        }

        private string GetSolutionFolderPathWithForwardSlashes()
        {
            // SolutionModel::AddFolder expects path to have leading, trailing and inner forward slashes
            return PathUtility.EnsureTrailingForwardSlash( PathUtility.GetPathWithForwardSlashes(Path.Join("/", _solutionFolderPath)) );
        }

        private void AddProjectWithDefaultGuid(SolutionModel solution, string relativePath, SolutionFolderModel solutionFolder)
        {
            try
            {
                solution.AddProject(relativePath, null, solutionFolder);
            }
            catch (ArgumentException ex)
            {
                if (ex.Message == "ProjectType '' not found. (Parameter 'projectTypeName')")
                {
                    solution.AddProject(relativePath, "130159A9-F047-44B3-88CF-0CF7F02ED50F", solutionFolder);
                }
            }
        }
    }
}
