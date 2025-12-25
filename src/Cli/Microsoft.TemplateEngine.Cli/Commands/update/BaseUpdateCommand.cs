// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal abstract class BaseUpdateCommand(Func<ParseResult, ITemplateEngineHost> hostBuilder, CommandDefinition.Update definition)
        : BaseCommand<UpdateCommandArgs>(hostBuilder, definition)
    {
        public CommandDefinition.Update Definition => definition;

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

        protected override UpdateCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}
