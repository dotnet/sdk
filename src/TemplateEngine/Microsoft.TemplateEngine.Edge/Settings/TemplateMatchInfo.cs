// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class TemplateMatchInfo : ITemplateMatchInfo
    {
        public TemplateMatchInfo(ITemplateInfo info, IReadOnlyList<MatchInfo> matchDispositions)
            : this(info)
        {
            if (matchDispositions != null)
            {
                foreach (MatchInfo disposition in matchDispositions)
                {
                    AddDisposition(disposition);
                }
            }
        }

        public TemplateMatchInfo(ITemplateInfo info)
        {
            Info = info;
            _matchDisposition = new List<MatchInfo>();
            _dispositionOfDefaults = new List<MatchInfo>();
        }

        public ITemplateInfo Info { get; }

        public IReadOnlyList<MatchInfo> MatchDisposition
        {
            get
            {
                return _matchDisposition.ToList();
            }
        }
        private IList<MatchInfo> _matchDisposition;

        // Stores match info relative to default settings.
        // These don't have to match for the template to be a match, but they can be used to filter matches
        // in appropriate situations.
        // For example, matching or non-matching on the default language should only be used as a final disambiguator.
        // It shouldn't unconditionally disqualify a match.
        public IReadOnlyList<MatchInfo> DispositionOfDefaults
        {
            get
            {
                return _dispositionOfDefaults.ToList();
            }
        }
        private IList<MatchInfo> _dispositionOfDefaults;

        public void AddDisposition(MatchInfo newDisposition)
        {
            if (newDisposition.Location == MatchLocation.DefaultLanguage)
            {
                _dispositionOfDefaults.Add(newDisposition);
            }
            else
            {
                _matchDisposition.Add(newDisposition);
            }
        }

        public bool IsMatch => MatchDisposition.Count > 0 && !MatchDisposition.Any(x => x.Kind == MatchKind.Mismatch);

        public bool IsPartialMatch => MatchDisposition.Any(x => x.Kind != MatchKind.Mismatch)
                                    && MatchDisposition.All(x => x.Location != MatchLocation.Context
                                        || (x.Location == MatchLocation.Context && x.Kind == MatchKind.Exact));
    }
}
