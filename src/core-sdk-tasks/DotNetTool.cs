// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli.Build.Framework;

namespace Microsoft.DotNet.Cli.Build
{
    public abstract class DotNetTool : ToolTask
    {
        public DotNetTool()
        {
        }

        protected abstract string Command { get; }

        protected abstract string Args { get; }

        protected override ProcessStartInfo GetProcessStartInfo(string pathToTool, string commandLineCommands, string responseFileSwitch)
        {
            var psi = base.GetProcessStartInfo(pathToTool, commandLineCommands, responseFileSwitch);
            foreach (var environmentVariableName in new EnvironmentFilter().GetEnvironmentVariableNamesToRemove())
            {
                psi.Environment.Remove(environmentVariableName);
            }

            return psi;
        }

        public string WorkingDirectory { get; set; }

        protected override string ToolName => $"dotnet{(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty)}";

        protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.High;

        protected override string GenerateFullPathToTool()
        {
            // if ToolPath was not provided by the MSBuild script
            if (string.IsNullOrEmpty(ToolPath))
            {
                Log.LogError($"Could not find the Path to {ToolName}");

                return string.Empty;
            }

            return ToolPath;
        }

        protected override string GetWorkingDirectory() => WorkingDirectory ?? base.GetWorkingDirectory();

        protected override string GenerateCommandLineCommands()
        {
            var commandLineCommands = $"{Command} {Args}";

            LogToolCommand($"[DotNetTool] {commandLineCommands}");

            return commandLineCommands;
        }

        protected override void LogToolCommand(string message) => base.LogToolCommand($"{GetWorkingDirectory()}> {message}");
    }
}
