using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge.Template
{
    public interface IFilteredTemplateInfo
    {
        ITemplateInfo Info { get; }

        IReadOnlyList<MatchInfo> MatchDisposition { get; }

        IReadOnlyList<MatchInfo> DispositionOfDefaults { get; }

        void AddDisposition(MatchInfo newDisposition);

        bool HasMatchDisposition(MatchLocation location, MatchKind kind);

        bool IsMatch { get; }

        bool IsMatchExceptContext { get; }

        bool IsPartialMatch { get; }

        bool IsPartialMatchExceptContext { get; }

        bool HasNameMismatch { get; }

        bool HasParameterMismatch { get; }

        bool IsInvokableMatch { get; }

        bool HasAmbiguousParameterValueMatch { get; }

        IReadOnlyList<string> InvalidParameterNames { get; }

        bool HasParseError { get; }

        string ParseError { get; }

        // This is analogous to INewCommandInput.InputTemplateParams
        IReadOnlyDictionary<string, string> ValidTemplateParameters { get; }
    }
}
