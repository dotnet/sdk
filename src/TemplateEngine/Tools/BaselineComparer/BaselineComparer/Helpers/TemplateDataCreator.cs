using System;
using System.Collections.Generic;
using System.IO;

namespace BaselineComparer.Helpers
{
    public class TemplateDataCreator
    {
        public TemplateDataCreator(string dotnetCommand, string creationBaseDir, IReadOnlyList<string> templateCommands, string customHiveBaseDir)
        {
            _dotnetCommand = dotnetCommand;
            _creationBaseDir = creationBaseDir;
            _templateCommands = templateCommands;
            _customHiveBaseDir = customHiveBaseDir;
        }

        public TemplateDataCreator(string dotnetCommand, string creationBaseDir, IReadOnlyList<string> templateCommands)
        {
            _dotnetCommand = dotnetCommand;
            _creationBaseDir = creationBaseDir;
            _templateCommands = templateCommands;
            _customHiveBaseDir = $"c:\\temp\\customHive-{Guid.NewGuid().ToString()}";
        }

        private readonly string _dotnetCommand;
        private readonly string _creationBaseDir;
        private readonly IReadOnlyList<string> _templateCommands;
        private readonly string _customHiveBaseDir;

        private bool ValidateProperties()
        {
            if (!string.Equals(_dotnetCommand, "new") && !string.Equals(_dotnetCommand, "new3"))
            {
                Console.WriteLine($"dotnet command arg value must be either 'new' or 'new3'. Actual = '{_dotnetCommand}");
                return false;
            }

            if (Directory.Exists(_creationBaseDir))
            {
                Console.WriteLine($"Template creation base directory '${_creationBaseDir}' already exists.");
                return false;
            }
            else if (!Path.IsPathRooted(_creationBaseDir))
            {
                Console.WriteLine($"Template creation base directory '${_creationBaseDir}' must be rooted");
                return false;
            }

            return true;
        }

        public bool PerformTemplateCommands(bool isTemporaryHive)
        {
            if (!ValidateProperties())
            {
                return false;
            }

            string originalDirectory = Directory.GetCurrentDirectory();
            if (!Directory.Exists(_creationBaseDir))
            {
                Directory.CreateDirectory(_creationBaseDir);
            }
            Environment.CurrentDirectory = _creationBaseDir;

            if (isTemporaryHive)
            {
                InitializeHive();
            }

            foreach (string command in _templateCommands)
            {
                string args = $"{command} --debug:custom-hive {_customHiveBaseDir}";
                Run(args);
            }

            Environment.CurrentDirectory = originalDirectory;

            if (isTemporaryHive)
            {
                RemoveHive();
            }

            return true;
        }

        private void InitializeHive()
        {
            string args = $"--debug:reinit --debug:custom-hive {_customHiveBaseDir}";
            Console.WriteLine($"Reinitializing hive: {args}");
            Run(args);
        }

        private void RemoveHive()
        {
            Directory.Delete(_customHiveBaseDir, true);
        }

        private void Run(string args)
        {
            string commandArgs = _dotnetCommand + " " + args;
            Console.WriteLine($"Running: dotnet {commandArgs}");

            Proc.Run("dotnet", commandArgs).WaitForExit();
        }
    }
}
