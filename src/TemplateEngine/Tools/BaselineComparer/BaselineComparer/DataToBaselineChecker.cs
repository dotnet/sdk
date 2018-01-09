using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace BaselineComparer
{
    // Compares a dataset against a baseline.
    // The dataset is compared to the baseline data. Then the differences are checked against the baseline check results.
    //
    public class DataToBaselineChecker
    {
        public DataToBaselineChecker(string baselineFile, string dataToCheckBasePath)
        {
            _baselineFile = baselineFile;
            _dataToCheckBasePath = dataToCheckBasePath;
        }

        private string _baselineFile;
        private string _dataToCheckBasePath;
        private DirectoryDifference _existingBaseline;
        private DirectoryDifference _newDataComparisonResult;  // results of comparing the files from the existing baseline against the dataToCheckBasePath

        private DirectoryComparisonDifference _comparisonResult;

        public DirectoryComparisonDifference ComparisonResult
        {
            get
            {
                EnsureDifferenceComparison();

                return _comparisonResult;
            }
        }

        private void EnsureDifferenceComparison()
        {
            if (_comparisonResult == null)
            {
                EnsureNewDataComparisons();

                Console.WriteLine("Comparing the baseline diff to the new data diff...");
                DirectoryDifferenceComparer comparer = new DirectoryDifferenceComparer(_existingBaseline, _newDataComparisonResult);
                _comparisonResult = comparer.Compare();
            }
        }

        private void EnsureNewDataComparisons()
        {
            if (_newDataComparisonResult == null)
            {
                EnsureBaselineLoaded();

                Console.WriteLine("Comparing the new data to the baseline...");

                DirectoryComparer comparer = new DirectoryComparer(_existingBaseline.BaselineDirectory, _dataToCheckBasePath);
                _newDataComparisonResult = comparer.Compare();
            }
        }

        private void EnsureBaselineLoaded()
        {
            if (_existingBaseline == null)
            {
                Console.WriteLine("Loading the existing baseline...");

                string baselineContent;

                try
                {
                    baselineContent = File.ReadAllText(_baselineFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unable to read the specified baseline file: {_baselineFile}");
                    Console.WriteLine($"Error: {ex.Message}");
                    throw;
                }

                try
                {
                    JObject baselineJObject = JObject.Parse(baselineContent);
                    _existingBaseline = DirectoryDifference.FromJObject(baselineJObject);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to deserialize the existing baseline data.");
                    Console.WriteLine($"Error: {ex.Message}");
                    throw;
                }
            }
        }
    }
}
