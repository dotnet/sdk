// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    [Obsolete("The implementation became internal")]
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

        public IReadOnlyList<MatchInfo> MatchDisposition => _matchDisposition.ToList();
        private IList<MatchInfo> _matchDisposition;

        // Stores match info relative to default settings.
        // These don't have to match for the template to be a match, but they can be used to filter matches
        // in appropriate situations.
        // For example, matching or non-matching on the default language should only be used as a final disambiguator.
        // It shouldn't unconditionally disqualify a match.
        public IReadOnlyList<MatchInfo> DispositionOfDefaults => _dispositionOfDefaults.ToList();
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

        public bool IsMatch => MatchDisposition.Count > 0 && MatchDisposition.All(x => x.Kind != MatchKind.Mismatch);

        public bool IsPartialMatch => MatchDisposition.Any(x => x.Kind != MatchKind.Mismatch);
    }

    internal class TemplateMatchInfo2 : Abstractions.TemplateFiltering.ITemplateMatchInfo
    {
        private List<Abstractions.TemplateFiltering.MatchInfo> _matchDisposition = new List<Abstractions.TemplateFiltering.MatchInfo>();

        internal TemplateMatchInfo2(ITemplateInfo info, IReadOnlyList<Abstractions.TemplateFiltering.MatchInfo> matchDispositions)
            : this(info)
        {
            if (matchDispositions != null)
            {
                foreach (Abstractions.TemplateFiltering.MatchInfo disposition in matchDispositions)
                {
                    AddMatchDisposition(disposition);
                }
            }
        }

        internal TemplateMatchInfo2(ITemplateInfo info)
        {
            Info = info ?? throw new ArgumentNullException(nameof(info));
        }

        public ITemplateInfo Info { get; }

        public IReadOnlyList<Abstractions.TemplateFiltering.MatchInfo> MatchDisposition => _matchDisposition;

        public void AddMatchDisposition(Abstractions.TemplateFiltering.MatchInfo newDisposition)
        {
            _matchDisposition.Add(newDisposition);
        }
    }
}
