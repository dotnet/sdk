// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Reference.Remove;

internal class ReferenceRemoveCommand : CommandBase
{
    private readonly string _fileOrDirectory;
    private readonly IReadOnlyCollection<string> _arguments;

    public ReferenceRemoveCommand(
        ParseResult parseResult) : base(parseResult)
    {
        _fileOrDirectory = parseResult.HasOption(ReferenceCommandParser.ProjectOption) ?
            parseResult.GetValue(ReferenceCommandParser.ProjectOption) :
            parseResult.GetValue(PackageCommandParser.ProjectOrFileArgument);
        _arguments = parseResult.GetValue(ReferenceRemoveCommandParser.ProjectPathArgument).ToList().AsReadOnly();

        if (_arguments.Count == 0)
        {
            throw new GracefulException(CliStrings.SpecifyAtLeastOneReferenceToRemove);
        }
    }

    public override int Execute()
    {
        var msbuildProj = MsbuildProject.FromFileOrDirectory(new ProjectCollection(), _fileOrDirectory, false);
        var references = _arguments.Select(p =>
        {
            var fullPath = Path.GetFullPath(p);
            if (!Directory.Exists(fullPath))
            {
                return p;
            }

            return Path.GetRelativePath(
                msbuildProj.ProjectRootElement.FullPath,
                MsbuildProject.GetProjectFileFromDirectory(fullPath).FullName
            );
        });

        int numberOfRemovedReferences = msbuildProj.RemoveProjectToProjectReferences(
            _parseResult.GetValue(ReferenceRemoveCommandParser.FrameworkOption),
            references);

        if (numberOfRemovedReferences != 0)
        {
            msbuildProj.ProjectRootElement.Save();
        }

        return 0;
    }
}
