// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.JsonOutput;

namespace Microsoft.TemplateEngine.Cli.Output.JsonOutput;

internal class JsonDisplayFormatter : IDisplayFormatter
{
    string IDisplayFormatter.FormatTemplateList(IReadOnlyCollection<TemplateGroupEntry> templates, IEnvironment environment)
    {
        var jsonEntries = templates.Select(FormatJsonEntry);
        return JsonSerializer.Serialize(jsonEntries);
    }

    private JsonTemplateGroupEntry FormatJsonEntry(TemplateGroupEntry templateGroupEntry)
    {
        return new JsonTemplateGroupEntry
        {
            Name = templateGroupEntry.Name,
            Authors = templateGroupEntry.Authors.ToArray(),
            Languages = FormatJsonLanguageEntries(templateGroupEntry.Languages).ToArray(),
            ShortNames = templateGroupEntry.ShortNames.ToArray(),
            Classifications = templateGroupEntry.Classifications.ToArray(),
            Types = templateGroupEntry.Types.ToArray(),

        };
    }

    private IEnumerable<JsonLanguageEntry> FormatJsonLanguageEntries(IEnumerable<TemplateLanguageEntry> entries)
        => entries.Select(entry => new JsonLanguageEntry { Id = entry.Id, Default = entry.Default });
}
