using Newtonsoft.Json;

namespace BaselineComparer.DifferenceComparison
{
    public class StructuredPositionalComparisonDifference
    {
        [JsonProperty]
        public string MatchDisposition { get; set; }

        [JsonProperty]
        public int BaselineMasterStartPosition { get; set; }

        [JsonProperty]
        public string BaselineMasterData { get; set; }

        [JsonProperty]
        public string BaselineClassification { get; set; }

        public bool ShouldSerializeBaselineClassification()
        {
            return !AreClassificationsTheSame;
        }

        [JsonProperty]
        public int BaselineSecondaryStartPosition { get; set; }

        [JsonProperty]
        public string BaselineSecondaryData { get; set; }

        [JsonProperty]
        public int ComparisonStartPosition { get; set; }

        [JsonProperty]
        public string ComparisonData { get; set; }

        [JsonProperty]
        public string ComparisonDataClassification { get; set; }

        public bool ShouldSerializeComparisonDataClassification()
        {
            return !AreClassificationsTheSame;
        }

        [JsonProperty]
        public string Classification
        {
            get
            {
                if (AreClassificationsTheSame)
                {
                    return BaselineClassification;
                }

                return null;
            }
        }

        public bool ShouldSerializeClassification()
        {
            return AreClassificationsTheSame;
        }

        private bool AreClassificationsTheSame
        {
            get
            {
                return string.Equals(BaselineClassification, ComparisonDataClassification);
            }
        }

        public static StructuredPositionalComparisonDifference FromPositionalComparisonDifference(PositionalComparisonDifference difference)
        {
            StructuredPositionalComparisonDifference structuredDifference = new StructuredPositionalComparisonDifference()
            {
                MatchDisposition = difference.Disposition.ToString(),
                BaselineMasterStartPosition = difference.BaselineDifference.BaselineStartPosition,
                BaselineMasterData = difference.BaselineDifference.BaselineData,
                BaselineClassification = difference.BaselineDifference.ClassificationString,
                BaselineSecondaryStartPosition = difference.BaselineDifference.SecondaryStartPosition,
                BaselineSecondaryData = difference.BaselineDifference.SecondaryData,
                ComparisonStartPosition = difference.CheckDifference.SecondaryStartPosition,
                ComparisonData = difference.CheckDifference.SecondaryData,
                ComparisonDataClassification = difference.CheckDifference.ClassificationString
            };

            return structuredDifference;
        }
    }
}
