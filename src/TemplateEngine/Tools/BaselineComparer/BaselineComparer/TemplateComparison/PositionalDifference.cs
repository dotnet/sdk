using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BaselineComparer.TemplateComparison
{
    public enum DifferenceDatatype
    {
        Integer,
        Decimal,
        Guid,
        String,  // catchall for now
        TooLong
    };

    public class PositionalDifference
    {
        public PositionalDifference(int baselineStart, string baselineData, int secondaryStart, string secondaryData, DifferenceDatatype classification, int leeway)
        {
            BaselineStartPosition = baselineStart;
            BaselineData = baselineData;
            SecondaryStartPosition = secondaryStart;
            SecondaryData = secondaryData;
            Classification = classification;
            LocationLeeway = leeway;
        }

        public PositionalDifference(int baselineStart, string baselineData, int secondaryStart, string secondaryData)
        {
            BaselineStartPosition = baselineStart;
            BaselineData = baselineData;
            SecondaryStartPosition = secondaryStart;
            SecondaryData = secondaryData;
            Classification = ClassifyDifference(out int leeway);
            LocationLeeway = leeway;
        }

        [JsonProperty]
        public int BaselineStartPosition { get; }

        [JsonProperty]
        public string BaselineData { get; }

        [JsonProperty]
        public int SecondaryStartPosition { get; }

        [JsonProperty]
        public string SecondaryData { get; }

        // Based on the classification of the difference, we may need to allow some leeway on the position of the difference for future comparisons.
        // For example if in the baseline comparison, two generated ints are 65432 and 69999, the diff will start after the 6.
        // But in future comparisons, we may be comparing 65432 and 12345, in which case the diff starts earlier.
        //  So if the diff is of the same classification and only varies by a "few" characters, it's probably ok.
        [JsonProperty]
        public int LocationLeeway { get; }

        [JsonIgnore]
        public DifferenceDatatype Classification { get; }

        [JsonProperty]
        public string ClassificationString => Classification.ToString();

        // TODO (future enhancement): semantic version diff checking. Can use TemplateEngine.Utils.SemanticVersion.cs for parsing
        //      just need to be able to "walk around" the actual diff to get the full version string so it parses correctly.
        // TODO (future enhancement): detect other "standard" data types.
        private DifferenceDatatype ClassifyDifference(out int leeway)
        {
            if (IsIntegerDifference(out leeway))
            {
                return DifferenceDatatype.Integer;
            }

            if (IsDecimalDifference(out leeway))
            {
                return DifferenceDatatype.Decimal;
            }

            if (IsGuidDifference(out leeway))
            {
                return DifferenceDatatype.Guid;
            }

            leeway = 0; // not sure what's best here
            return DifferenceDatatype.String;
        }

        private bool IsDecimalDifference(out int leeway)
        {
            if (Double.TryParse(BaselineData, out double _) && Double.TryParse(SecondaryData, out double _))
            {
                leeway = Math.Abs(BaselineData.Length - SecondaryData.Length) + 2;
                return true;
            }

            leeway = 0;
            return false;
        }

        private bool IsIntegerDifference(out int leeway)
        {
            if (Int32.TryParse(BaselineData, out int _) && Int32.TryParse(SecondaryData, out int _))
            {
                leeway = Math.Abs(BaselineData.Length - SecondaryData.Length) + 2;
                return true;
            }

            leeway = 0;
            return false;
        }

        private bool IsGuidDifference(out int recommendedLeeway)
        {
            if (IsAlmostGuid(BaselineData, out int baselinePadding) && IsAlmostGuid(SecondaryData, out int secondaryPadding))
            {
                recommendedLeeway = 4;  // this would be the total number of characters at the start & end of the compared guids that are identical. More than 4 is very unlikely.
                return true;
            }

            recommendedLeeway = 0;
            return false;
        }

        // If 2 different guids have the same lead / trailing char(s) as each other, the diff checker won't pick up those characters because
        // they aren't internal to the difference.
        private bool IsAlmostGuid(string toCheck, out int paddingLength)
        {
            // allow for a few missing or extra chars, plus possible hyphens, plus possible braces or parens.
            if (toCheck.Length > 38 || toCheck.Length < 28)
            {
                paddingLength = 0;
                return false;
            }

            if (toCheck[0] == '{' || toCheck[0] == '(')
            {
                toCheck = toCheck.Substring(1);
            }

            int lastIndex = toCheck.Length - 1;
            if (toCheck[lastIndex] == '}' || toCheck[lastIndex] == ')')
            {
                toCheck = toCheck.Substring(0, lastIndex);
            }

            toCheck = toCheck.Replace("-", "");

            if (toCheck.Length > 32)
            {
                paddingLength = 0;
                return false;
            }

            if (toCheck.Length < 32)
            {
                paddingLength = 32 - toCheck.Length;
                toCheck = toCheck + new string('0', paddingLength);   // pad to 32 chars
            }
            else
            {
                paddingLength = 0;
            }

            return Guid.TryParse(toCheck, out Guid _);
        }

        public void ConsoleDebug()
        {
            Console.WriteLine($"Baseline start: {BaselineStartPosition} | {BaselineData}");
            Console.WriteLine($"Target start: {SecondaryStartPosition} | {SecondaryData}");
            Console.WriteLine($"\tDatatype: {Classification.ToString()}");
            Console.WriteLine($"\tLeeway: {LocationLeeway}");
        }

        public static PositionalDifference FromJObject(JObject source)
        {
            int baselineStart = source.GetValue(nameof(BaselineStartPosition)).ToObject<int>();
            string baselineData = source.GetValue(nameof(BaselineData)).ToString();
            int secondaryStart = source.GetValue(nameof(SecondaryStartPosition)).ToObject<int>();
            string secondaryData = source.GetValue(nameof(SecondaryData)).ToString();

            int leeway = source.GetValue(nameof(LocationLeeway)).ToObject<int>();
            string classificationString = source.GetValue(nameof(ClassificationString)).ToString();
            DifferenceDatatype classification = Enum.Parse<DifferenceDatatype>(classificationString);

            PositionalDifference difference = new PositionalDifference(baselineStart, baselineData, secondaryStart, secondaryData, classification, leeway);

            return difference;
        }
    }
}
