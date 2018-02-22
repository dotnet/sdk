using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BaselineComparer.DifferenceComparison;
using BaselineComparer.TemplateComparison;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BaselineComparer
{
    public class ComparisonComparisonReportCreator
    {
        private static readonly string BaselineOnlyReportFilename = "BaselineOnlyReports.json";
        private static readonly string ComparisonOnlyReportFilename = "ComparisonOnlyReports.json";

        public ComparisonComparisonReportCreator(string baselineReportDir, string comparisonReportDir)
        {
            _baselineReportDir = baselineReportDir;
            _comparisonReportDir = comparisonReportDir;
        }

        private readonly string _baselineReportDir;
        private readonly string _comparisonReportDir;

        private IReadOnlyList<string> _commonReportFiles;
        private IReadOnlyList<string> _baselineOnlyFiles;
        private IReadOnlyList<string> _comparisonOnlyFiles;

        public void CreateComparisons(string comparisonComparisonReportDir)
        {
            if (!Directory.Exists(comparisonComparisonReportDir))
            {
                Directory.CreateDirectory(comparisonComparisonReportDir);
            }

            Console.WriteLine("Generating comparison comparison reports.");
            DetermineReportDispositions();

            CreateCommonFileComparisonReports(comparisonComparisonReportDir);
            CreateBaselineOnlyReportReport(comparisonComparisonReportDir);
            CreateComparisonOnlyReportReport(comparisonComparisonReportDir);
        }

        private void CreateCommonFileComparisonReports(string comparisonComparisonReportDir)
        {
            foreach (string differenceReportFilename in _commonReportFiles)
            {
                string baselineReportPath = Path.Combine(_baselineReportDir, differenceReportFilename);
                CommandBaseline baselineBaseline = CommandBaseline.FromFile(baselineReportPath);
                DirectoryDifference baselineDifference = baselineBaseline.FileResults;

                string comparisonReportPath = Path.Combine(_comparisonReportDir, differenceReportFilename);
                CommandBaseline comparisonBaseline = CommandBaseline.FromFile(comparisonReportPath);
                DirectoryDifference comparisonDifference = comparisonBaseline.FileResults;


                DirectoryDifferenceComparer comparer = new DirectoryDifferenceComparer(baselineDifference, comparisonDifference);
                DirectoryComparisonDifference comparisonComparison = comparer.Compare();
                StructuredDirectoryComparisonDifference structuredDifference = StructuredDirectoryComparisonDifference.FromDirectoryComparisonDifference(comparisonComparison);

                JObject serialized = JObject.FromObject(structuredDifference);
                string comparisonComparisonReportFile = Path.Combine(comparisonComparisonReportDir, differenceReportFilename);
                File.WriteAllText(comparisonComparisonReportFile, serialized.ToString());
            }
        }

        private void CreateBaselineOnlyReportReport(string comparisonComparisonReportDir)
        {
            if (_baselineOnlyFiles.Count == 0)
            {
                return;
            }

            string serialized = JsonConvert.SerializeObject(_baselineOnlyFiles);
            string reportPath = Path.Combine(comparisonComparisonReportDir, BaselineOnlyReportFilename);
            File.WriteAllText(reportPath, serialized);
        }

        private void CreateComparisonOnlyReportReport(string comparisonComparisonReportDir)
        {
            if (_comparisonOnlyFiles.Count == 0)
            {
                return;
            }

            string serialized = JsonConvert.SerializeObject(_comparisonOnlyFiles);
            string reportPath = Path.Combine(comparisonComparisonReportDir, ComparisonOnlyReportFilename);
            File.WriteAllText(reportPath, serialized);
        }

        // Looks at the report files in both directories. Cagegorize them as common vs. just in one or the other dirs.
        private void DetermineReportDispositions()
        {
            HashSet<string> baselineReportFiles = GetReportFileNames(_baselineReportDir);
            HashSet<string> comparisonReportFiles = GetReportFileNames(_comparisonReportDir);

            _commonReportFiles = baselineReportFiles.Intersect(comparisonReportFiles).ToList();
            _baselineOnlyFiles = baselineReportFiles.Except(comparisonReportFiles).ToList();
            _comparisonOnlyFiles = comparisonReportFiles.Except(baselineReportFiles).ToList();
        }

        private HashSet<string> GetReportFileNames(string baseDir)
        {
            return new HashSet<string>(Directory.EnumerateFiles(baseDir, "*.json")
                .Select(x => Path.GetFileName(x))
                .Where(x => !string.Equals(x, BaselineReportCreator.MasterBaselineReportFileName)));
        }
    }
}
