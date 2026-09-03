// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli;

public abstract class CommandBase
{
    protected ParseResult _parseResult;

    protected CommandBase(ParseResult parseResult)
    {
        _parseResult = parseResult;
        parseResult.ShowHelpOrErrorIfAppropriate();
    }

    protected CommandBase() { }

    internal string MSBuildSubmissionMetricCommandName { get; set; }

    protected void RecordProcessStartToMSBuildSubmission()
    {
        if (MSBuildSubmissionMetricCommandName is not null)
        {
            Microsoft.DotNet.Cli.Utils.CliMetrics.RecordProcessStartToMSBuildSubmission(MSBuildSubmissionMetricCommandName);
            MSBuildSubmissionMetricCommandName = null;
        }
    }

    public abstract int Execute();
}

public abstract class CommandBase<TDefinition>(ParseResult parseResult) : CommandBase(parseResult)
    where TDefinition : Command
{
    protected TDefinition Definition { get; } = (TDefinition)parseResult.CommandResult.Command;
}
