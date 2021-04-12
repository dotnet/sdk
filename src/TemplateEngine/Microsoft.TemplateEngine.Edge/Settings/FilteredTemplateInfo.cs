// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    [Obsolete("Use ITemplateMatchInfo instead")]
    public class FilteredTemplateInfo : IFilteredTemplateInfo
    {
        public FilteredTemplateInfo(ITemplateInfo info, IReadOnlyList<MatchInfo> matchDisposition)
        {
            Info = info;
            MatchDisposition = matchDisposition;
        }

        public ITemplateInfo Info { get; }

        public IReadOnlyList<MatchInfo> MatchDisposition { get; set; }

        public bool IsMatch => MatchDisposition.Count > 0 && !MatchDisposition.Any(x => x.Kind == MatchKind.Mismatch);

        // There is any criteria that is not Mismatch
        // allowing context misses again
        public bool IsPartialMatch => MatchDisposition.Any(x => x.Kind != MatchKind.Mismatch)
            && MatchDisposition.All(x => x.Location != MatchLocation.Context
                                || (x.Location == MatchLocation.Context && x.Kind == MatchKind.Exact));

        // All parameter matches are exact (or there are no parameter matches)
        public bool HasParameterMismatch => MatchDisposition.Any(x => x.Location == MatchLocation.OtherParameter && x.Kind != MatchKind.Exact);

        public bool IsParameterMatch => !HasParameterMismatch && MatchDisposition.Any(x => x.Location == MatchLocation.OtherParameter);

        public bool HasInvalidParameterValue => MatchDisposition.Any(x => x.Location == MatchLocation.OtherParameter && x.Kind == MatchKind.InvalidParameterValue);

        public bool HasAmbiguousParameterMatch => !HasInvalidParameterValue && MatchDisposition.Any(x => x.Location == MatchLocation.OtherParameter && x.Kind == MatchKind.AmbiguousParameterValue);
    }
}
