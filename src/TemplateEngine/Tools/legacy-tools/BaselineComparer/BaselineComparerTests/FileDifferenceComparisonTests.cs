using BaselineComparer.DifferenceComparison;
using BaselineComparer.TemplateComparison;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BaselineComparerTests
{
    public class FileDifferenceComparisonTests
    {
        // Tests that "Close" but unaligned differences are all classified - that none of them get lost during attempted matching
        [Fact(DisplayName = nameof(PartialDifferenceMialignmentTest))]
        public void PartialDifferenceMialignmentTest()
        {
            string baselineDifference = @"
              {
                ""File"": ""Test_C\\Mvc_2.0_Ind_Uld_LS\\Properties\\launchSettings.json"",
                ""Differences"": [
                  {
                    ""BaselineStartPosition"": 165,
                    ""BaselineData"": ""31"",
                    ""SecondaryStartPosition"": 165,
                    ""SecondaryData"": ""64"",
                    ""LocationLeeway"": 2,
                    ""ClassificationString"": ""Integer""
                  },
                  {
                    ""BaselineStartPosition"": 691,
                    ""BaselineData"": ""30"",
                    ""SecondaryStartPosition"": 691,
                    ""SecondaryData"": ""63"",
                    ""LocationLeeway"": 2,
                    ""ClassificationString"": ""Integer""
                  }
                ]
              }
        ";

            string comparisonDifference = @"
            {
              ""File"": ""Test_C\\Mvc_2.0_Ind_Uld_LS\\Properties\\launchSettings.json"",
              ""Differences"": [
                {
                  ""BaselineStartPosition"": 162,
                  ""BaselineData"": ""56131"",
                  ""SecondaryStartPosition"": 162,
                  ""SecondaryData"": ""22963"",
                  ""LocationLeeway"": 2,
                  ""ClassificationString"": ""Integer""
                },
                {
                  ""BaselineStartPosition"": 192,
                  ""BaselineData"": ""84"",
                  ""SecondaryStartPosition"": 192,
                  ""SecondaryData"": ""33"",
                  ""LocationLeeway"": 2,
                  ""ClassificationString"": ""Integer""
                },
                {
                  ""BaselineStartPosition"": 355,
                  ""BaselineData"": ""84"",
                  ""SecondaryStartPosition"": 355,
                  ""SecondaryData"": ""33"",
                  ""LocationLeeway"": 2,
                  ""ClassificationString"": ""Integer""
                },
                {
                  ""BaselineStartPosition"": 688,
                  ""BaselineData"": ""56130"",
                  ""SecondaryStartPosition"": 688,
                  ""SecondaryData"": ""22962"",
                  ""LocationLeeway"": 2,
                  ""ClassificationString"": ""Integer""
                }
              ]
            }
        ";

            JObject baselineJObject = JObject.Parse(baselineDifference);
            JObject comparisonJObject = JObject.Parse(comparisonDifference);

            FileDifference baselineDiff = FileDifference.FromJObject(baselineJObject);
            FileDifference comparisonDiff = FileDifference.FromJObject(comparisonJObject);

            FileDifferenceComparer comparer = new FileDifferenceComparer(baselineDiff, comparisonDiff);
            FileComparisonDifference comparisonResult = comparer.Compare();

            Assert.False(comparisonResult.HasDifferenceResolutionError);
        }
    }
}
