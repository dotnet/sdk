using System;
using BaselineComparer.DifferenceComparison;
using BaselineComparer.TemplateComparison;

namespace BaselineComparer
{
    // Compares a dataset against a baseline.
    // The dataset is compared to the baseline data. Then the differences are checked against the baseline check results.
    public class DataToBaselineChecker
    {
        public DataToBaselineChecker(Baseline baseline, string dataToCheckBasePath)
        {
            _baseline = baseline;
            _dataToCheckBasePath = dataToCheckBasePath;
        }

        private Baseline _baseline;
        private string _dataToCheckBasePath;
        private DirectoryComparisonDifference _differenceComparisonResult;
        private DirectoryDifference _newDataComparisonResult;

        // result of comparing the original baseline comparison against the comparison of the baseline to the new data.
        public DirectoryComparisonDifference DifferenceComparisonResult
        {
            get
            {
                EnsureDifferenceComparison();

                return _differenceComparisonResult;
            }
        }

        // result of comparing the files from the existing baseline against the dataToCheckBasePath
        public DirectoryDifference NewDataComparisonResult
        {
            get
            {
                EnsureDifferenceComparison();

                return _newDataComparisonResult;
            }
        }

        private void EnsureDifferenceComparison()
        {
            if (_differenceComparisonResult == null)
            {
                EnsureNewDataComparisons();

                Console.WriteLine("Comparing the baseline diff to the new data diff...");
                DirectoryDifferenceComparer comparer = new DirectoryDifferenceComparer(_baseline.FileResults, _newDataComparisonResult);

                _differenceComparisonResult = comparer.Compare();
            }
        }

        private void EnsureNewDataComparisons()
        {
            if (_newDataComparisonResult == null)
            {
                Console.WriteLine("Comparing the new data to the baseline...");

                DirectoryComparer comparer = new DirectoryComparer(_baseline.BaselineDirectory, _dataToCheckBasePath);
                _newDataComparisonResult = comparer.Compare();
            }
        }
    }
}
