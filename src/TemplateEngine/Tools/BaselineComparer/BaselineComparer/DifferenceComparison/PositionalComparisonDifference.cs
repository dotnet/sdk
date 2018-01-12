using BaselineComparer.TemplateComparison;

namespace BaselineComparer.DifferenceComparison
{
    public enum PositionalComparisonDisposition
    {
        Match,
        DatatypeMismatch,
        LengthMismatch
    };

    public class PositionalComparisonDifference
    {
        public PositionalComparisonDifference(PositionalDifference baselineDifference, PositionalDifference checkDifference, PositionalComparisonDisposition disposition)
        {
            BaselineDifference = baselineDifference;
            CheckDifference = checkDifference;
            Disposition = disposition;
        }

        public PositionalDifference BaselineDifference { get; }

        public PositionalDifference CheckDifference { get; }

        public PositionalComparisonDisposition Disposition { get; }
    }
}
