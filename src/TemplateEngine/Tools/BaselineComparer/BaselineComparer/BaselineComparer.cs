using System;
using System.IO;
using BaselineComparer.DifferenceComparison;
using BaselineComparer.Helpers;
using BaselineComparer.TemplateComparison;
using Newtonsoft.Json.Linq;

namespace BaselineComparer
{
    public class BaselineComparer
    {
        public BaselineComparer(string baselineFile, string newDataBaseDir)
        {
            _baselineFile = baselineFile;
            _newDataBaseDir = newDataBaseDir;
        }

        private string _baselineFile;
        private string _newDataBaseDir;
        private Baseline _baseline;
        private DataToBaselineChecker _comparisonComparer;

        public void WriteStructuredComparisonDifferenceReport(string reportFileName)
        {
            StructuredDirectoryComparisonDifference structuredDifference = StructuredDirectoryComparisonDifference.FromDirectoryComparisonDifference(_comparisonComparer.DifferenceComparisonResult);
            JObject serialized = JObject.FromObject(structuredDifference);
            File.WriteAllText(reportFileName, serialized.ToString());
        }

        // Write the report that compares the original baseline comparison against
        // the new-data-to-baseline report.
        public void WriteFreeFormComparisonDifferenceReport(string reportFilePath)
        {
            ComparisonDifferenceReportCreator reportCreator = new ComparisonDifferenceReportCreator(DifferenceComparisonResult);

            string reportDirectory = Path.GetDirectoryName(reportFilePath);
            if (!Directory.Exists(reportDirectory))
            {
                Directory.CreateDirectory(reportDirectory);
            }

            File.WriteAllText(reportFilePath, reportCreator.ReportText);

            Console.WriteLine("...wrote the comparison difference report.");
        }

        // Writes the report that compares the newly generated templates against the baseline master templates.
        // (this is the intermediate result, which is used for the comparison difference report).
        public void WriteTemplateComparisonReport(string reportFilePath)
        {
            string reportDirectory = Path.GetDirectoryName(reportFilePath);
            if (!Directory.Exists(reportDirectory))
            {
                Directory.CreateDirectory(reportDirectory);
            }

            JObject serializedComparison = JObject.FromObject(NewDataComparisonResult);
            File.WriteAllText(reportFilePath, serializedComparison.ToString());

            Console.WriteLine("...wrote the secondary template comparison report.");
        }

        public DirectoryComparisonDifference DifferenceComparisonResult
        {
            get
            {
                EnsureComparison();

                return _comparisonComparer.DifferenceComparisonResult;
            }
        }

        public DirectoryDifference NewDataComparisonResult
        {
            get
            {
                EnsureComparison();

                return _comparisonComparer.NewDataComparisonResult;
            }
        }

        private void EnsureComparison()
        {
            if (_baseline == null)
            {
                _baseline = Baseline.FromFile(_baselineFile);

                Console.WriteLine("Generating new comparison data.");
                TemplateCommandRunner.RunTemplateCommands(_baseline.NewCommand, _newDataBaseDir, _baseline.TemplateCommands);

                _comparisonComparer = new DataToBaselineChecker(_baseline, _newDataBaseDir);
            }
        }
    }
}
