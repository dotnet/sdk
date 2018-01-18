using System.Collections.Generic;
using System.Linq;
using BaselineComparer.TemplateComparison;

namespace BaselineComparer.DifferenceComparison
{
    public class FileComparisonDifference
    {
        public FileComparisonDifference(string filename)
        {
            Filename = filename;
            _positionallyMatchedDifferences = new List<PositionalComparisonDifference>();
            _baselineOnlyDifferences = new List<PositionalDifference>();
            _checkOnlyDifferences = new List<PositionalDifference>();
        }

        public string Filename { get; }

        private List<PositionalComparisonDifference> _positionallyMatchedDifferences;
        private List<PositionalDifference> _baselineOnlyDifferences;
        private List<PositionalDifference> _checkOnlyDifferences;
        private bool _missingBaselineFile;
        private bool _missingSecondaryFile;

        // Stores the differences that positionally match up between two sets of comparisons.
        // They could still have a datatype or length mismatch.
        // The key is the baseline difference, the value is the difference for the current comparison.
        public IReadOnlyList<PositionalComparisonDifference> PositionallyMatchedDifferences => _positionallyMatchedDifferences;

        // Differences from the baseline comparison that couldn't be matched to differences from the check comparison.
        public IReadOnlyList<PositionalDifference> BaselineOnlyDifferences => _baselineOnlyDifferences;

        // Differences from the check comparison that couldn't be matched to differences from the baseline comparison.
        public IReadOnlyList<PositionalDifference> SecondaryOnlyDifferences => _checkOnlyDifferences;

        public void AddPositionallyMatchedDifference(PositionalDifference baselineDifference, PositionalDifference checkDifference, PositionalComparisonDisposition disposition)
        {
            _positionallyMatchedDifferences.Add(new PositionalComparisonDifference(baselineDifference, checkDifference, disposition));
        }

        public void AddBaselineOnlyDifference(PositionalDifference baselineDifference)
        {
            if (MissingBaselineFile || MissingSecondaryFile)
            {
                throw new System.Exception("Cant have differences if a file is missing.");
            }

            _baselineOnlyDifferences.Add(baselineDifference);
        }

        public void AddCheckOnlyDifference(PositionalDifference checkDifference)
        {
            if (MissingBaselineFile || MissingSecondaryFile)
            {
                throw new System.Exception("Cant have differences if a file is missing.");
            }

            _checkOnlyDifferences.Add(checkDifference);
        }

        // externally set in situations where the number of classified differences don't exactly match the number of original differences.
        public bool HasDifferenceResolutionError { get; set; }

        public bool AnyInvalidDifferences
        {
            get
            {
                return BaselineOnlyDifferences.Count > 0
                    || SecondaryOnlyDifferences.Count > 0
                    || MissingBaselineFile
                    || MissingSecondaryFile
                    || PositionallyMatchedDifferences.Any(d => d.Disposition != PositionalComparisonDisposition.Match);
            }
        }

        public bool MissingBaselineFile
        {
            get
            {
                return _missingBaselineFile;
            }
            set
            {
                if (_positionallyMatchedDifferences.Count > 0
                    || _baselineOnlyDifferences.Count > 0
                    || _checkOnlyDifferences.Count > 0)
                {
                    throw new System.Exception("Cant label baseline file as missing - there are registered differences.");
                }

                _missingBaselineFile = value;
            }
        }

        public bool MissingSecondaryFile
        {
            get
            {
                return _missingSecondaryFile;
            }
            set
            {
                if (_positionallyMatchedDifferences.Count > 0
                    || _baselineOnlyDifferences.Count > 0
                    || _checkOnlyDifferences.Count > 0)
                {
                    throw new System.Exception("Cant label secondary file as missing - there are registered differences.");
                }

                _missingSecondaryFile = value;
            }
        }
    }
}
