using System.Collections.Generic;
using System.Linq;
using BaselineComparer.TemplateComparison;
using Newtonsoft.Json;

namespace BaselineComparer.DifferenceComparison
{
    public class StructuredFileComparisonDifference
    {
        [JsonProperty]
        public string Filename { get; set; }

        [JsonProperty]
        public bool HasDifferenceResolutionError { get; set; }

        public bool ShouldSerializeHasDifferenceResolutionError()
        {
            return HasDifferenceResolutionError;
        }

        [JsonProperty]
        public bool HasInvalidDifferences { get; set; }

        public bool ShouldSerializeHasInvalidDifferences()
        {
            return HasInvalidDifferences;
        }

        [JsonProperty]
        public IReadOnlyList<PositionalDifference> BaselineOnlyDifferences { get; set; }

        public bool ShouldSerializeBaselineOnlyDifferences()
        {
            return BaselineOnlyDifferences.Count > 0;
        }

        [JsonProperty]
        public IReadOnlyList<PositionalDifference> SecondaryOnlyDifferences { get; set; }

        public bool ShouldSerializeSecondaryOnlyDifferences()
        {
            return SecondaryOnlyDifferences.Count > 0;
        }

        [JsonProperty]
        public IReadOnlyList<StructuredPositionalComparisonDifference> FullyMatchedDifferences { get; set; }

        public bool ShouldSerializeFullyMatchedDifferences()
        {
            return FullyMatchedDifferences.Count > 0;
        }

        [JsonProperty]
        public IReadOnlyList<StructuredPositionalComparisonDifference> PositionallyMatchedDifferencesWithIssues { get; set; }

        public bool ShouldSerializePositionallyMatchedDifferencesWithIssues()
        {
            return PositionallyMatchedDifferencesWithIssues.Count > 0;
        }

        public static StructuredFileComparisonDifference FromFileComparisonDifference(FileComparisonDifference differences)
        {
            StructuredFileComparisonDifference structuredDifference = new StructuredFileComparisonDifference()
            {
                Filename = differences.Filename,
                HasDifferenceResolutionError = differences.HasDifferenceResolutionError,
                HasInvalidDifferences = differences.AnyInvalidDifferences,
                BaselineOnlyDifferences = differences.BaselineOnlyDifferences,
                SecondaryOnlyDifferences = differences.SecondaryOnlyDifferences,
                //PositionallyMatchedDifferences = differences.PositionallyMatchedDifferences.Select(d => StructuredPositionalComparisonDifference.FromPositionalComparisonDifference(d)).ToList()
                FullyMatchedDifferences = differences.PositionallyMatchedDifferences.Where(d => d.Disposition == PositionalComparisonDisposition.Match)
                                                                                    .Select(m => StructuredPositionalComparisonDifference.FromPositionalComparisonDifference(m)).ToList(),
                PositionallyMatchedDifferencesWithIssues = differences.PositionallyMatchedDifferences.Where(d => d.Disposition != PositionalComparisonDisposition.Match)
                                                                                    .Select(m => StructuredPositionalComparisonDifference.FromPositionalComparisonDifference(m)).ToList(),

            };

            return structuredDifference;
        }
    }
}
