using System;
using System.Collections.Generic;
using System.IO;
using BaselineComparer.TemplateComparison;
using Newtonsoft.Json.Linq;

namespace BaselineComparer
{
    public class BaselineReportCreator
    {
        public static readonly string MasterBaselineReportFileName = "Baseline.json";

        public BaselineReportCreator(string masterDataBasePath, string secondaryDataBasePath, IReadOnlyList<InvocationUnit> invocationUnits, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> unitNameAndCommandToDataPathMap, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> unitNameAndCommandToReportFileMap)
        {
            _masterDataBasePath = masterDataBasePath;
            _secondaryDataBasePath = secondaryDataBasePath;
            _invocationUnits = invocationUnits;
            _unitNameAndCommandToDataPathMap = unitNameAndCommandToDataPathMap;
            _unitNameAndCommandToReportFileMap = unitNameAndCommandToReportFileMap;
        }

        private readonly string _masterDataBasePath;
        private readonly string _secondaryDataBasePath;
        private readonly IReadOnlyList<InvocationUnit> _invocationUnits;
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _unitNameAndCommandToDataPathMap;
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _unitNameAndCommandToReportFileMap;

        public void WriteAllBaselineComparisons(string reportDir, string newCommand)
        {
            if (!Directory.Exists(reportDir))
            {
                Directory.CreateDirectory(reportDir);
            }

            // write the comparisons for the commands
            List<InvocationBaselineUnit> invocationBaselineUnitList = new List<InvocationBaselineUnit>();

            foreach (InvocationUnit unit in _invocationUnits)
            {
                IReadOnlyList<BaselineCommandData> unitCommandData = WriteBaselineComparisonsForInvocationUnit(unit, reportDir, newCommand);
                InvocationBaselineUnit reportUnit = new InvocationBaselineUnit(unit.Name, unit.InstallRequirements, unitCommandData);
                invocationBaselineUnitList.Add(reportUnit);
            }

            // write the master baseline report.
            BaselineMasterReport masterBaseline = new BaselineMasterReport()
            {
                NewCommand = newCommand,
                Invocations = invocationBaselineUnitList
            };
            JObject serializedMaster = JObject.FromObject(masterBaseline);

            string masterBaselineReportFilename = Path.Combine(reportDir, MasterBaselineReportFileName);
            File.WriteAllText(masterBaselineReportFilename, serializedMaster.ToString());
        }

        private IReadOnlyList<BaselineCommandData> WriteBaselineComparisonsForInvocationUnit(InvocationUnit unit, string reportDir, string newCommand)
        {
            if (!_unitNameAndCommandToDataPathMap.TryGetValue(unit.Name, out IReadOnlyDictionary<string, string> commandToDataRelativePathMap))
            {
                throw new Exception($"Invocation unit {unit.Name} didn't have a command-path map");
            }

            if (!_unitNameAndCommandToReportFileMap.TryGetValue(unit.Name, out IReadOnlyDictionary<string, string> commandToReportFileMap))
            {
                throw new Exception($"Invocation unit {unit.Name} didn't have a report file map");
            }

            IReadOnlyDictionary<string, DirectoryDifference> comparisonsForUnit = CreateComparisonsForInvocationUnit(commandToDataRelativePathMap);
            List<BaselineCommandData> commandInfoList = new List<BaselineCommandData>();

            foreach (KeyValuePair<string, DirectoryDifference> commandComparison in comparisonsForUnit)
            {
                string command = commandComparison.Key;
                if (!commandToDataRelativePathMap.TryGetValue(command, out string relativePath))
                {
                    throw new Exception($"Unit '{unit.Name}' data map didnt have a data path for command: '{command}'");
                }

                CommandBaseline baselineForCommand = new CommandBaseline()
                {
                    InvocationName = unit.Name,
                    Command = command,
                    NewCommand = newCommand,
                    FileResults = commandComparison.Value
                };

                if (!commandToReportFileMap.TryGetValue(command, out string reportFilename))
                {
                    throw new Exception($"Unit '{unit.Name}' report map didnt have a report file for command: '{command}'");
                }

                string reportFileFullPath = Path.Combine(reportDir, reportFilename);
                JObject serialized = JObject.FromObject(baselineForCommand);

                File.WriteAllText(reportFileFullPath, serialized.ToString());

                BaselineCommandData dataForCommand = new BaselineCommandData()
                {
                    Command = command,
                    RelativePath = relativePath,
                    ReportFileName = reportFilename
                };
                commandInfoList.Add(dataForCommand);
            }

            return commandInfoList;
        }

        private IReadOnlyDictionary<string, DirectoryDifference> CreateComparisonsForInvocationUnit(IReadOnlyDictionary<string, string> commandAndPathMap)
        {
            Dictionary<string, DirectoryDifference> comparisonResults = new Dictionary<string, DirectoryDifference>();

            foreach (KeyValuePair<string, string> commandAndRelativePath in commandAndPathMap)
            {
                string command = commandAndRelativePath.Key;
                string relativePath = commandAndRelativePath.Value;

                DirectoryComparer comparer = new DirectoryComparer(_masterDataBasePath, _secondaryDataBasePath, relativePath);
                comparisonResults[command] = comparer.Compare();
            }

            return comparisonResults;
        }
    }
}
