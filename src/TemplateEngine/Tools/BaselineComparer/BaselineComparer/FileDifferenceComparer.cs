using System;

namespace BaselineComparer
{
    public class FileDifferenceComparer
    {
        public FileDifferenceComparer(FileDifference baselineComparisonDifferences, FileDifference checkComparisonDifferences)
        {
            BaselineComparisonDifferences = baselineComparisonDifferences;
            CheckComparisonDifferences = checkComparisonDifferences;
        }

        public FileDifference BaselineComparisonDifferences { get; }

        public FileDifference CheckComparisonDifferences { get; }

        public FileComparisonDifference Compare()
        {
            int baselineDiffIndex = 0;
            int checkDiffIndex = 0;
            FileComparisonDifference fileResult = new FileComparisonDifference(BaselineComparisonDifferences.File);

            while (baselineDiffIndex < BaselineComparisonDifferences.Differences.Count && checkDiffIndex < CheckComparisonDifferences.Differences.Count)
            {
                PositionalDifference baselineDiff = BaselineComparisonDifferences.Differences[baselineDiffIndex];
                PositionalDifference checkDiff = CheckComparisonDifferences.Differences[checkDiffIndex];

                if (baselineDiff.BaselineStartPosition - checkDiff.BaselineStartPosition > baselineDiff.LocationLeeway)
                {
                    // The differences are too far apart in absolute baseline position, it's a non-match. Consume the first one.
                    // Once one of these occurs, the overall match has failed, and the rest will probably be bad too.
                    if (baselineDiff.BaselineStartPosition < checkDiff.BaselineStartPosition)
                    {
                        fileResult.AddBaselineOnlyDifference(baselineDiff);
                        ++baselineDiffIndex;
                    }
                    else
                    {
                        fileResult.AddCheckOnlyDifference(checkDiff);
                        ++checkDiffIndex;
                    }
                }
                else
                {
                    if (baselineDiff.Classification != checkDiff.Classification)
                    {
                        // The classifications are different, it's a non-match
                        fileResult.AddPositionallyMatchedDifference(baselineDiff, checkDiff, PositionalComparisonDisposition.DatatypeMismatch);
                    }
                    else if (Math.Abs(baselineDiff.TargetData.Length - checkDiff.TargetData.Length) > baselineDiff.LocationLeeway)
                    {
                        // The check data length is more than the allowed difference in length.
                        fileResult.AddPositionallyMatchedDifference(baselineDiff, checkDiff, PositionalComparisonDisposition.LengthMismatch);
                    }
                    else
                    {
                        fileResult.AddPositionallyMatchedDifference(baselineDiff, checkDiff, PositionalComparisonDisposition.Match);
                    }

                    // The baseline & check differences are positionally similar in the above cases, both get consumed.
                    ++baselineDiffIndex;
                    ++checkDiffIndex;
                }
            }

            // One of the comparisons may have additional unhandled differences, can't possibly be both.
            // So at most one of these loops can actually iterate.
            while (baselineDiffIndex < BaselineComparisonDifferences.Differences.Count)
            {
                PositionalDifference baselineDiff = BaselineComparisonDifferences.Differences[baselineDiffIndex];
                fileResult.AddBaselineOnlyDifference(baselineDiff);
                ++baselineDiffIndex;
            }

            while (checkDiffIndex < CheckComparisonDifferences.Differences.Count)
            {
                PositionalDifference checkDiff = CheckComparisonDifferences.Differences[checkDiffIndex];
                fileResult.AddCheckOnlyDifference(checkDiff);
                ++checkDiffIndex;
            }

            return fileResult;
        }
    }
}
