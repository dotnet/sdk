// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Security.Cryptography;

namespace Microsoft.NET.TestFramework.Commands
{
    internal sealed class ScenarioBinlogCapture
    {
        internal const string DirectoryEnvironmentVariable = "DOTNET_SDK_TEST_SCENARIO_BINLOG_DIRECTORY";

        private readonly string _diagnosticDirectory;
        private readonly string? _helixUploadRoot;
        private readonly List<BinlogFile> _binlogs;
        private readonly ITestOutputHelper _log;
        private readonly string _attemptId;
        private readonly bool _isSkipped;

        private ScenarioBinlogCapture(
            string diagnosticDirectory,
            string? helixUploadRoot,
            List<BinlogFile> binlogs,
            ITestOutputHelper log,
            string attemptId,
            bool isSkipped = false)
        {
            _diagnosticDirectory = diagnosticDirectory;
            _helixUploadRoot = helixUploadRoot;
            _binlogs = binlogs;
            _log = log;
            _attemptId = attemptId;
            _isSkipped = isSkipped;
        }

        public static ScenarioBinlogCapture? Begin(
            SdkCommandSpec commandSpec,
            ITestOutputHelper log,
            string commandName)
        {
            if (commandSpec.EnvironmentToRemove.Contains(DirectoryEnvironmentVariable))
            {
                return null;
            }

            string? diagnosticDirectory = commandSpec.Environment.TryGetValue(DirectoryEnvironmentVariable, out string? commandDirectory)
                ? commandDirectory
                : Environment.GetEnvironmentVariable(DirectoryEnvironmentVariable);

            if (string.IsNullOrWhiteSpace(diagnosticDirectory))
            {
                return null;
            }

            if (!IsFullyQualifiedPath(diagnosticDirectory))
            {
                throw new InvalidOperationException(
                    $"{DirectoryEnvironmentVariable} must be an absolute path. Value: '{diagnosticDirectory}'.");
            }

            try
            {
                Directory.CreateDirectory(diagnosticDirectory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                throw new InvalidOperationException(
                    $"Could not create scenario binlog directory '{diagnosticDirectory}'.",
                    exception);
            }

            string workingDirectory = Path.GetFullPath(commandSpec.WorkingDirectory ?? Environment.CurrentDirectory);
            using Process currentProcess = Process.GetCurrentProcess();
            string attemptId = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{currentProcess.Id}-{Guid.NewGuid():N}";
            string? uploadRoot = commandSpec.EnvironmentToRemove.Contains("HELIX_WORKITEM_UPLOAD_ROOT")
                ? null
                : commandSpec.Environment.TryGetValue("HELIX_WORKITEM_UPLOAD_ROOT", out string? commandUploadRoot)
                    ? commandUploadRoot
                    : Environment.GetEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT");

            if (HasUnresolvedResponseFile(commandSpec.Arguments, workingDirectory, commandSpec.FileName))
            {
                log.WriteLine(
                    $"Scenario binlog capture skipped for {commandName} attempt {attemptId}: "
                    + "response-file arguments may configure a binary logger.");
                return new ScenarioBinlogCapture(
                    diagnosticDirectory,
                    uploadRoot,
                    [],
                    log,
                    attemptId,
                    isSkipped: true);
            }

            List<LoggerDestination> destinations = ParseLoggerDestinations(commandSpec.Arguments, attemptId);
            if (destinations.Count == 0)
            {
                string binlogPath = Path.Combine(
                    diagnosticDirectory,
                    $"{SanitizeFileName(commandName)}-{attemptId}.binlog");
                commandSpec.Arguments.Add($"/bl:{binlogPath}");
                destinations.Add(new LoggerDestination(binlogPath, true, false));
            }

            var binlogs = new List<BinlogFile>();
            foreach (LoggerDestination destination in destinations)
            {
                string destinationPath = ResolveFullPath(destination.Path, workingDirectory);
                string? originalHash = GetExistingFileHash(destinationPath);
                string? importsZipPath = destination.IncludesProjectImportsZip
                    ? destinationPath + ".ProjectImports.zip"
                    : null;
                string? originalImportsZipHash = importsZipPath is null ? null : GetExistingFileHash(importsZipPath);

                binlogs.Add(new BinlogFile(
                    destinationPath,
                    destination.IsHarnessDestination,
                    originalHash,
                    importsZipPath,
                    originalImportsZipHash));
            }

            log.WriteLine(
                $"Scenario binlog capture: command={commandName}; attempt={attemptId}; "
                + $"workingDirectory='{workingDirectory}'; directory='{diagnosticDirectory}'.");
            foreach (BinlogFile binlog in binlogs)
            {
                log.WriteLine($"Scenario binlog destination: '{binlog.Path}'.");
            }

            return new ScenarioBinlogCapture(diagnosticDirectory, uploadRoot, binlogs, log, attemptId);
        }

        public void Complete(int exitCode)
        {
            if (_isSkipped)
            {
                return;
            }

            var retainedFiles = new List<string>();

            foreach (BinlogFile binlog in _binlogs)
            {
                if (FileHasChanged(binlog.Path, binlog.OriginalHash))
                {
                    if (!binlog.IsHarnessDestination)
                    {
                        string archivePath = CreateArchivePath(binlog.Path, "binlog");
                        CopyRequired(binlog.Path, archivePath, exitCode);
                        retainedFiles.Add(archivePath);
                        _log.WriteLine($"Scenario binlog archived: '{archivePath}'.");
                    }
                    else
                    {
                        retainedFiles.Add(binlog.Path);
                        _log.WriteLine($"Scenario binlog retained: '{binlog.Path}'.");
                    }
                }

                if (binlog.ImportsZipPath is not null
                    && FileHasChanged(binlog.ImportsZipPath, binlog.OriginalImportsZipHash))
                {
                    if (!binlog.IsHarnessDestination)
                    {
                        string archivePath = CreateArchivePath(binlog.ImportsZipPath, "imports");
                        CopyRequired(binlog.ImportsZipPath, archivePath, exitCode);
                        retainedFiles.Add(archivePath);
                        _log.WriteLine($"Scenario binlog companion archived: '{archivePath}'.");
                    }
                }
            }

            if (retainedFiles.Count == 0)
            {
                _log.WriteLine("Scenario binlog capture produced no new binlog; existing files were left untouched.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(_helixUploadRoot))
            {
                foreach (string sourcePath in retainedFiles.Where(path => IsWithinDirectory(path, _diagnosticDirectory))
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (IsWithinDirectory(sourcePath, _helixUploadRoot))
                    {
                        _log.WriteLine($"Scenario binlog is already under the Helix upload root: '{sourcePath}'.");
                        continue;
                    }

                    string uploadDirectory = Path.Combine(_helixUploadRoot, "scenario-binlogs");
                    string destinationPath = Path.Combine(
                        uploadDirectory,
                        $"{_attemptId}-{Path.GetFileName(sourcePath)}");
                    CopyRequired(sourcePath, destinationPath, exitCode);
                    _log.WriteLine($"Scenario binlog uploaded: '{destinationPath}'.");
                }
            }
        }

        private string CreateArchivePath(string sourcePath, string category)
        {
            string archiveDirectory = Path.Combine(_diagnosticDirectory, "authored");
            return Path.Combine(
                archiveDirectory,
                $"{SanitizeFileName(category)}-{_attemptId}-{Guid.NewGuid():N}-{Path.GetFileName(sourcePath)}");
        }

        private void CopyRequired(string sourcePath, string destinationPath, int buildExitCode)
        {
            try
            {
                string? destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(sourcePath, destinationPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                throw new InvalidOperationException(
                    $"The build command exited with code {buildExitCode}, but scenario binlog retention failed. "
                    + $"Could not copy '{sourcePath}' to '{destinationPath}'.",
                    exception);
            }
        }

        private static bool IsFullyQualifiedPath(string path)
        {
            if (Path.DirectorySeparatorChar != '\\')
            {
                return Path.IsPathRooted(path);
            }

            return path.StartsWith(@"\\", StringComparison.Ordinal)
                || (path.Length >= 3
                    && char.IsLetter(path[0])
                    && path[1] == ':'
                    && (path[2] == '\\' || path[2] == '/'));
        }

        private static bool HasUnresolvedResponseFile(
            IEnumerable<string> arguments,
            string workingDirectory,
            string? executablePath)
        {
            if (arguments.Any(argument => argument.StartsWith("@", StringComparison.Ordinal)))
            {
                return true;
            }

            var responseFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.Combine(workingDirectory, "MSBuild.rsp"),
                Path.Combine(workingDirectory, "Directory.Build.rsp"),
            };

            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                string? executableDirectory = Path.GetDirectoryName(executablePath);
                if (!string.IsNullOrEmpty(executableDirectory))
                {
                    responseFilePaths.Add(Path.Combine(executableDirectory, "MSBuild.rsp"));
                    if (Path.GetFileNameWithoutExtension(executablePath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                    {
                        string sdkDirectory = Path.Combine(executableDirectory, "sdk");
                        if (Directory.Exists(sdkDirectory))
                        {
                            foreach (string sdkVersionDirectory in Directory.GetDirectories(sdkDirectory))
                            {
                                responseFilePaths.Add(Path.Combine(sdkVersionDirectory, "MSBuild.rsp"));
                            }
                        }
                    }
                }
            }

            foreach (string argument in arguments)
            {
                string candidate = argument.Trim('"');
                if (candidate.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                    || candidate.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                    || candidate.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
                    || candidate.EndsWith(".proj", StringComparison.OrdinalIgnoreCase))
                {
                    string? projectDirectory = Path.GetDirectoryName(ResolveFullPath(candidate, workingDirectory));
                    if (!string.IsNullOrEmpty(projectDirectory))
                    {
                        responseFilePaths.Add(Path.Combine(projectDirectory, "Directory.Build.rsp"));
                    }
                }
            }

            return responseFilePaths.Any(File.Exists);
        }

        private static List<LoggerDestination> ParseLoggerDestinations(
            List<string> arguments,
            string attemptId)
        {
            var destinations = new List<LoggerDestination>();
            for (int index = 0; index < arguments.Count; index++)
            {
                string argument = arguments[index];
                if (TryGetBinaryLoggerParameters(argument, arguments, ref index, out string? parameters, out int parameterArgumentIndex))
                {
                    if (parameters is not null && parameters.IndexOf("{}", StringComparison.Ordinal) >= 0)
                    {
                        arguments[parameterArgumentIndex] = arguments[parameterArgumentIndex].Replace("{}", attemptId);
                        parameters = parameters.Replace("{}", attemptId);
                    }

                    destinations.Add(ParseDestination(parameters));
                }
            }

            return destinations;
        }

        private static bool TryGetBinaryLoggerParameters(
            string argument,
            List<string> arguments,
            ref int index,
            out string? parameters,
            out int parameterArgumentIndex)
        {
            parameters = null;
            parameterArgumentIndex = index;
            foreach (string option in new[] { "--binaryLogger", "/binaryLogger", "-binaryLogger", "/bl", "-bl" })
            {
                if (argument.Equals(option, StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 < arguments.Count
                        && (arguments[index + 1].EndsWith(".binlog", StringComparison.OrdinalIgnoreCase)
                            || arguments[index + 1].StartsWith("LogFile=", StringComparison.OrdinalIgnoreCase)))
                    {
                        parameterArgumentIndex = ++index;
                        parameters = arguments[parameterArgumentIndex];
                    }
                    return true;
                }

                if (argument.StartsWith(option + ":", StringComparison.OrdinalIgnoreCase))
                {
                    parameters = argument[(option.Length + 1)..];
                    return true;
                }
            }

            foreach (string option in new[] { "--logger", "/logger", "-logger" })
            {
                if (argument.Equals(option, StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 < arguments.Count)
                    {
                        string? parsedParameters = ParseBinaryLoggerFromLoggerArgument(arguments[index + 1]);
                        if (parsedParameters is not null)
                        {
                            parameterArgumentIndex = ++index;
                            parameters = parsedParameters;
                            return true;
                        }
                    }
                    return false;
                }

                if (argument.StartsWith(option + ":", StringComparison.OrdinalIgnoreCase))
                {
                    parameters = ParseBinaryLoggerFromLoggerArgument(argument[(option.Length + 1)..]);
                    return parameters is not null;
                }
            }

            return false;
        }

        private static string? ParseBinaryLoggerFromLoggerArgument(string argument)
        {
            int optionSeparator = argument.IndexOf(';');
            string loggerName = optionSeparator >= 0 ? argument[..optionSeparator] : argument;
            if (loggerName.IndexOf("BinaryLogger", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return null;
            }

            return optionSeparator >= 0 ? argument[(optionSeparator + 1)..] : string.Empty;
        }

        private static LoggerDestination ParseDestination(string? parameters)
        {
            string? path = null;
            bool projectImportsZip = false;
            if (!string.IsNullOrEmpty(parameters))
            {
                foreach (string parameter in parameters.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmedParameter = parameter.Trim().Trim('"');
                    if (trimmedParameter.Equals("ProjectImports=ZipFile", StringComparison.OrdinalIgnoreCase))
                    {
                        projectImportsZip = true;
                    }
                    else if (trimmedParameter.StartsWith("LogFile=", StringComparison.OrdinalIgnoreCase))
                    {
                        path = trimmedParameter["LogFile=".Length..].Trim('"');
                    }
                    else if (trimmedParameter.IndexOf('=') < 0)
                    {
                        path = trimmedParameter;
                    }
                }
            }

            path ??= "msbuild.binlog";
            return new LoggerDestination(path, false, projectImportsZip);
        }

        private static string? GetExistingFileHash(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            using var sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(stream));
        }

        private static bool FileHasChanged(string path, string? originalHash)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            string? currentHash = GetExistingFileHash(path);
            return !string.Equals(originalHash, currentHash, StringComparison.Ordinal);
        }

        private static bool IsWithinDirectory(string path, string directory)
        {
            string fullPath = Path.GetFullPath(path);
            string fullDirectory = Path.GetFullPath(directory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    fullDirectory.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidCharacter, '-');
            }

            return value;
        }

        private static string ResolveFullPath(string path, string basePath) =>
            Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(basePath, path));

        private sealed class BinlogFile
        {
            public string Path { get; }
            public bool IsHarnessDestination { get; }
            public string? OriginalHash { get; }
            public string? ImportsZipPath { get; }
            public string? OriginalImportsZipHash { get; }

            public BinlogFile(
                string path,
                bool isHarnessDestination,
                string? originalHash,
                string? importsZipPath,
                string? originalImportsZipHash)
            {
                Path = path;
                IsHarnessDestination = isHarnessDestination;
                OriginalHash = originalHash;
                ImportsZipPath = importsZipPath;
                OriginalImportsZipHash = originalImportsZipHash;
            }
        }

        private sealed class LoggerDestination
        {
            public string Path { get; }
            public bool IsHarnessDestination { get; }
            public bool IncludesProjectImportsZip { get; }

            public LoggerDestination(string path, bool isHarnessDestination, bool includesProjectImportsZip)
            {
                Path = path;
                IsHarnessDestination = isHarnessDestination;
                IncludesProjectImportsZip = includesProjectImportsZip;
            }
        }
    }
}
