// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Commands.Hidden.List;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Reference.List;

internal class ReferenceListCommand : CommandBase
{
    private readonly string _fileOrDirectory;

    public ReferenceListCommand(ParseResult parseResult) : base(parseResult)
    {
        ShowHelpOrErrorIfAppropriate(parseResult);

        _fileOrDirectory = parseResult.HasOption(ReferenceCommandParser.ProjectOption) ?
            parseResult.GetValue(ReferenceCommandParser.ProjectOption) :
            parseResult.GetValue(ListCommandParser.SlnOrProjectArgument) ??
            Directory.GetCurrentDirectory();
    }

    public override int Execute()
    {
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
}
