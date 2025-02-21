// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class TarGzFileCreateFromDirectory : ToolTask
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
        /// An item group of regular expressions for content to exclude from the archive.
        /// </summary>
        public ITaskItem[] ExcludePatterns { get; set; }

        public bool IgnoreExitCode { get; set; }

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

            if (File.Exists(DestinationArchive))
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

            SourceDirectory = Path.GetFullPath(SourceDirectory);

            SourceDirectory = SourceDirectory.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? SourceDirectory
                : SourceDirectory + Path.DirectorySeparatorChar;

            if (!Directory.Exists(SourceDirectory))
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
                var parentDirectory = Directory.GetParent(SourceDirectory).Parent.FullName;
                var sourceDirectoryName = Path.GetFileName(Path.GetDirectoryName(SourceDirectory));
                return $"--directory {parentDirectory} {sourceDirectoryName}  {GetExcludes()}";
            }
            else
            {
                return $"--directory {SourceDirectory}  {GetExcludes()} \".\"";
            }
        }

        private string GetDestinationArchive() => $"-czf {DestinationArchive}";

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
