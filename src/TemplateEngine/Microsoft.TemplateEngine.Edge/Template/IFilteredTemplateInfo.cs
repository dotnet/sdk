using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge.Template
{
    [Obsolete("Use ITemplateMatchInfo instead")]
    public interface IFilteredTemplateInfo
    {
        ITemplateInfo Info { get; }

        IReadOnlyList<MatchInfo> MatchDisposition { get; }

        bool IsMatch { get; }

        bool IsPartialMatch { get; }

        bool HasParameterMismatch { get; }

        bool IsParameterMatch { get; }

        bool HasInvalidParameterValue { get; }

        bool HasAmbiguousParameterMatch { get; }
    }
}
