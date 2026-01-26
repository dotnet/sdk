// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.New;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal sealed class AliasShowCommand(Func<ParseResult, ITemplateEngineHost> hostBuilder, NewAliasShowCommandDefinition definition)
        : BaseCommand<AliasShowCommandArgs, NewAliasShowCommandDefinition>(hostBuilder, definition)
    {
        protected override Task<NewCommandStatus> ExecuteAsync(
            AliasShowCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult parseResult,
            CancellationToken cancellationToken) => throw new NotImplementedException();

        protected override AliasShowCommandArgs ParseContext(ParseResult parseResult) => new(parseResult);
    }
}
