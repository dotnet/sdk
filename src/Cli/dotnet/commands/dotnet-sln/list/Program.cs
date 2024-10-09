// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.Tool.List;

namespace Microsoft.DotNet.Tools.Sln.List
{
    internal class ListProjectsInSolutionCommand : CommandBase
    {
        private readonly static JsonSerializerOptions s_noEscapeJsonSerializerOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        private readonly string _fileOrDirectory;
        private readonly bool _displaySolutionFolders;
        private readonly SlnListReportOutputFormat _outputFormat;

        public ListProjectsInSolutionCommand(
            ParseResult parseResult) : base(parseResult)
        {
            _fileOrDirectory = parseResult.GetValue(SlnCommandParser.SlnArgument);
            _displaySolutionFolders = parseResult.GetValue(SlnListParser.SolutionFolderOption);
            _outputFormat = parseResult.GetValue(SlnListParser.OutputFormatOption);
        }

        public override int Execute()
        {
            var slnFile = SlnFileFactory.CreateFromFileOrDirectory(_fileOrDirectory);

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

                switch (_outputFormat)
                {
                    case SlnListReportOutputFormat.text:
                        string header = _displaySolutionFolders ? LocalizableStrings.SolutionFolderHeader : LocalizableStrings.ProjectsHeader;
                        Reporter.Output.WriteLine($"{header}");
                        Reporter.Output.WriteLine(new string('-', header.Length));

                        foreach (string slnProject in paths)
                        {
                            Reporter.Output.WriteLine(slnProject);
                        }
                        break;

                    case SlnListReportOutputFormat.json:
                        var jsonArray = JsonSerializer.Serialize(paths, s_noEscapeJsonSerializerOptions);
                        Reporter.Output.WriteLine(jsonArray);
                        break;
                }
            }
            return 0;
        }
    }
}
