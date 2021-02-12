// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public class DotNetDebTool : ToolTask
    {
        [Required]
        public string InputDirectory { get; set; }

        [Required]
        public string OutputDirectory { get; set; }

        [Required]
        public string PackageName { get; set; }

        [Required]
        public string PackageVersion { get; set; }

        public string WorkingDirectory { get; set; }

        protected override string ToolName => "package_tool.sh";

        private string GetInputDir()
        {
            return $"-i {InputDirectory}";
        }

        private string GetOutputFile()
        {
            return $"-o {OutputDirectory}";
        }

        private string GetPackageName()
        {
            return $"-n {PackageName}";
        }

        private string GetPackageVersion()
        {
            return $"-v {PackageVersion}";
        }

        protected override MessageImportance StandardOutputLoggingImportance
        {
            get { return MessageImportance.High; } // or else the output doesn't get logged by default
        }

        protected override string GenerateFullPathToTool()
        {
            string path = ToolPath;

            // if ToolPath was not provided by the MSBuild script 
            if (string.IsNullOrEmpty(path))
            {
                Log.LogError($"Could not find the Path to {ToolName}");

                return string.Empty;
            }

            return path;
        }

        protected override string GetWorkingDirectory()
        {
            return WorkingDirectory ?? base.GetWorkingDirectory();
        }

        protected override string GenerateCommandLineCommands()
        {
            var commandLineCommands = $"{GetInputDir()} {GetOutputFile()} {GetPackageName()} {GetPackageVersion()}";

            LogToolCommand($"package_tool.sh {commandLineCommands}");

            return commandLineCommands;
        }

        protected override void LogToolCommand(string message)
        {
            base.LogToolCommand($"{GetWorkingDirectory()}> {message}");
        }

        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            Log.LogMessage(messageImportance, singleLine, null);
        }

        protected override ProcessStartInfo GetProcessStartInfo(
            string pathToTool,
            string commandLineCommands,
            string responseFileSwitch)
        {
            var psi = base.GetProcessStartInfo(
                pathToTool,
                commandLineCommands,
                responseFileSwitch);

            foreach (var environmentVariableName in new EnvironmentFilter().GetEnvironmentVariableNamesToRemove())
            {
                psi.Environment.Remove(environmentVariableName);
            }

            return psi;
        }
    }
}
