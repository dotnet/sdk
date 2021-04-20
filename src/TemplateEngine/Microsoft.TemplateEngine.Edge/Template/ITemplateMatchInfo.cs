// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge.Template
{
    // Replacement for IFilteredTemplateInfo
    [Obsolete("moved to " + nameof(Abstractions.TemplateFiltering) + " namespace")]
    public interface ITemplateMatchInfo
    {
        ITemplateInfo Info { get; }

        IReadOnlyList<MatchInfo> MatchDisposition { get; }

        IReadOnlyList<MatchInfo> DispositionOfDefaults { get; }

        void AddDisposition(MatchInfo newDisposition);

        bool IsMatch { get; }

        bool IsPartialMatch { get; }
    }
}
