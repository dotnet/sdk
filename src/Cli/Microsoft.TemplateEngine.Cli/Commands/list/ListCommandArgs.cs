// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.New;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal sealed class ListCommandArgs : BaseFilterableArgs<NewListCommandDefinition>, ITabularOutputArgs
    {
        internal ListCommandArgs(BaseListCommand command, ParseResult parseResult)
            : base(parseResult)
        {
            string? nameCriteria = parseResult.GetValue(command.Definition.NameArgument);
            if (!string.IsNullOrWhiteSpace(nameCriteria))
            {
                ListNameCriteria = nameCriteria;
            }
            // for legacy case new command argument is also accepted
            else if (command is LegacyListCommand)
            {
                var newCommand = (NewCommand)command.Parents.Single();

                string? newCommandArgument = parseResult.GetValue(newCommand.Definition.ShortNameArgument);
                if (!string.IsNullOrWhiteSpace(newCommandArgument))
                {
                    ListNameCriteria = newCommandArgument;
                }
            }
            (DisplayAllColumns, ColumnsToDisplay) = ParseTabularOutputSettings(command, parseResult);

            if (AppliedFilters.Contains(FilterOptionDefinition.LanguageFilter))
            {
                Language = GetFilterValue(FilterOptionDefinition.LanguageFilter);
            }
            IgnoreConstraints = parseResult.GetValue(command.Definition.IgnoreConstraintsOption);
        }

        public bool DisplayAllColumns { get; }

        public IReadOnlyList<string>? ColumnsToDisplay { get; }

        internal string? ListNameCriteria { get; }

        internal string? Language { get; }

        internal bool IgnoreConstraints { get; }
    }
}
