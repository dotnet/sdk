// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal interface IUpdateCommand
    {
        UpdateCommandDefinitionBase Definition { get; }
    }

    internal abstract class BaseUpdateCommand<TDefinition>(Func<ParseResult, ITemplateEngineHost> hostBuilder, TDefinition definition)
        : BaseCommand<UpdateCommandArgs, TDefinition>(hostBuilder, definition), IUpdateCommand
        where TDefinition : UpdateCommandDefinitionBase
    {
        UpdateCommandDefinitionBase IUpdateCommand.Definition => Definition;

        protected override Task<NewCommandStatus> ExecuteAsync(
            UpdateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult context,
            CancellationToken cancellationToken)
        {
            TemplatePackageCoordinator templatePackageCoordinator = new(environmentSettings, templatePackageManager);

            return templatePackageCoordinator.EnterUpdateFlowAsync(args, cancellationToken);
        }

        protected override UpdateCommandArgs ParseContext(ParseResult parseResult) => new(parseResult);
    }
}
