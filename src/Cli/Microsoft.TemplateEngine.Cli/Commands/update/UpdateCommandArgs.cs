// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands;

internal sealed class UpdateCommandArgs(BaseUpdateCommand command, ParseResult parseResult) : GlobalArgs(parseResult)
{
    public bool CheckOnly { get; } = command.Definition.GetCheckOnlyValue(parseResult);
    public bool Interactive { get; } = parseResult.GetValue(command.Definition.InteractiveOption);
    public IReadOnlyList<string>? AdditionalSources { get; } = parseResult.GetValue(command.Definition.AddSourceOption);
}
