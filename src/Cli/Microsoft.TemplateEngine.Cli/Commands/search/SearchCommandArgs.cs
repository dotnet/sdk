// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.New;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal sealed class SearchCommandArgs : BaseFilterableArgs<NewSearchCommandDefinition>, ITabularOutputArgs
    {
        internal SearchCommandArgs(BaseSearchCommand command, ParseResult parseResult)
            : base(parseResult)
        {
            string? nameCriteria = parseResult.GetValue(command.Definition.NameArgument);
            if (!string.IsNullOrWhiteSpace(nameCriteria))
            {
                SearchNameCriteria = nameCriteria;
            }
            // for legacy case new command argument is also accepted
            else if (command is LegacySearchCommand)
            {
                var newCommand = (NewCommand)command.Parents.Single();
                string? newCommandArgument = parseResult.GetValue(newCommand.Definition.ShortNameArgument);
                if (!string.IsNullOrWhiteSpace(newCommandArgument))
                {
                    SearchNameCriteria = newCommandArgument;
                }
            }
            (DisplayAllColumns, ColumnsToDisplay) = ParseTabularOutputSettings(command, parseResult);

            if (AppliedFilters.Contains(FilterOptionDefinition.LanguageFilter))
            {
                Language = GetFilterValue(FilterOptionDefinition.LanguageFilter);
            }

            // These options are only registered on the modern `dotnet new search` command (see
            // NewSearchCommandDefinition); on the legacy `--search` branch the fields exist but are never
            // added to the parser, so the parsed values are always the option defaults (null/empty/false).
            ConfigFile = parseResult.GetValue(command.Definition.ConfigFileOption)?.FullName;
            Sources = parseResult.GetValue(command.Definition.SourceOption);
            AddSources = parseResult.GetValue(command.Definition.AddSourceOption);
            Interactive = parseResult.GetValue(command.Definition.InteractiveOption);
        }

        public bool DisplayAllColumns { get; }

        public IReadOnlyList<string>? ColumnsToDisplay { get; }

        internal string? SearchNameCriteria { get; }

        internal string? Language { get; }

        /// <summary>
        /// The NuGet config file specified via <c>--configfile</c>, or <see langword="null"/> if not specified.
        /// </summary>
        internal string? ConfigFile { get; }

        /// <summary>
        /// The exclusive set of NuGet feeds specified via <c>--source</c>, or <see langword="null"/>/empty if not specified.
        /// When non-empty, these feeds replace the configured feeds entirely.
        /// </summary>
        internal IReadOnlyList<string>? Sources { get; }

        /// <summary>
        /// Additional NuGet feeds specified via <c>--add-source</c>, or <see langword="null"/>/empty if not specified.
        /// These feeds are added on top of the configured (or <see cref="Sources"/> overridden) feeds.
        /// </summary>
        internal IReadOnlyList<string>? AddSources { get; }

        /// <summary>
        /// Whether interactive NuGet credential prompts are allowed.
        /// </summary>
        internal bool Interactive { get; }
    }
}
