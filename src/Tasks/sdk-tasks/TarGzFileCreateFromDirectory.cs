// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.ComponentModel;
using System.Diagnostics;

namespace Microsoft.DotNet.Build.Tasks
{
    [MSBuildMultiThreadableTask]
    public sealed class TarGzFileCreateFromDirectory : ToolTask, IMultiThreadableTask
    {
        /// <summary>
        /// The path to the directory to be archived.
        /// </summary>
        [Required]
        public string SourceDirectory { get; set; }

        /// <summary>
        /// The path of the archive to be created.
        /// </summary>
        [Required]
        public string DestinationArchive { get; set; }

        /// <summary>
        /// Indicates if the destination archive should be overwritten if it already exists.
        /// </summary>
        public bool OverwriteDestination { get; set; }

        /// <summary>
        /// If zipping an entire folder without exclusion patterns, whether to include the folder in the archive.
        /// </summary>
        public bool IncludeBaseDirectory { get; set; }

        /// <summary>
        /// Whether to normalize archive ownership for source-build artifacts.
        /// </summary>
        public bool NormalizeSourceBuildOwnership { get; set; }

        /// <summary>
        /// Whether to use pigz (parallel gzip) for compression when available on PATH.
        /// </summary>
        public bool UsePigz { get; set; } = true;

        /// <summary>
        /// An item group of regular expressions for content to exclude from the archive.
        /// </summary>
        public ITaskItem[] ExcludePatterns { get; set; }

        public bool IgnoreExitCode { get; set; }

        internal Func<bool> IsPigzAvailable { get; set; }

        protected override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            int returnCode = base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands);

            if (IgnoreExitCode)
            {
                returnCode = 0;
            }

            return returnCode;
        }

        protected override bool ValidateParameters()
        {
            base.ValidateParameters();

            var retVal = true;

            AbsolutePath destinationArchive = TaskEnvironment.GetAbsolutePath(DestinationArchive);
            if (File.Exists(destinationArchive))
            {
                if (OverwriteDestination == true)
                {
                    Log.LogMessage(MessageImportance.Low, $"{DestinationArchive} will be overwritten");
                }
                else
                {
                    Log.LogError($"'{DestinationArchive}' already exists. Did you forget to set '{nameof(OverwriteDestination)}' to true?");

                    retVal = false;
                }
            }

            AbsolutePath sourceDirectory = TaskEnvironment.GetAbsolutePath(SourceDirectory);

            SourceDirectory = sourceDirectory.Value.EndsWith(Path.DirectorySeparatorChar)
                ? sourceDirectory.Value
                : sourceDirectory.Value + Path.DirectorySeparatorChar;

            if (!Directory.Exists(sourceDirectory))
            {
                Log.LogError($"SourceDirectory '{SourceDirectory} does not exist.");

                retVal = false;
            }

            return retVal;
        }

        public override bool Execute() => base.Execute();

        protected override string ToolName => "tar";

        protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.High;

        protected override string GenerateFullPathToTool() => "tar";

        protected override string GenerateCommandLineCommands() => $"{GetDestinationArchive()} {GetSourceSpecification()}";

        private string GetSourceSpecification()
        {
            if (IncludeBaseDirectory)
            {
                DirectoryInfo sourceDirectory = new(TaskEnvironment.GetAbsolutePath(SourceDirectory));
                var parentDirectory = sourceDirectory.Parent.FullName;
                var sourceDirectoryName = sourceDirectory.Name;
                return $"--directory {parentDirectory} {sourceDirectoryName}  {GetExcludes()}";
            }
            else
            {
                return $"--directory {SourceDirectory}  {GetExcludes()} \".\"";
            }
        }

        private string GetDestinationArchive()
        {
            string ownershipArguments = NormalizeSourceBuildOwnership
                ? "--numeric-owner --owner=2001 --group=2001 "
                : string.Empty;

            string compressionArguments = UsePigz && (IsPigzAvailable?.Invoke() ?? ProbePigz())
                ? "--use-compress-program=pigz -cf"
                : "-czf";

            return $"{ownershipArguments}{compressionArguments} {DestinationArchive}";
        }

        private bool ProbePigz()
        {
            try
            {
                ProcessStartInfo startInfo = TaskEnvironment.GetProcessStartInfo();
                startInfo.FileName = "pigz";
                startInfo.Arguments = "--version";
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;

                using var process = new Process { StartInfo = startInfo };
                if (!process.Start())
                {
                    return false;
                }

                if (!process.WaitForExit(5000))
                {
#pragma warning disable MSBuildTask0001 // Only terminates the pigz probe child process started by this task.
                    try { process.Kill(entireProcessTree: true); } catch { }
#pragma warning restore MSBuildTask0001
                    return false;
                }

                return process.ExitCode == 0;
            }
            catch (Exception ex) when (ex is Win32Exception or IOException)
            {
                return false;
            }
        }

        private string GetExcludes()
        {
            var excludes = string.Empty;

            if (ExcludePatterns != null)
            {
                foreach (var excludeTaskItem in ExcludePatterns)
                {
                    excludes += $" --exclude {excludeTaskItem.ItemSpec}";
                }
            }
            
            return excludes;
        }

        protected override void LogToolCommand(string message) => base.LogToolCommand($"{GetWorkingDirectory()}> {message}");
    }
}
