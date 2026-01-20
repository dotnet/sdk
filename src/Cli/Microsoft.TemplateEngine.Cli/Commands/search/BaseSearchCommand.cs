// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.New;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.TemplateSearch;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal abstract class BaseSearchCommand(Func<ParseResult, ITemplateEngineHost> hostBuilder, NewSearchCommandDefinition definition)
        : BaseCommand<SearchCommandArgs, NewSearchCommandDefinition>(hostBuilder, definition),
          IFilterableCommand,
          ITabularOutputCommand
    {
        public IEnumerable<Option> FilterOptions => Definition.FilterOptions.AllOptions;

        public Option<bool> ColumnsAllOption => Definition.ColumnsAllOption;

        public Option<string[]> ColumnsOption => Definition.ColumnsOption;

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
