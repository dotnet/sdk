// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Build.Evaluation;
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
    private readonly IReadOnlyList<string> _arguments;

    public AddFileToSolutionCommand(ParseResult parseResult) : base(parseResult)
    {
        var projectPaths = parseResult.GetValue(SlnAddParser.ProjectPathArgument)?.ToArray() ?? [];
        if (projectPaths.Length != 0)
        {
            throw new GracefulException(LocalizableStrings.ProjectPathArgumentShouldNotBeProvidedForDotnetSlnAddFile);
        }

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
            throw new GracefulException(Tools.Sln.LocalizableStrings.SpecifyAtLeastOneFileToAdd);
        }

        PathUtility.EnsureAllPathsExist(arguments, Tools.Sln.LocalizableStrings.CouldNotFindFile, allowDirectories: false);

        var relativeFilePaths = _arguments.Select(f =>
        {
            var fullFilePath = Path.GetFullPath(f);
            return Path.GetRelativePath(
                PathUtility.EnsureTrailingSlash(slnFile.BaseDirectory),
                fullFilePath);
        }).ToList();

        var solutionFileName = Path.GetFileName(slnFile.FullPath);
        foreach (var relativeFilePath in relativeFilePaths)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(solutionFileName, relativeFilePath))
            {
                throw new GracefulException(LocalizableStrings.CannotAddTheSameSolutionToItself);
            }

            foreach (var project in slnFile.Projects)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(relativeFilePath, project.FilePath))
                {
                    throw new GracefulException(LocalizableStrings.CannotAddExistingProjectAsSolutionItem);
                }

                if (project.GetSolutionItemsSectionOrDefault() is not SlnSection solutionItems) { continue; }
                Dictionary<string, string> solutionItemsDictionary = new(solutionItems.GetContent(), StringComparer.OrdinalIgnoreCase);
                if (solutionItemsDictionary.ContainsKey(relativeFilePath))
                {
                    throw new GracefulException(LocalizableStrings.CannotAddExistingSolutionItem);
                }
            }
        }

        var preAddSolutionItemsCount = slnFile.Projects
            .Sum(p => p.GetSolutionItemsSectionOrDefault() is { } solutionItems ? solutionItems.GetContent().Count() : 0);

        var solutionFolders = _relativeRootSolutionFolders ?? ["Solution Items"];

        foreach (var relativeFilePath in relativeFilePaths)
        {
            // Identify the intended solution folders
            foreach (var solutionFolder in solutionFolders)
            {
                if (slnFile.SolutionFolderContainsSolutionItem(solutionFolder, relativeFilePath))
                {
                    throw new GracefulException(string.Format(LocalizableStrings.SolutionItemWithTheSameNameExists, relativeFilePath, solutionFolder));
                }
            }

            slnFile.AddSolutionItem(relativeFilePath, solutionFolders);
        }

        var postAddSolutionItemsCount = slnFile.Projects
            .Sum(p => p.GetSolutionItemsSectionOrDefault() is { } solutionItems ? solutionItems.GetContent().Count() : 0);
        if (postAddSolutionItemsCount > preAddSolutionItemsCount)
        {
            slnFile.Write();
        }

        return 0;
    }
}
