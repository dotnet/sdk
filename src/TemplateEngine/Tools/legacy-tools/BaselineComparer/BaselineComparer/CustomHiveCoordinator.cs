using System;
using System.Collections.Generic;
using System.IO;
using BaselineComparer.Helpers;

namespace BaselineComparer
{
    public class CustomHiveCoordinator
    {
        public CustomHiveCoordinator(string newCommand, IReadOnlyList<string> toInstall)
        {
            _newCommand = newCommand;
            _toInstallList = toInstall;
            _customHiveBaseDir = @"c:\temp\" + Guid.NewGuid().ToString() + @"\";
        }

        private readonly string _newCommand;
        private readonly IReadOnlyList<string> _toInstallList;
        private readonly string _customHiveBaseDir;

        public bool TryCreateTemplateData(string basePath, IReadOnlyDictionary<string, string> commandToRelativePathMap)
        {
            bool allCreationGood = true;

            foreach (KeyValuePair<string, string> commandAndRelativePath in commandToRelativePathMap)
            {
                string command = commandAndRelativePath.Key;
                string relativePath = commandAndRelativePath.Value;
                string creationPath = Path.Combine(basePath, relativePath);
                allCreationGood &= TemplateCommandRunner.RunTemplateCommand(_newCommand, creationPath, command, _customHiveBaseDir, false);
            }

            return allCreationGood;
        }

        public void DeleteHive()
        {
            Directory.Delete(_customHiveBaseDir, true);
        }

        public bool InitializeHive()
        {
            bool anyProblems = TryUninstallAllTemplatesFromHive();
            anyProblems |= TryInstallRequiredPackages();

            return anyProblems;
        }

        // TODO: better detection that uninstalls actually worked (Can't do right now)
        private bool TryInstallRequiredPackages()
        {
            Console.WriteLine("Installing required packages.");

            string installCommandArgs = null;
            try
            {
                foreach (string toInstall in _toInstallList)
                {
                    installCommandArgs = $"{_newCommand} -i {toInstall} --debug:custom-hive {_customHiveBaseDir}";
                    Console.WriteLine($"Installing {toInstall} ");
                    Console.WriteLine($"\t{installCommandArgs}");
                    Proc.Run("dotnet", installCommandArgs).WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to install everything for the install unit. Failure occurred with command:");
                Console.WriteLine($"\t{installCommandArgs}");
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }

            return true;
        }

        private static readonly string UninstallListHeader = "Currently installed items:";

        // TODO: better detection that installs actually worked (Can't do right now)
        private bool TryUninstallAllTemplatesFromHive()
        {
            Console.WriteLine("Finding things to uninstall.");

            string uninstallListCommandArgs = $"{_newCommand} -u --debug:custom-hive {_customHiveBaseDir}";
            ProcessEx uninstallListCommand = Proc.Run("dotnet", uninstallListCommandArgs);
            uninstallListCommand.WaitForExit();

            string output = uninstallListCommand.Output;

            string[] outputLineList = output.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            List<string> toUninstallList = new List<string>();

            string previousLine = null;

            foreach (string outputLine in outputLineList)
            {
                if (string.Equals(outputLine.Trim(), "Templates:", StringComparison.Ordinal)
                    && !string.IsNullOrEmpty(previousLine))
                {
                    toUninstallList.Add(previousLine);
                    Console.WriteLine($"adding to uninstall list: {previousLine}");
                }

                previousLine = outputLine;
            }

            Console.WriteLine("Uninstalling everything.");

            try
            {
                foreach (string toUninstall in toUninstallList)
                {
                    string uninstallArgs = $"{_newCommand} -u {toUninstall} --debug:custom-hive {_customHiveBaseDir}";
                    Console.WriteLine($"Uninstalling {toUninstall}");
                    Console.WriteLine($"Uninstall command args: {uninstallArgs}");
                    Proc.Run("dotnet", uninstallArgs).WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to uninstall everything for custom hive {_customHiveBaseDir}");
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }

            return true;
        }
    }
}
