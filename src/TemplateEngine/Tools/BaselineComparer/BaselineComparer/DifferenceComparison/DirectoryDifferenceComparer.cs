using System.Collections.Generic;
using System.Linq;
using BaselineComparer.TemplateComparison;

namespace BaselineComparer.DifferenceComparison
{
    public class DirectoryDifferenceComparer
    {
        public DirectoryDifferenceComparer(DirectoryDifference baselineDifference, DirectoryDifference checkDifference)
        {
            BaselineDifference = baselineDifference;
            CheckDifference = checkDifference;
        }

        public DirectoryDifference BaselineDifference { get; }

        public DirectoryDifference CheckDifference { get; }

        public DirectoryComparisonDifference Compare()
        {
            DirectoryComparisonDifference comparisonResult = new DirectoryComparisonDifference();

            if (!BaselineDifference.IsValidBaseline)
            {
                comparisonResult.InvalidBaselineData = true;
                return comparisonResult;
            }

            if (!CheckDifference.IsValidBaseline)
            {
                comparisonResult.InvalidSecondaryData = true;
                return comparisonResult;
            }

            IReadOnlyDictionary<string, FileDifference> checkFileLookup = CheckDifference.FileResults.ToDictionary(x => x.File, x => x);

            foreach (FileDifference baselineFileDiff in BaselineDifference.FileResults)
            {
                if (!checkFileLookup.TryGetValue(baselineFileDiff.File, out FileDifference checkFileDiff))
                {
                    FileComparisonDifference fileResult = new FileComparisonDifference(baselineFileDiff.File);
                    fileResult.MissingSecondaryFile = true;
                    comparisonResult.AddFileResult(fileResult);
                }
                else
                {
                    FileDifferenceComparer comparer = new FileDifferenceComparer(baselineFileDiff, checkFileDiff);
                    FileComparisonDifference fileResult = comparer.Compare();
                    comparisonResult.AddFileResult(fileResult);
                }
            }

            // check for files in the check result but not the baseline.
            HashSet<string> baselineFileLookup = new HashSet<string>(BaselineDifference.FileResults.Select(x => x.File));
            foreach (FileDifference checkFileDifference in CheckDifference.FileResults)
            {
                if (!baselineFileLookup.Contains(checkFileDifference.File))
                {
                    FileComparisonDifference fileResult = new FileComparisonDifference(checkFileDifference.File);
                    fileResult.MissingBaselineFile = true;
                    comparisonResult.AddFileResult(fileResult);
                }
            }

            return comparisonResult;
        }
    }
}
