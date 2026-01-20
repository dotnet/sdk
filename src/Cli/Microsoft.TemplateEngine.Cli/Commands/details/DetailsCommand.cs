// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.New;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.NuGet;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal sealed class DetailsCommand(Func<ParseResult, ITemplateEngineHost> hostBuilder, NewDetailsCommandDefinition definition)
        : BaseCommand<DetailsCommandArgs, NewDetailsCommandDefinition>(hostBuilder, definition)
    {
        protected override async Task<NewCommandStatus> ExecuteAsync(
            DetailsCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult parseResult,
            CancellationToken cancellationToken)
        {
            var templatePackageCoordinator = new TemplatePackageCoordinator(environmentSettings, templatePackageManager);

            NewCommandStatus status = await templatePackageCoordinator.DisplayTemplatePackageMetadata(
                args.NameCriteria,
                args.VersionCriteria,
                args.Interactive,
                args.AdditionalSources,
                new NugetApiManager(),
                cancellationToken).ConfigureAwait(false);

            await CheckTemplatesWithSubCommandName(args, templatePackageManager, cancellationToken).ConfigureAwait(false);
            return status;
        }

        protected override DetailsCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}
