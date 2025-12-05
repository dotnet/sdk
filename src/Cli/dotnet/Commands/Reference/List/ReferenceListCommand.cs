// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.List;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.MSBuildEvaluation;

namespace Microsoft.DotNet.Cli.Commands.Reference.List;

internal class ReferenceListCommand : CommandBase
{
    private readonly string _fileOrDirectory;

    public ReferenceListCommand(ParseResult parseResult) : base(parseResult)
    {
        ShowHelpOrErrorIfAppropriate(parseResult);

        _fileOrDirectory = (parseResult.HasOption(ReferenceCommandParser.ProjectOption) ?
            parseResult.GetValue(ReferenceCommandParser.ProjectOption) :
            parseResult.GetValue(ListCommandParser.SlnOrProjectArgument)) ??
            Directory.GetCurrentDirectory()!;
    }

    public override int Execute()
    {
        using var evaluator = DotNetProjectEvaluatorFactory.CreateForCommand();
        var project = evaluator.LoadProject(_fileOrDirectory);
        var p2ps = project.ProjectReferences;
        if (!p2ps.Any())
        {
            Reporter.Output.WriteLine(string.Format(CliStrings.NoReferencesFound, CliStrings.P2P, _fileOrDirectory));
            return 0;
        }

        Reporter.Output.WriteLine($"{CliStrings.ProjectReferenceOneOrMore}");
        Reporter.Output.WriteLine(new string('-', CliStrings.ProjectReferenceOneOrMore.Length));
        foreach (var item in p2ps)
        {
            Reporter.Output.WriteLine(item.EvaluatedInclude);
        }

        return 0;
    }
}
