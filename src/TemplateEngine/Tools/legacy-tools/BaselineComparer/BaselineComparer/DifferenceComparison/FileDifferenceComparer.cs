using System;
using BaselineComparer.TemplateComparison;

namespace BaselineComparer.DifferenceComparison
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

                if (Math.Abs(baselineDiff.BaselineStartPosition - checkDiff.BaselineStartPosition) > baselineDiff.LocationLeeway)
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
                    // the differences are close enough to compare to each other.
                    if (baselineDiff.Classification != checkDiff.Classification)
                    {
                        // The classifications are different, it's a non-match
                        fileResult.AddPositionallyMatchedDifference(baselineDiff, checkDiff, PositionalComparisonDisposition.DatatypeMismatch);
                    }
                    else if (Math.Abs(baselineDiff.SecondaryData.Length - checkDiff.SecondaryData.Length) > baselineDiff.LocationLeeway)
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

            // verify the results are sane - that all differences got classified.
            int classifiedDiffCount = fileResult.BaselineOnlyDifferences.Count + fileResult.SecondaryOnlyDifferences.Count
                                        + 2 * fileResult.PositionallyMatchedDifferences.Count; // these are counted twice because they consume both a baseline & secondary diff
            int actualDiffCount = BaselineComparisonDifferences.Differences.Count + CheckComparisonDifferences.Differences.Count;

            if (classifiedDiffCount != actualDiffCount)
            {
                fileResult.HasDifferenceResolutionError = true;
            }

            return fileResult;
        }
    }
}
