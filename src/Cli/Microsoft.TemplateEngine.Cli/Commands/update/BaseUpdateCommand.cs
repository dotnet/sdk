// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class BaseUpdateCommand : BaseCommand<UpdateCommandArgs>
    {
        internal BaseUpdateCommand(
            NewCommand parentCommand,
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            string commandName,
            string description)
            : base(hostBuilder, commandName, description)
        {
            ParentCommand = parentCommand;
            this.Options.Add(InteractiveOption);
            this.Options.Add(AddSourceOption);
        }

        internal virtual CliOption<bool> InteractiveOption { get; } = SharedOptionsFactory.CreateInteractiveOption();

        internal virtual CliOption<string[]> AddSourceOption { get; } = SharedOptionsFactory.CreateAddSourceOption();

        protected NewCommand ParentCommand { get; }

        protected override Task<NewCommandStatus> ExecuteAsync(
            UpdateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult context,
            CancellationToken cancellationToken)
        {
            TemplatePackageCoordinator templatePackageCoordinator = new TemplatePackageCoordinator(environmentSettings, templatePackageManager);

            return templatePackageCoordinator.EnterUpdateFlowAsync(args, cancellationToken);
        }

        protected override UpdateCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}
