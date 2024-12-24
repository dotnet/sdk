// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer.SlnV12;

namespace Microsoft.DotNet.Tools.Sln.Remove
{
    internal class RemoveProjectFromSolutionCommand : CommandBase
    {
        private readonly string _fileOrDirectory;
        private readonly IReadOnlyCollection<string> _projects;

        private int CountNonFolderDescendants(
            SolutionModel solution,
            SolutionFolderModel item,
            Dictionary<SolutionFolderModel, SolutionItemModel[]> solutionItemsGroupedByParent,
            Dictionary<SolutionFolderModel, int> cached)
        {
            if (cached.ContainsKey(item))
            {
                return cached[item];
            }
            int count = item.Files?.Count ?? 0;
            var children = solutionItemsGroupedByParent.TryGetValue(item, out var items) ? items : Array.Empty<SolutionItemModel>();
            foreach (var child in children)
            {
                count += child is SolutionFolderModel folderModel
                    ? CountNonFolderDescendants(solution, folderModel, solutionItemsGroupedByParent, cached)
                    : 1;
            }
            cached.Add(item, count);
            return count;
        }

        public RemoveProjectFromSolutionCommand(ParseResult parseResult) : base(parseResult)
        {
            _fileOrDirectory = parseResult.GetValue(SlnCommandParser.SlnArgument);

            _projects = (parseResult.GetValue(SlnRemoveParser.ProjectPathArgument) ?? Array.Empty<string>()).ToList().AsReadOnly();

            SlnArgumentValidator.ParseAndValidateArguments(_fileOrDirectory, _projects, SlnArgumentValidator.CommandType.Remove);
        }

        public override int Execute()
        {
            string solutionFileFullPath = SlnCommandParser.GetSlnFileFullPath(_fileOrDirectory);
            if (_projects.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneProjectToRemove);
            }

            try
            {
                var relativeProjectPaths = _projects.Select(p =>
                {
                    var fullPath = Path.GetFullPath(p);
                    return Path.GetRelativePath(
                        Path.GetDirectoryName(solutionFileFullPath),
                        Directory.Exists(fullPath)
                            ? MsbuildProject.GetProjectFileFromDirectory(fullPath).FullName
                            : fullPath);
                });
                RemoveProjectsAsync(solutionFileFullPath, relativeProjectPaths, CancellationToken.None).Wait();
                return 0;
            }
            catch (Exception ex) when (ex is not GracefulException)
            {
                if (ex is SolutionException || ex.InnerException is SolutionException)
                {
                    throw new GracefulException(CommonLocalizableStrings.InvalidSolutionFormatString, solutionFileFullPath, ex.Message);
                }
                // TODO: Check
                if (ex.InnerException is GracefulException)
                {
                    throw ex.InnerException;
                }
                throw new GracefulException(ex.Message, ex);
            }
        }

        private async Task RemoveProjectsAsync(string solutionFileFullPath, IEnumerable<string> projectPaths, CancellationToken cancellationToken)
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

            foreach (var projectPath in projectPaths)
            {
                var project = solution.FindProject(projectPath);
                if (project != null)
                {
                    solution.RemoveProject(project);
                    Reporter.Output.WriteLine(CommonLocalizableStrings.ProjectRemovedFromTheSolution, projectPath);
                }
                else
                {
                    Reporter.Output.WriteLine(CommonLocalizableStrings.ProjectNotFoundInTheSolution, projectPath);
                }
            }

            Dictionary<SolutionFolderModel, SolutionItemModel[]> solutionItemsGroupedByParent = solution.SolutionItems
                .Where(i => i.Parent != null)
                .GroupBy(i => i.Parent)
                .ToDictionary(g => g.Key, g => g.ToArray());

            Dictionary<SolutionFolderModel, int> nonFolderDescendantsCount = new();
            foreach (var item in solution.SolutionFolders)
            {
                CountNonFolderDescendants(solution, item, solutionItemsGroupedByParent, nonFolderDescendantsCount);
            }

            var emptyFolders = nonFolderDescendantsCount.Where(i => i.Value == 0).Select(i => i.Key);
            foreach (var folder in emptyFolders)
            {
                solution.RemoveFolder(folder);
            }

            await serializer.SaveAsync(solutionFileFullPath, solution, cancellationToken);
        }
    }
}
