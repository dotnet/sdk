// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.Sln.Add;

internal class AddFileToSolutionCommand : CommandBase
{
    private readonly string _fileOrDirectory;
    private readonly bool _inRoot;
    private readonly IList<string> _relativeRootSolutionFolders;
    private readonly IReadOnlyCollection<string> _arguments;

    public AddFileToSolutionCommand(ParseResult parseResult) : base(parseResult)
    {
        _fileOrDirectory = parseResult.GetValue(SlnCommandParser.SlnArgument);

        _arguments = parseResult.GetValue(SlnAddFileParser.FilePathArgument).ToArray();

        _inRoot = parseResult.GetValue(SlnAddFileParser.InRootOption);
        string relativeRoot = parseResult.GetValue(SlnAddFileParser.SolutionFolderOption);

        SlnArgumentValidator.ParseAndValidateArguments(_arguments, SlnArgumentValidator.CommandType.Add, _inRoot, relativeRoot, subcommand: "file");

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
        SlnFile slnFile = SlnFileFactory.CreateFromFileOrDirectory(_fileOrDirectory);

        var arguments = _parseResult.GetValue(SlnAddFileParser.FilePathArgument).ToArray();
        if (arguments.Length == 0)
        {
            throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneFileToAdd);
        }

        PathUtility.EnsureAllPathsExist(arguments, CommonLocalizableStrings.CouldNotFindFile, true);

        var fullFilePaths = _arguments.Select(f =>
        {
            var fullPath = Path.GetFullPath(f);
            return Directory.Exists(fullPath) ?
                MsbuildProject.GetProjectFileFromDirectory(fullPath).FullName :
                fullPath;
        }).ToList();

        var preAddProjectCount = slnFile.Projects.Count;
        var solutionFolders = _relativeRootSolutionFolders ?? ["Solution Items"];

        foreach (var fullFilePath in fullFilePaths)
        {
            var relativeFilePath = Path.GetRelativePath(
                PathUtility.EnsureTrailingSlash(slnFile.BaseDirectory),
                fullFilePath);

            // Identify the intended solution folders
            foreach (var solutionFolder in solutionFolders)
            {
                if (slnFile.SolutionFolderContainsSolutionItem(solutionFolder, fullFilePath))
                {
                    throw new GracefulException(string.Format(LocalizableStrings.SolutionItemWithTheSameNameExists, relativeFilePath, solutionFolder));
                }
            }

            slnFile.AddSolutionItem(fullFilePath, solutionFolders);
        }

        if (slnFile.Projects.Count > preAddProjectCount)
        {
            slnFile.Write();
        }

        return 0;
    }
}
