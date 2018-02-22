using System;
using System.Collections.Generic;
using System.IO;

namespace BaselineComparer
{
    public class BaselineCreator
    {
        public static readonly string BaseDataDir = "Data";
        public static readonly string BaseReportDir = "BaselineReports";
        public static readonly string BaselineMasterDataDirName = "BaselineMaster";
        private static readonly string BaselineSecondaryDataDirName = "BaselineSecondary";

        public BaselineCreator(string baselineRoot, string newCommand, IReadOnlyList<InvocationUnit> invocationUnits)
        {
            BaselineRoot = baselineRoot;
            NewCommand = newCommand;
            string dataDir = Path.Combine(BaselineRoot, BaseDataDir);
            _masterDataBasePath = Path.Combine(dataDir, BaselineMasterDataDirName);
            _secondaryDataBasePath = Path.Combine(dataDir, BaselineSecondaryDataDirName);

            _reportDir = Path.Combine(BaselineRoot, BaseReportDir);
            _invocationUnits = invocationUnits;
        }

        public string BaselineRoot { get; }
        public string NewCommand { get; }

        private readonly string _masterDataBasePath;
        private readonly string _secondaryDataBasePath;
        private readonly string _reportDir;

        private readonly IReadOnlyList<InvocationUnit> _invocationUnits;
        // unit name -> command -> relative path
        private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _unitNameAndCommandToDataPathMap;
        private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _unitNameAndCommandToReportFileMap;

        public void CreateBaseline()
        {
            AssignRelativePathsAndReportNamesToCommands();

            if (!TryCreateTemplates())
            {
                throw new Exception("There were problems creating the templates to compare.");
            }

            BaselineReportCreator reportCreator = new BaselineReportCreator(_masterDataBasePath, _secondaryDataBasePath, _invocationUnits, _unitNameAndCommandToDataPathMap, _unitNameAndCommandToReportFileMap);
            reportCreator.WriteAllBaselineComparisons(_reportDir, NewCommand);
        }

        // todo: move this outside this class for creating comparison data.
        private bool TryCreateTemplates()
        {
            bool allCreationsGood = true;

            foreach (InvocationUnit unit in _invocationUnits)
            {
                CustomHiveCoordinator hive = new CustomHiveCoordinator(NewCommand, unit.InstallRequirements);
                hive.InitializeHive();

                IReadOnlyDictionary<string, string> commandToPathMap = _unitNameAndCommandToDataPathMap[unit.Name];
                allCreationsGood &= hive.TryCreateTemplateData(_masterDataBasePath, commandToPathMap);
                allCreationsGood &= hive.TryCreateTemplateData(_secondaryDataBasePath, commandToPathMap);

                hive.DeleteHive();
            }

            return allCreationsGood;
        }

        private void AssignRelativePathsAndReportNamesToCommands()
        {
            Dictionary<string, IReadOnlyDictionary<string, string>> unitCommandDataPaths = new Dictionary<string, IReadOnlyDictionary<string, string>>();
            Dictionary<string, IReadOnlyDictionary<string, string>> unitCommandReportFiles = new Dictionary<string, IReadOnlyDictionary<string, string>>();

            foreach (InvocationUnit unit in _invocationUnits)
            {
                int commandIndex = 0;

                if (unitCommandDataPaths.ContainsKey(unit.Name))
                {
                    throw new Exception($"Multiple invocation units with the same name are not allowed. Duplicated name: '{unit.Name}'");
                }

                Dictionary<string, string> commandToDataPathMap = new Dictionary<string, string>();
                unitCommandDataPaths[unit.Name] = commandToDataPathMap;
                Dictionary<string, string> commandToReportFileMap = new Dictionary<string, string>();
                unitCommandReportFiles[unit.Name] = commandToReportFileMap;

                foreach (string command in unit.InvocationCommands)
                {
                    commandToDataPathMap[command] = Guid.NewGuid().ToString();
                    commandToReportFileMap[command] = $"{unit.Name}_{commandIndex}.json";
                    ++commandIndex;
                }
            }

            _unitNameAndCommandToDataPathMap = unitCommandDataPaths;
            _unitNameAndCommandToReportFileMap = unitCommandReportFiles;
        }
    }
}
