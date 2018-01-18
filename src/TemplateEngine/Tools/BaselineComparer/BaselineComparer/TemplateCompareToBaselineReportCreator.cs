using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BaselineComparer.Helpers;

namespace BaselineComparer
{
    // based on an existing baseline, creates one set of templates, and baseline-like reports for the new templates vs. the baseline master data.
    public class TemplateCompareToBaselineReportCreator
    {
        private static readonly string ComparisonDataDirName = "Comparison";
        private static readonly string ComparisonReportDirName = "ComparisonReports";

        // the setup of all the baseline related paths are assuming the baseline gets copied into the comparison root.
        // This happens in CopyExistingBaseline() via CreateBaselineComparison()
        public TemplateCompareToBaselineReportCreator(string baselineRoot, string comparisonRoot)
        {
            _baselineRoot = baselineRoot;
            _comparisonRoot = comparisonRoot;

            string dataDir = Path.Combine(_comparisonRoot, BaselineCreator.BaseDataDir);
            _masterDataBasePath = Path.Combine(dataDir, BaselineCreator.BaselineMasterDataDirName);

            _baselineReportDir = Path.Combine(_comparisonRoot, BaselineCreator.BaseReportDir);

            _comparisonDataDir = Path.Combine(dataDir, ComparisonDataDirName);
            _comparisonReportDir = Path.Combine(_comparisonRoot, ComparisonReportDirName);
        }

        private readonly string _baselineRoot;
        private readonly string _comparisonRoot;
        private readonly string _masterDataBasePath;
        private readonly string _baselineReportDir;

        public string BaselineReportDir => _baselineReportDir;

        private readonly string _comparisonDataDir;
        private readonly string _comparisonReportDir;

        public string ComparisonReportDir => _comparisonReportDir;

        private BaselineMasterReport _masterReport;
        private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _unitNameAndCommandToDataPathMap;
        private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _unitNameAndCommandToReportFileMap;

        public void CreateBaselineComparison(bool debugCompareOnly)
        {
            if (!debugCompareOnly)
            {
                CopyExistingBaseline();
            }

            ReadBaselineMasterReport();
            AssignRelativePathsAndReportNamesToCommandsFromMasterReport();

            if (!debugCompareOnly)
            {
                if (!TryCreateTemplates())
                {
                    throw new Exception("There were problems creating the templates to compare.");
                }
            }

            IReadOnlyList<InvocationUnit> invocationUnits = _masterReport.Invocations.Select(x => InvocationUnit.FromInvocationBaselineUnit(x)).ToList();
            BaselineReportCreator reportCreator = new BaselineReportCreator(_masterDataBasePath, _comparisonDataDir, invocationUnits, _unitNameAndCommandToDataPathMap, _unitNameAndCommandToReportFileMap);
            reportCreator.WriteAllBaselineComparisons(_comparisonReportDir, _masterReport.NewCommand);
        }

        private bool TryCreateTemplates()
        {
            bool allCreationsGood = true;

            foreach (InvocationBaselineUnit unit in _masterReport.Invocations)
            {
                CustomHiveCoordinator hive = new CustomHiveCoordinator(_masterReport.NewCommand, unit.InstallRequirements);
                hive.InitializeHive();

                IReadOnlyDictionary<string, string> commandToPathMap = _unitNameAndCommandToDataPathMap[unit.Name];
                allCreationsGood &= hive.TryCreateTemplateData(_comparisonDataDir, commandToPathMap);

                hive.DeleteHive();
            }

            return allCreationsGood;
        }

        private void ReadBaselineMasterReport()
        {
            string baselineMasterReportFilename = Path.Combine(_baselineReportDir, BaselineReportCreator.MasterBaselineReportFileName);
            _masterReport = BaselineMasterReport.FromFile(baselineMasterReportFilename);
        }

        private void AssignRelativePathsAndReportNamesToCommandsFromMasterReport()
        {
            Dictionary<string, IReadOnlyDictionary<string, string>> unitCommandDataPaths = new Dictionary<string, IReadOnlyDictionary<string, string>>();
            Dictionary<string, IReadOnlyDictionary<string, string>> unitCommandReportFiles = new Dictionary<string, IReadOnlyDictionary<string, string>>();

            foreach (InvocationBaselineUnit unit in _masterReport.Invocations)
            {
                Dictionary<string, string> commandPathsForUnit = new Dictionary<string, string>();
                unitCommandDataPaths[unit.Name] = commandPathsForUnit;
                Dictionary<string, string> reportFilesForUnit = new Dictionary<string, string>();
                unitCommandReportFiles[unit.Name] = reportFilesForUnit;

                foreach (BaselineCommandData commandData in unit.Invocations)
                {
                    commandPathsForUnit[commandData.Command] = commandData.RelativePath;
                    reportFilesForUnit[commandData.Command] = commandData.ReportFileName;
                }
            }

            _unitNameAndCommandToDataPathMap = unitCommandDataPaths;
            _unitNameAndCommandToReportFileMap = unitCommandReportFiles;
        }

        private void CopyExistingBaseline()
        {
            Console.WriteLine("Copying baseline directory to comparison directory.");
            DirectoryCopy.CopyDirectory(_baselineRoot, _comparisonRoot, true);
        }
    }
}
