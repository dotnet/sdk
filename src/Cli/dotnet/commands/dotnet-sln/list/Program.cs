// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using CommandLocalizableStrings = Microsoft.DotNet.Tools.CommonLocalizableStrings;

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
                throw new GracefulException(CommandLocalizableStrings.InvalidSolutionFormatString, solutionFileFullPath, ex.Message);
            }
        }

        private async Task ListAllProjectsAsync(string solutionFileFullPath, CancellationToken cancellationToken)
        {
            ISolutionSerializer serializer = SlnCommandParser.GetSolutionSerializer(solutionFileFullPath);
            SolutionModel solution = await serializer.OpenAsync(solutionFileFullPath, cancellationToken);
            string[] paths;
            if (_displaySolutionFolders)
            {
                paths = solution.SolutionFolders
                    // VS-SolutionPersistence does not return a path object, so there might be issues with forward/backward slashes on different platforms
                    .Select(folder => Path.GetDirectoryName(folder.Path.TrimStart("/")))
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
