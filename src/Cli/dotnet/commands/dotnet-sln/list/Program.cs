// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace Microsoft.DotNet.Tools.Sln.List
{
    internal class ListProjectsInSolutionCommand : CommandBase
    {
        private readonly string _fileOrDirectory;
        private readonly bool _displaySolutionFolders;

        public ListProjectsInSolutionCommand(
            ParseResult parseResult) : base(parseResult)
        {
            _fileOrDirectory = Path.GetFullPath(parseResult.GetValue(SlnCommandParser.SlnArgument));
            _displaySolutionFolders = parseResult.GetValue(SlnListParser.SolutionFolderOption);
        }

        public override int Execute()
        {
            string slnFileFullPath = SlnCommandParser.GetSlnFileFullPath(_fileOrDirectory);
            try
            {
                ListAllProjectsAsync(slnFileFullPath, CancellationToken.None).Wait();
            }
            catch (Exception ex)
            {
                throw new GracefulException(ex.Message, ex);
            }
            return 0;
            /*var slnFile = SlnFileFactory.CreateFromFileOrDirectory(_fileOrDirectory);

            string[] paths;

            if (_displaySolutionFolders)
            {
                paths = slnFile.Projects
                    .GetProjectsByType(ProjectTypeGuids.SolutionFolderGuid)
                    .Select(folder => folder.GetFullSolutionFolderPath())
                    .ToArray();
            }
            else
            {
                paths = slnFile.Projects
                    .GetProjectsNotOfType(ProjectTypeGuids.SolutionFolderGuid)
                    .Select(project => project.FilePath)
                    .ToArray();
            }

            if (paths.Length == 0)
            {
                Reporter.Output.WriteLine(CommonLocalizableStrings.NoProjectsFound);
            }
            else
            {
                Array.Sort(paths);

                string header = _displaySolutionFolders ? LocalizableStrings.SolutionFolderHeader : LocalizableStrings.ProjectsHeader;
                Reporter.Output.WriteLine($"{header}");
                Reporter.Output.WriteLine(new string('-', header.Length));
                foreach (string slnProject in paths)
                {
                    Reporter.Output.WriteLine(slnProject);
                }
            }*/
        }

        private async Task ListAllProjectsAsync(string solutionFullPath, CancellationToken cancellationToken)
        {
            ISolutionSerializer? serializer = SolutionSerializers.GetSerializerByMoniker(solutionFullPath);
            if (serializer == null)
            {
                return;
            }

            SolutionModel solution = await serializer.OpenAsync(solutionFullPath, cancellationToken);
            string[] paths;
            // TODO: This can be simplified
            if (_displaySolutionFolders)
            {
                paths = solution.SolutionProjects
                    .Where(solution => solution.Type == ProjectTypeGuids.SolutionFolderGuid)
                    .Select(folder => Path.GetFullPath(folder.FilePath))
                    .ToArray();
            }
            else
            {
                paths = solution.SolutionProjects
                    .Where(solution => solution.Type != ProjectTypeGuids.SolutionFolderGuid)
                    .Select(folder => folder.FilePath)
                    .ToArray();
            }

            if (paths.Length == 0)
            {
                Reporter.Output.WriteLine(CommonLocalizableStrings.NoProjectsFound);
            }
            else
            {
                Array.Sort(paths);

                string header = _displaySolutionFolders ? LocalizableStrings.SolutionFolderHeader : LocalizableStrings.ProjectsHeader;
                Reporter.Output.WriteLine($"{header}");
                Reporter.Output.WriteLine(new string('-', header.Length));
                foreach (string slnProject in paths)
                {
                    Reporter.Output.WriteLine(slnProject);
                }
            }

        }
    }
}
