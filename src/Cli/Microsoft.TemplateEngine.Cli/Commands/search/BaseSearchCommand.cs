// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.TemplateSearch;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal abstract class BaseSearchCommand : BaseCommand<SearchCommandArgs>, IFilterableCommand, ITabularOutputCommand
    {
        private readonly CommandDefinition.Search _definition;

        internal BaseSearchCommand(Func<ParseResult, ITemplateEngineHost> hostBuilder, CommandDefinition.Search definition)
            : base(hostBuilder, definition)
        {
            _definition = definition;
        }

        public IReadOnlyDictionary<FilterOptionDefinition, Option> Filters => _definition.Filters;

        public Option<bool> ColumnsAllOption => _definition.ColumnsAllOption;

        public Option<string[]> ColumnsOption => _definition.ColumnsOption;

        protected override Task<NewCommandStatus> ExecuteAsync(
            SearchCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult parseResult,
            CancellationToken cancellationToken)
        {
            return CliTemplateSearchCoordinator.SearchForTemplateMatchesAsync(
                environmentSettings,
                templatePackageManager,
                args,
                environmentSettings.GetDefaultLanguage(),
                cancellationToken);
        }

        protected override SearchCommandArgs ParseContext(ParseResult parseResult)
            => new(this, parseResult);
    }
}
