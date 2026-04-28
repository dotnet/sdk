// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.New;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal sealed class LegacySearchCommand(Func<ParseResult, ITemplateEngineHost> hostBuilder, NewSearchCommandDefinition definition)
        : BaseSearchCommand(hostBuilder, definition)
    {
        protected override Task<NewCommandStatus> ExecuteAsync(SearchCommandArgs args, IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager, ParseResult parseResult, CancellationToken cancellationToken)
        {
            PrintDeprecationMessage<LegacySearchCommand, SearchCommand>(args.ParseResult);
            return base.ExecuteAsync(args, environmentSettings, templatePackageManager, parseResult, cancellationToken);
        }
    }
}
