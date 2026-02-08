// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal abstract class BaseUninstallCommand(Func<ParseResult, ITemplateEngineHost> hostBuilder, CommandDefinition definition)
        : BaseCommand<UninstallCommandArgs>(hostBuilder, definition)
    {
        protected override Task<NewCommandStatus> ExecuteAsync(
            UninstallCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult parseResult,
            CancellationToken cancellationToken)
        {
            TemplatePackageCoordinator templatePackageCoordinator = new(environmentSettings, templatePackageManager);

            return templatePackageCoordinator.EnterUninstallFlowAsync(args, cancellationToken);
        }

        protected override UninstallCommandArgs ParseContext(ParseResult parseResult)
        {
            return new UninstallCommandArgs(this, parseResult);
        }
    }
}
