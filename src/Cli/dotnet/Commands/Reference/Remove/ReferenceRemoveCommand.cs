// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Commands.Run;

namespace Microsoft.DotNet.Cli.Commands.Reference.Remove;

internal sealed class ReferenceRemoveCommand : CommandBase<ReferenceRemoveCommandDefinitionBase>
{
    private readonly string _fileOrDirectory;
    private readonly IReadOnlyCollection<string> _arguments;
    private readonly AppKinds _allowedAppKinds;

    public ReferenceRemoveCommand(ParseResult parseResult)
        : base(parseResult)
    {
        (_fileOrDirectory, _allowedAppKinds) = PackageCommandParser.ProcessPathOptions(
            Definition.GetFileOption(),
            Definition.GetProjectOption(),
            Definition.GetProjectOrFileArgument(),
            parseResult);
        _arguments = parseResult.GetValue(Definition.ProjectPathArgument).ToList().AsReadOnly();

        if (_arguments.Count == 0)
        {
            throw new GracefulException(CliStrings.SpecifyAtLeastOneReferenceToRemove);
        }
    }

    public override int Execute()
    {
        var msbuildProj = MsbuildProject.FromFileOrDirectory(new ProjectCollection(), _fileOrDirectory, false, _allowedAppKinds);

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
