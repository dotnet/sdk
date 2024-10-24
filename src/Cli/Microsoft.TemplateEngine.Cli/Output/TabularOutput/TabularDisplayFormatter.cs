// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.TabularOutput;

namespace Microsoft.TemplateEngine.Cli.Output.TabularOutput;

internal class TabularDisplayFormatter(TabularOutputSettings outputSettings) : IDisplayFormatter
{
    string IDisplayFormatter.FormatTemplateList(
        IReadOnlyCollection<TemplateGroupEntry> templates,
        IEnvironment environment)
    {
        var tableRows = templates.Select(template => FormatEntry(template, environment)).ToArray();

        return GenerateTableDisplay(tableRows, outputSettings);
    }

    private static string GenerateTableDisplay(
        IReadOnlyCollection<TemplateGroupTableRow> groupsForDisplay,
        TabularOutputSettings tabularOutputSettings)
    {
        TabularOutput<TemplateGroupTableRow> formatter =
            Cli.TabularOutput.TabularOutput
                .For(tabularOutputSettings, groupsForDisplay)
                .DefineColumn(t => t.Name, out object? nameColumn, LocalizableStrings.ColumnNameTemplateName, shrinkIfNeeded: true, minWidth: 15, showAlways: true)
                .DefineColumn(t => t.ShortNames, LocalizableStrings.ColumnNameShortName, showAlways: true)
                .DefineColumn(t => t.Languages, out object? languageColumn, LocalizableStrings.ColumnNameLanguage, TabularOutputSettings.ColumnNames.Language, defaultColumn: true)
                .DefineColumn(t => t.Type, LocalizableStrings.ColumnNameType, TabularOutputSettings.ColumnNames.Type, defaultColumn: false)
                .DefineColumn(t => t.Author, LocalizableStrings.ColumnNameAuthor, TabularOutputSettings.ColumnNames.Author, defaultColumn: false, shrinkIfNeeded: true, minWidth: 10)
                .DefineColumn(t => t.Classifications, out object? tagsColumn, LocalizableStrings.ColumnNameTags, TabularOutputSettings.ColumnNames.Tags, defaultColumn: true)
                .OrderBy(nameColumn, StringComparer.OrdinalIgnoreCase);

        return formatter.Layout();
    }

    private TemplateGroupTableRow FormatEntry(TemplateGroupEntry entry, IEnvironment environment)
    {
        return new TemplateGroupTableRow
        {
            Name = entry.Name,
            ShortNames = JoinDistinct(entry.ShortNames),
            Languages = FormatLanguages(entry.Languages),
            Classifications = FormatClassifications(entry.Classifications),
            Author = JoinDistinct(entry.Authors, environment.NewLine),
            Type = FormatTypes(entry.Types),
        };
    }

    private string FormatClassifications(IEnumerable<string> classifications)
    {
        var notEmpty = classifications
            .Where(classification => !string.IsNullOrWhiteSpace(classification));

        return JoinDistinct(notEmpty, "/");

    }

    private string FormatTypes(IEnumerable<string> types)
    {
        var orderedDistinct = types
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(type => type, StringComparer.OrdinalIgnoreCase);

        return string.Join(",", orderedDistinct);
    }

    private string FormatLanguages(IEnumerable<TemplateLanguageEntry> languages)
    {
        var values = languages
            .Where(lang => lang.Default is not true)
            .OrderBy(lang => lang.Id, StringComparer.OrdinalIgnoreCase)
            .Select(lang => lang.Id);

        var defaultLang = languages.FirstOrDefault(lang => lang.Default);

        if (defaultLang.Id is not null)
        {
            values = values.Prepend($"[{defaultLang.Id}]");
        }

        return JoinDistinct(values);
    }

    private string JoinDistinct(IEnumerable<string> values, string separator = ",")
    {
        var distinctValues = values.Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(", ", distinctValues);
    }
}
