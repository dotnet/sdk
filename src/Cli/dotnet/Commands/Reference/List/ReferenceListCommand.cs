// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Commands.Hidden.List.Reference;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.Cli.Commands.Run;

namespace Microsoft.DotNet.Cli.Commands.Reference.List;

internal class ReferenceListCommand : CommandBase<ListReferenceCommandDefinitionBase>
{
    private readonly string _fileOrDirectory;
    private readonly AppKinds _allowedAppKinds;

    public ReferenceListCommand(ParseResult parseResult)
        : base(parseResult)
    {
        ShowHelpOrErrorIfAppropriate(parseResult);

        (_fileOrDirectory, _allowedAppKinds) = PackageCommandParser.ProcessPathOptions(
            Definition.GetFileOption(),
            Definition.GetProjectOption(),
            Definition.GetProjectOrFileArgument(),
            parseResult);
    }

    public override int Execute()
    {
        var msbuildProj = MsbuildProject.FromFileOrDirectory(new ProjectCollection(), _fileOrDirectory, false, _allowedAppKinds);
        var p2ps = msbuildProj.GetProjectToProjectReferences();
        if (!p2ps.Any())
        {
            Reporter.Output.WriteLine(string.Format(CliStrings.NoReferencesFound, CliStrings.P2P, _fileOrDirectory));
            return 0;
        }

        ProjectInstance projectInstance = new(msbuildProj.ProjectRootElement);
        Reporter.Output.WriteLine($"{CliStrings.ProjectReferenceOneOrMore}");
        Reporter.Output.WriteLine(new string('-', CliStrings.ProjectReferenceOneOrMore.Length));
        foreach (var include in p2ps.SelectMany(static item => item.Includes()))
        {
            Reporter.Output.WriteLine(projectInstance.ExpandString(include));
        }

        return 0;
    }
}
