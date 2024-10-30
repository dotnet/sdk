// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;

namespace Microsoft.DotNet.Tools.Sln.List
{
    internal class ListProjectsInSolutionCommand : CommandBase
    {
        private readonly string _fileOrDirectory;
        private readonly bool _displaySolutionFolders;

        public ListProjectsInSolutionCommand(
            ParseResult parseResult) : base(parseResult)
        {
            _fileOrDirectory = parseResult.GetValue(SlnCommandParser.SlnArgument);
            _displaySolutionFolders = parseResult.GetValue(SlnListParser.SolutionFolderOption);
        }

        public override int Execute()
        {
            string solutionFileFullPath = SlnCommandParser.GetSlnFileFullPath(_fileOrDirectory);
            try
            {
                ListAllProjectsAsync(solutionFileFullPath, CancellationToken.None).Wait();
                return 0;
            }
            catch (Exception ex)
            {
                throw new GracefulException(ex.Message, ex);
            }
        }

        private async Task ListAllProjectsAsync(string solutionFileFullPath, CancellationToken cancellationToken)
        {
            Guid solutionFolderGuid = new Guid(ProjectTypeGuids.SolutionFolderGuid);
            ISolutionSerializer serializer = SlnCommandParser.GetSolutionSerializer(solutionFileFullPath);
            SolutionModel solution = await serializer.OpenAsync(solutionFileFullPath, cancellationToken);
            string[] paths;
            if (_displaySolutionFolders)
            {
                paths = solution.SolutionFolders
                    .Select(folder => folder.Path.Substring(1))
                    .ToArray();
            }
            else
            {
                paths = solution.SolutionProjects
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
                Reporter.Output.WriteLine(header);
                Reporter.Output.WriteLine(new string('-', header.Length));
                foreach (string slnProject in paths)
                {
                    Reporter.Output.WriteLine(slnProject);
                }
            }

        }
    }
}
