// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Commands.Hidden.List.Reference;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Reference.List;

internal class ReferenceListCommand : CommandBase<ListReferenceCommandDefinitionBase>
{
    private readonly string _fileOrDirectory;
    private readonly AppKinds _allowedAppKinds;

    public ReferenceListCommand(ParseResult parseResult)
        : base(parseResult)
    {
        (_fileOrDirectory, _allowedAppKinds) = PackageCommandParser.ProcessPathOptions(
            Definition.GetFileOption(),
            Definition.GetProjectOption(),
            Definition.GetProjectOrFileArgument(),
            parseResult);
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
        var p2ps = msbuildProj.GetProjectToProjectReferences();
        if (!p2ps.Any())
        {
            Reporter.Output.WriteLine(string.Format(CliStrings.NoReferencesFound, CliStrings.P2P, _fileOrDirectory));
            return 0;
        }

        ProjectInstance projectInstance = new(msbuildProj.ProjectRootElement);
        Reporter.Output.WriteLine($"{CliStrings.ProjectReferenceOneOrMore}");
        Reporter.Output.WriteLine(new string('-', CliStrings.ProjectReferenceOneOrMore.Length));
        foreach (var item in projectInstance.GetItems("ProjectReference"))
        {
            Reporter.Output.WriteLine(item.EvaluatedInclude);
        }

        return 0;
    }

    private int ExecuteForFileBasedApp()
    {
        var references = new FileBasedAppReferenceEditor(_fileOrDirectory).GetReferencesForDisplay().ToList();
        if (references.Count == 0)
        {
            Reporter.Output.WriteLine(string.Format(CliStrings.NoReferencesFound, CliStrings.P2P, _fileOrDirectory));
            return 0;
        }

        Reporter.Output.WriteLine($"{CliStrings.ProjectReferenceOneOrMore}");
        Reporter.Output.WriteLine(new string('-', CliStrings.ProjectReferenceOneOrMore.Length));
        foreach (var reference in references)
        {
            Reporter.Output.WriteLine(reference);
        }

        return 0;
    }
}
