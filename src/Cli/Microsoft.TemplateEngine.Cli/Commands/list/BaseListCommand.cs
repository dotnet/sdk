// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal abstract class BaseListCommand : BaseCommand<ListCommandArgs>, IFilterableCommand, ITabularOutputCommand
    {
        private readonly CommandDefinition.List _definition;

        internal BaseListCommand(
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            CommandDefinition.List definition)
            : base(hostBuilder, definition)
        {
            _definition = definition;
        }

        public IReadOnlyDictionary<FilterOptionDefinition, Option> Filters => _definition.Filters;

        public Option<bool> ColumnsAllOption => _definition.ColumnsAllOption;

        public Option<string[]> ColumnsOption => _definition.ColumnsOption;

        protected override Task<NewCommandStatus> ExecuteAsync(
            ListCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult parseResult,
            CancellationToken cancellationToken)
        {
            TemplateListCoordinator templateListCoordinator = new(
                environmentSettings,
                templatePackageManager,
                new HostSpecificDataLoader(environmentSettings));

            return templateListCoordinator.DisplayTemplateGroupListAsync(args, cancellationToken);
        }

        protected override ListCommandArgs ParseContext(ParseResult parseResult)
            => new(this, parseResult);

    }
}
