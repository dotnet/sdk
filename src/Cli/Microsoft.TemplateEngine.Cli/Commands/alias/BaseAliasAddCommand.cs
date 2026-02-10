// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class BaseAliasAddCommand(Func<ParseResult, ITemplateEngineHost> hostBuilder, CommandDefinition definition)
        : BaseCommand<AliasAddCommandArgs>(hostBuilder, definition)
    {
        protected override Task<NewCommandStatus> ExecuteAsync(
            AliasAddCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult parseResult,
            CancellationToken cancellationToken) => throw new NotImplementedException();

        protected override AliasAddCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}
