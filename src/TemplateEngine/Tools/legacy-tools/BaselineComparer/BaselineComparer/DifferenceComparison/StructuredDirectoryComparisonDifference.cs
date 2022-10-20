using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace BaselineComparer.DifferenceComparison
{
    public class StructuredDirectoryComparisonDifference
    {
        [JsonProperty]
        public bool HasProblems
        {
            get
            {
                return FileResults.Any(x => x.HasDifferenceResolutionError || x.HasInvalidDifferences);
            }
        }

        [JsonProperty]
        public bool InvalidBaselineData { get; set; }

        public bool ShouldSerializeInvalidBaselineData()
        {
            return InvalidBaselineData;
        }

        [JsonProperty]
        public bool InvalidSecondaryData { get; set; }

        public bool ShouldSerializeInvalidSecondaryData()
        {
            return InvalidSecondaryData;
        }

        [JsonProperty]
        public IReadOnlyList<StructuredFileComparisonDifference> FileResults { get; set; }

        public static StructuredDirectoryComparisonDifference FromDirectoryComparisonDifference(DirectoryComparisonDifference difference)
        {
            StructuredDirectoryComparisonDifference structuredDifference = new StructuredDirectoryComparisonDifference()
            {
                InvalidBaselineData = difference.InvalidBaselineData,
                InvalidSecondaryData = difference.InvalidSecondaryData,
                FileResults = difference.FileResults.Select(x => StructuredFileComparisonDifference.FromFileComparisonDifference(x)).ToList()
            };

            return structuredDifference;
        }
    }
}
