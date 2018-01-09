using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace BaselineComparer
{
    public class BaselineCreator
    {
        // When establishing a baseline, neither directory is particularly "preferred". But one is designated as the baseline.
        // The same baseline data must be used for future comparisons, because the differences in the comparison results is the real check.
        public BaselineCreator(string baselineDir, string comparisonDir)
        {
            BaselineDir = baselineDir;
            ComparisonDir = comparisonDir;
        }

        public string BaselineDir { get; }
        public string ComparisonDir { get; }

        private DirectoryDifference _comparisonResult;

        public void WriteBaseline(string baselineDataFilename)
        {
            EnsureComparisons();

            JObject serialized = JObject.FromObject(_comparisonResult);
            File.WriteAllText(baselineDataFilename, serialized.ToString());
        }

        public DirectoryDifference ComparisonResult
        {
            get
            {
                EnsureComparisons();

                return _comparisonResult;
            }
        }

        private void EnsureComparisons()
        {
            if (_comparisonResult == null)
            {
                DirectoryComparer comparer = new DirectoryComparer(BaselineDir, ComparisonDir);
                _comparisonResult = comparer.Compare();
            }
        }

        public IReadOnlyList<string> MissingBaselineFiles
        {
            get
            {
                EnsureComparisons();

                return _comparisonResult.MissingBaselineFiles;
            }
        }

        public IReadOnlyList<string> MissingCheckFiles
        {
            get
            {
                EnsureComparisons();

                return _comparisonResult.MissingCheckFiles;
            }
        }
    }
}
