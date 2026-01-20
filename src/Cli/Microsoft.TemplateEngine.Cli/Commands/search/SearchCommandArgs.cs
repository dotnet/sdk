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
        }

        public bool DisplayAllColumns { get; }

        public IReadOnlyList<string>? ColumnsToDisplay { get; }

        internal string? SearchNameCriteria { get; }

        internal string? Language { get; }
    }
}
