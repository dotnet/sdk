// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Cli.JsonOutput;

internal struct JsonTemplateGroupEntry
{
    internal string Name { get; set; }

    internal string[] ShortNames { get; set; }

    internal string[] Authors { get; set; }

    internal string[] Types { get; set; }

    internal string[] Classifications { get; set; }

    internal JsonLanguageEntry[] Languages { get; set; }
}
