// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Cli.Output;

internal struct TemplateGroupEntry
{
    internal string Name { get; set; }

    internal IReadOnlyCollection<string> ShortNames { get; set; }

    internal IReadOnlyCollection<string> Authors { get; set; }

    internal IReadOnlyCollection<string> Types { get; set; }

    internal IReadOnlyCollection<string> Classifications { get; set; }

    internal IReadOnlyCollection<TemplateLanguageEntry> Languages { get; set; }
}
