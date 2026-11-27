// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed partial class TarGzFileCreateFromDirectory : ToolTask
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

        internal Func<bool> IsPigzAvailable { get; set; } = () => IsPigzOnPath(Environment.GetEnvironmentVariable("PATH"));

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

        private string GetDestinationArchive()
        {
            string ownershipArguments = NormalizeSourceBuildOwnership
                ? "--numeric-owner --owner=2001 --group=2001 "
                : string.Empty;

            string compressionArguments = UsePigz && (IsPigzAvailable?.Invoke() ?? false)
                ? "--use-compress-program=pigz -cf"
                : "-czf";

            return $"{ownershipArguments}{compressionArguments} {DestinationArchive}";
        }

        internal static bool IsPigzOnPath(string pathEnvironmentVariable)
        {
            if (string.IsNullOrEmpty(pathEnvironmentVariable))
            {
                return false;
            }

            string executableName = OperatingSystem.IsWindows() ? "pigz.exe" : "pigz";

            foreach (string rawDirectory in pathEnvironmentVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string directory = OperatingSystem.IsWindows()
                    ? rawDirectory.Trim().Trim('"')
                    : rawDirectory;
                if (string.IsNullOrEmpty(directory))
                {
                    continue;
                }

                string candidatePath = Path.Combine(directory, executableName);
                if (!File.Exists(candidatePath))
                {
                    continue;
                }

                if (IsExecutableFile(candidatePath))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExecutableFile(string candidatePath)
        {
            if (OperatingSystem.IsWindows())
            {
                return true;
            }

            try
            {
                UnixFileMode mode = File.GetUnixFileMode(candidatePath);
                if ((mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
                {
                    return false;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }

            return HasEffectiveExecuteAccess(candidatePath);
        }

        private static bool HasEffectiveExecuteAccess(string path)
        {
            const int X_OK = 1;

            if (OperatingSystem.IsLinux())
            {
                const int AT_FDCWD = -100;
                const int AT_EACCESS = 0x200;
                return NativeMethods.faccessat(AT_FDCWD, path, X_OK, AT_EACCESS) == 0;
            }

            if (OperatingSystem.IsMacOS())
            {
                const int AT_FDCWD = -2;
                const int AT_EACCESS = 0x0010;
                return NativeMethods.faccessat(AT_FDCWD, path, X_OK, AT_EACCESS) == 0;
            }

            if (OperatingSystem.IsFreeBSD())
            {
                const int AT_FDCWD = -100;
                const int AT_EACCESS = 0x0100;
                return NativeMethods.faccessat(AT_FDCWD, path, X_OK, AT_EACCESS) == 0;
            }

            return NativeMethods.access(path, X_OK) == 0;
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

        private static partial class NativeMethods
        {
            [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
            internal static partial int faccessat(int dirfd, string pathname, int mode, int flags);

            [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
            internal static partial int access(string pathname, int mode);
        }
    }
}
