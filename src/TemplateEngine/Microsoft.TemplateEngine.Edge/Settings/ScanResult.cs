// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class ScanResult
    {
        public static readonly ScanResult Empty = new(Array.Empty<ITemplate>(), Array.Empty<ILocalizationLocator>());

        public ScanResult(IReadOnlyList<ITemplate> templates, IReadOnlyList<ILocalizationLocator> localizations)
        {
            Templates = templates;
            Localizations = localizations;
        }

        public IReadOnlyList<ILocalizationLocator> Localizations { get; }

        public IReadOnlyList<ITemplate> Templates { get; }
    }
}
