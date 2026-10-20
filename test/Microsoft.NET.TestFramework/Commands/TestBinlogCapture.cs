// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Security.Cryptography;

namespace Microsoft.NET.TestFramework.Commands
{
    internal sealed class TestBinlogCapture
    {
        // Capture is opt-in; existing test commands keep their default logging behavior.
        internal const string DirectoryEnvironmentVariable = "DOTNET_SDK_TEST_BINLOG_DIRECTORY";

        private readonly string _diagnosticDirectory;
        private readonly List<BinlogFile> _binlogs;
        private readonly ITestOutputHelper _log;

        private TestBinlogCapture(
            string diagnosticDirectory,
            List<BinlogFile> binlogs,
            ITestOutputHelper log)
        {
            _diagnosticDirectory = diagnosticDirectory;
            _binlogs = binlogs;
            _log = log;
        }

        public static TestBinlogCapture? Begin(
            SdkCommandSpec commandSpec,
            ITestOutputHelper log,
            string commandName)
        {
            StringComparer environmentComparer = Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            if (commandSpec.EnvironmentToRemove.Any(
                name => environmentComparer.Equals(name, DirectoryEnvironmentVariable)))
            {
                return null;
            }

            string? diagnosticDirectory = null;
            bool hasCommandOverride = false;
            foreach (KeyValuePair<string, string?> environmentVariable in commandSpec.Environment)
            {
                string name = environmentVariable.Key;
                string? value = environmentVariable.Value;
                if (environmentComparer.Equals(name, DirectoryEnvironmentVariable))
                {
                    diagnosticDirectory = value;
                    hasCommandOverride = true;
                }
            }

            if (!hasCommandOverride)
            {
                diagnosticDirectory = Environment.GetEnvironmentVariable(DirectoryEnvironmentVariable);
            }

            if (string.IsNullOrWhiteSpace(diagnosticDirectory))
            {
                return null;
            }

            if (!IsFullyQualifiedPath(diagnosticDirectory))
            {
                throw new InvalidOperationException(
                    $"{DirectoryEnvironmentVariable} must be an absolute path. Value: '{diagnosticDirectory}'.");
            }

            // Reject an unusable destination before launching the build.
            try
            {
                Directory.CreateDirectory(diagnosticDirectory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                throw new InvalidOperationException(
                    $"Could not create test binlog directory '{diagnosticDirectory}'.",
                    exception);
            }

            string workingDirectory = Path.GetFullPath(commandSpec.WorkingDirectory ?? Environment.CurrentDirectory);
            using Process currentProcess = Process.GetCurrentProcess();
            string attemptId = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{currentProcess.Id}-{Guid.NewGuid():N}";
            if (HasUnresolvedResponseFile(commandSpec.Arguments, workingDirectory, commandSpec.FileName))
            {
                log.WriteLine(
                    $"Test binlog capture skipped for {commandName} attempt {attemptId}: "
                    + "response-file arguments may configure a binary logger.");
                // A non-null capture prevents TestCommand from falling back to broad legacy collection.
                return new TestBinlogCapture(
                    diagnosticDirectory,
                    [],
                    log);
            }

            List<LoggerDestination> destinations = ParseLoggerDestinations(commandSpec.Arguments, attemptId);
            if (destinations.Count == 0)
            {
                // Add a logger only when the test has not supplied one.
                string binlogPath = Path.Combine(
                    diagnosticDirectory,
                    $"{Guid.NewGuid():N}.binlog");
                commandSpec.Arguments.Add($"/bl:{binlogPath}");
                destinations.Add(new LoggerDestination(binlogPath, true, false));
            }

            var binlogs = new List<BinlogFile>();
            foreach (LoggerDestination destination in destinations)
            {
                string destinationPath = ResolveFullPath(destination.Path, workingDirectory);
                // A failed launch may leave an older log at the same path.
                bool isWildcard = destinationPath.Contains("{}", StringComparison.Ordinal);
                string? originalHash = isWildcard ? null : GetExistingFileHash(destinationPath);

                binlogs.Add(new BinlogFile(
                    destinationPath,
                    destination.IsHarnessDestination,
                    originalHash,
                    destination.IncludesProjectImportsZip,
                    isWildcard));
            }

            log.WriteLine(
                $"Test binlog capture: command={commandName}; attempt={attemptId}; "
                + $"workingDirectory='{workingDirectory}'; directory='{diagnosticDirectory}'.");
            foreach (BinlogFile binlog in binlogs)
            {
                log.WriteLine($"Test binlog destination: '{binlog.Path}'.");
            }

            return new TestBinlogCapture(diagnosticDirectory, binlogs, log);
        }

        public void Complete(int exitCode)
        {
            if (_binlogs.Count == 0)
            {
                return;
            }

            bool retainedBinlog = false;

            foreach (BinlogFile binlog in _binlogs)
            {
                IEnumerable<string> producedPaths = GetProducedBinlogPaths(binlog);

                foreach (string binlogPath in producedPaths)
                {
                    if (!binlog.IsWildcard && !FileHasChanged(binlogPath, binlog.OriginalHash))
                    {
                        continue;
                    }

                    string retainedPath = binlogPath;
                    if (!binlog.IsHarnessDestination)
                    {
                        // Keep the test's file in place; short unique names also avoid archive collisions.
                        retainedPath = Path.Combine(_diagnosticDirectory, "authored", $"{Guid.NewGuid():N}.binlog");
                        CopyRequired(binlogPath, retainedPath, exitCode);
                        _log.WriteLine($"Test binlog archived: '{binlogPath}' -> '{retainedPath}'.");
                    }
                    else
                    {
                        _log.WriteLine($"Test binlog retained: '{retainedPath}'.");
                    }
                    retainedBinlog = true;

                    string importsZipPath = Path.ChangeExtension(binlogPath, ".ProjectImports.zip");
                    if (binlog.IncludesProjectImportsZip && File.Exists(importsZipPath))
                    {
                        // Imports may be unchanged across builds, but each retained binlog still needs its companion.
                        string retainedImportsPath = Path.ChangeExtension(retainedPath, ".ProjectImports.zip");
                        CopyRequired(importsZipPath, retainedImportsPath, exitCode);
                        _log.WriteLine($"Test binlog companion archived: '{importsZipPath}' -> '{retainedImportsPath}'.");
                    }
                }
            }

            if (!retainedBinlog)
            {
                _log.WriteLine("Test binlog capture produced no new binlog; existing files were left untouched.");
            }
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
                    $"The build command exited with code {buildExitCode}, but test binlog retention failed. "
                    + $"Could not copy '{sourcePath}' to '{destinationPath}'.",
                    exception);
            }
        }

        private static bool IsFullyQualifiedPath(string path)
        {
            // Path.IsPathFullyQualified is unavailable on .NET Framework.
            // On Windows, rooted paths such as "\logs" and "C:logs" still depend on the current directory.
            if (Path.DirectorySeparatorChar != '\\')
            {
                return Path.IsPathRooted(path);
            }

            return (path.Length >= 2
                && (path[0] == '\\' || path[0] == '/')
                && (path[1] == '\\' || path[1] == '/'))
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

            StringComparer pathComparer = Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var checkedResponseFilePaths = new HashSet<string>(pathComparer);
            if (HasResponseFile(Path.Combine(workingDirectory, "MSBuild.rsp"), checkedResponseFilePaths)
                || HasDirectoryResponseFile(workingDirectory, checkedResponseFilePaths))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                string? executableDirectory = Path.GetDirectoryName(executablePath);
                if (!string.IsNullOrEmpty(executableDirectory))
                {
                    if (HasResponseFile(
                        Path.Combine(executableDirectory, "MSBuild.rsp"),
                        checkedResponseFilePaths))
                    {
                        return true;
                    }

                    if (Path.GetFileNameWithoutExtension(executablePath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                    {
                        // The host selects the SDK, so conservatively check each installed SDK's response file.
                        string sdkDirectory = Path.Combine(executableDirectory, "sdk");
                        if (Directory.Exists(sdkDirectory))
                        {
                            foreach (string sdkVersionDirectory in Directory.GetDirectories(sdkDirectory))
                            {
                                if (HasResponseFile(
                                    Path.Combine(sdkVersionDirectory, "MSBuild.rsp"),
                                    checkedResponseFilePaths))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            foreach (string argument in arguments)
            {
                string candidate = argument.Trim('"');
                string candidatePath = ResolveFullPath(candidate, workingDirectory);
                string extension = Path.GetExtension(candidatePath);
                if (Directory.Exists(candidatePath)
                    || extension.EndsWith("proj", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase))
                {
                    string? projectDirectory = Directory.Exists(candidatePath)
                        ? candidatePath
                        : Path.GetDirectoryName(candidatePath);
                    if (!string.IsNullOrEmpty(projectDirectory))
                    {
                        if (HasDirectoryResponseFile(projectDirectory, checkedResponseFilePaths))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static IEnumerable<string> GetProducedBinlogPaths(BinlogFile binlog)
        {
            if (!binlog.IsWildcard)
            {
                return [binlog.Path];
            }

            string? directory = Path.GetDirectoryName(binlog.Path);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return [];
            }

            string searchPattern = Path.GetFileName(binlog.Path).Replace("{}", "*");
            return Directory.EnumerateFiles(directory, searchPattern);
        }

        private static bool HasDirectoryResponseFile(string directory, HashSet<string> checkedPaths)
        {
            // MSBuild can discover Directory.Build.rsp above the working or project directory.
            for (DirectoryInfo? current = new DirectoryInfo(directory); current is not null; current = current.Parent)
            {
                if (HasResponseFile(Path.Combine(current.FullName, "Directory.Build.rsp"), checkedPaths))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasResponseFile(string path, HashSet<string> checkedPaths)
        {
            return checkedPaths.Add(path) && File.Exists(path);
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
                        // Keep MSBuild's per-process suffix while separating retries.
                        string attemptPattern = $"{attemptId}-{{}}";
                        arguments[parameterArgumentIndex] = arguments[parameterArgumentIndex].Replace("{}", attemptPattern);
                        parameters = parameters.Replace("{}", attemptPattern);
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

            // BinaryLogger can also be supplied through the general logger option.
            foreach (string option in new[] { "--logger", "/logger", "-logger", "/l", "-l" })
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
                    else if (trimmedParameter.Equals("OmitInitialInfo", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
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

            // A bare /bl uses MSBuild's default filename.
            path ??= "msbuild.binlog";
            return new LoggerDestination(path, false, projectImportsZip);
        }

        private static string? GetExistingFileHash(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return GetFileHash(path);
        }

        private static string GetFileHash(string path)
        {
            using var stream = File.OpenRead(path);
            // Timestamps and lengths alone can miss an overwritten log.
            using var sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(stream));
        }

        private static bool FileHasChanged(string path, string? originalHash)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            return originalHash is null
                || !string.Equals(originalHash, GetFileHash(path), StringComparison.Ordinal);
        }

        private static string ResolveFullPath(string path, string basePath)
        {
            if (Path.DirectorySeparatorChar == '\\'
                && path.Length > 0
                && (path[0] == '\\' || path[0] == '/')
                && (path.Length == 1 || (path[1] != '\\' && path[1] != '/')))
            {
                return Path.GetFullPath(Path.Combine(Path.GetPathRoot(basePath)!, path.TrimStart('\\', '/')));
            }

            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(basePath, path));
        }

        private sealed class BinlogFile
        {
            public string Path { get; }
            public bool IsHarnessDestination { get; }
            public string? OriginalHash { get; }
            public bool IncludesProjectImportsZip { get; }
            public bool IsWildcard { get; }

            public BinlogFile(
                string path,
                bool isHarnessDestination,
                string? originalHash,
                bool includesProjectImportsZip,
                bool isWildcard)
            {
                Path = path;
                IsHarnessDestination = isHarnessDestination;
                OriginalHash = originalHash;
                IncludesProjectImportsZip = includesProjectImportsZip;
                IsWildcard = isWildcard;
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
