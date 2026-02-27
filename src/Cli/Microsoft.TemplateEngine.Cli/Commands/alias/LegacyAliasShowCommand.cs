// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands;

internal sealed class LegacyAliasShowCommand(Func<ParseResult, ITemplateEngineHost> hostBuilder)
    : BaseAliasShowCommand(hostBuilder, CommandDefinition.Alias.Show.LegacyCommand)
{
}
