// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectTools;

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
        if (_allowedAppKinds.HasFlag(AppKinds.FileBased) && VirtualProjectBuilder.IsValidEntryPointPath(_fileOrDirectory))
        {
            return ExecuteForFileBasedApp();
        }

        if (!_allowedAppKinds.HasFlag(AppKinds.ProjectBased))
        {
            throw new GracefulException(CliCommandStrings.InvalidFilePath, _fileOrDirectory);
        }

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
                MsbuildProject.GetProjectFileFromDirectory(fullPath)
            );
        });

        int numberOfRemovedReferences = msbuildProj.RemoveProjectToProjectReferences(
            _parseResult.GetValue(Definition.FrameworkOption),
            references);

        if (numberOfRemovedReferences != 0)
        {
            msbuildProj.ProjectRootElement.Save();
        }

        return 0;
    }

    private int ExecuteForFileBasedApp()
    {
        if (!string.IsNullOrEmpty(_parseResult.GetValue(Definition.FrameworkOption)))
        {
            throw new GracefulException(CliCommandStrings.InvalidOptionForFileBasedApp, Definition.FrameworkOption.Name);
        }

        var editor = new FileBasedAppReferenceEditor(_fileOrDirectory);
        int numberOfRemovedReferences = editor.RemoveReferences(_arguments);
        if (numberOfRemovedReferences != 0)
        {
            editor.Save();
        }

        return 0;
    }
}
