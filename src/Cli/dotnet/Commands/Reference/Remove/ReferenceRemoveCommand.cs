// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Reference.Remove;

internal sealed class ReferenceRemoveCommand : CommandBase<ReferenceRemoveCommandDefinitionBase>
{
    private readonly string _fileOrDirectory;
    private readonly IReadOnlyCollection<string> _arguments;

    public ReferenceRemoveCommand(ParseResult parseResult)
        : base(parseResult)
    {
        if (Definition.GetConflictingPathOptions(parseResult) is ({ } fileOptionName, { } projectOptionName))
        {
            throw new GracefulException(CliCommandStrings.CannotCombineOptions, fileOptionName, projectOptionName);
        }

        _fileOrDirectory = Definition.GetFileOrDirectory(parseResult) ?? Directory.GetCurrentDirectory();
        _arguments = parseResult.GetValue(Definition.ProjectPathArgument).ToList().AsReadOnly();

        if (_arguments.Count == 0)
        {
            throw new GracefulException(CliStrings.SpecifyAtLeastOneReferenceToRemove);
        }
    }

    public override int Execute()
    {
        var msbuildProj = MsbuildProject.FromFileOrDirectory(new ProjectCollection(), _fileOrDirectory, false, Definition.GetAllowedAppKinds(_parseResult));

        if (msbuildProj.IsFileBasedApp && !string.IsNullOrEmpty(_parseResult.GetValue(Definition.FrameworkOption)))
        {
            throw new GracefulException(CliCommandStrings.InvalidOptionForFileBasedApp, Definition.FrameworkOption.Name);
        }

        var references = _arguments.Select(p =>
        {
            var fullPath = Path.GetFullPath(p);
            if (!Directory.Exists(fullPath))
            {
                return p;
            }

            return Path.GetRelativePath(
                msbuildProj.ProjectRootElement.FullPath,
                MsbuildProject.GetProjectFileFromDirectory(fullPath)
            );
        });

        int numberOfRemovedReferences = msbuildProj.RemoveProjectToProjectReferences(
            _parseResult.GetValue(Definition.FrameworkOption),
            references);

        if (numberOfRemovedReferences != 0)
        {
            msbuildProj.Save();
        }

        return 0;
    }
}
