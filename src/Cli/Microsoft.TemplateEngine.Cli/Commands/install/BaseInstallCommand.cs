// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.New;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal abstract class BaseInstallCommand(Func<ParseResult, ITemplateEngineHost> hostBuilder, NewInstallCommandDefinition definition)
        : BaseCommand<InstallCommandArgs, NewInstallCommandDefinition>(hostBuilder, definition)
    {
        protected override Task<NewCommandStatus> ExecuteAsync(
            InstallCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult parseResult,
            CancellationToken cancellationToken)
        {
            TemplatePackageCoordinator templatePackageCoordinator = new(environmentSettings, templatePackageManager);
            return templatePackageCoordinator.EnterInstallFlowAsync(args, cancellationToken);
        }

        protected override InstallCommandArgs ParseContext(ParseResult parseResult)
            => new(this, parseResult);
    }
}
