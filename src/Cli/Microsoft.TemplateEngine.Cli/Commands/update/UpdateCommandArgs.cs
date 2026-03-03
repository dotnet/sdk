// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands;

internal sealed class UpdateCommandArgs : GlobalArgs
{
    public UpdateCommandArgs(ParseResult parseResult)
        : base(parseResult)
    {
        var definition = ((IUpdateCommand)parseResult.CommandResult.Command).Definition;
        CheckOnly = definition.GetCheckOnlyValue(parseResult);
        Interactive = parseResult.GetValue(definition.InteractiveOption);
        AdditionalSources = parseResult.GetValue(definition.AddSourceOption);
    }

    public bool CheckOnly { get; }
    public bool Interactive { get; }
    public IReadOnlyList<string>? AdditionalSources { get; }
}
