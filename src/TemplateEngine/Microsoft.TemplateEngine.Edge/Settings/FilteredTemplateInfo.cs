// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class FilteredTemplateInfo : IFilteredTemplateInfo
    {
        public FilteredTemplateInfo(ITemplateInfo info, IReadOnlyList<MatchInfo> matchDisposition)
        {
            Info = info;
            if (matchDisposition != null)
            {
                _matchDisposition = matchDisposition.ToList();
            }
            else
            {
                _matchDisposition = new List<MatchInfo>();
            }

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
            _matchDisposition.Add(newDisposition);
        }

        public void AddDefaultDisposition(MatchInfo newDisposition)
        {
            _dispositionOfDefaults.Add(newDisposition);
        }

        // TODO: get rid of, or rename this. "IsMatch" has become a misnomer as the system has evolved.
        public bool IsMatch => MatchDisposition.Count > 0 && !MatchDisposition.Any(x => x.Kind == MatchKind.Mismatch);

        public bool IsPartialMatch => MatchDisposition.Any(x => x.Kind != MatchKind.Mismatch)
            && MatchDisposition.All(x => x.Location != MatchLocation.Context
                                || (x.Location == MatchLocation.Context && x.Kind == MatchKind.Exact));

        // There is a parameter with a match kind other than: exact or ambiguous
        public bool HasParameterMismatch => MatchDisposition.Any(x => x.Location == MatchLocation.OtherParameter
                                                                && (x.Kind != MatchKind.Exact && x.Kind != MatchKind.AmbiguousParameterValue));

        // For choice parameters - when the value provided is the start of multiple choice values.
        public bool HasAmbiguousParameterValueMatch => MatchDisposition.Any(x => x.Location == MatchLocation.OtherParameter && x.Kind == MatchKind.AmbiguousParameterValue);

        public bool IsInvokableMatch => MatchDisposition.Count > 0
                                    && MatchDisposition.All(x =>
                                        x.Kind == MatchKind.Exact
                                        ||
                                        (   // these locations can have partial or exact matches.
                                            x.Kind == MatchKind.Partial
                                            && (x.Location == MatchLocation.Name || x.Location == MatchLocation.ShortName || x.Location == MatchLocation.Classification)
                                        )
                                    );

        // a list of the invalid parameter names for the template, if any.
        // analogous to INewCommandInput.RemainingArguments
        public IReadOnlyList<string> InvalidParameterNames => MatchDisposition.Where(x => x.Kind == MatchKind.InvalidParameterName)
                                                                            .Select(x => x.ChoiceIfLocationIsOtherChoice).ToList();

        public bool HasParseError => MatchDisposition.Any(x => x.Kind == MatchKind.Unspecified);

        public string ParseError => MatchDisposition.Where(x => x.Kind == MatchKind.Unspecified).Select(x => x.AdditionalInformation).FirstOrDefault();

        public IReadOnlyDictionary<string, string> ValidTemplateParameters
                    => MatchDisposition.Where(x => x.Location == MatchLocation.OtherParameter && x.Kind == MatchKind.Exact)
                        .ToDictionary(x => x.ChoiceIfLocationIsOtherChoice, x => x.ParameterValue);
    }
}
