// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Helper class for Windows-specific PATH management operations.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed partial class WindowsPathHelper : IDisposable
{
    private const string RegistryEnvironmentPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
    private const string PathVariableName = "Path";
    private const int HWND_BROADCAST = 0xffff;
    private const int WM_SETTINGCHANGE = 0x001A;
    private const int SMTO_ABORTIFHUNG = 0x0002;

    private readonly StreamWriter? _logWriter;
    private readonly string? _logFilePath;
    private bool _disposed;

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial IntPtr SendMessageTimeout(
        IntPtr hWnd,
        int Msg,
        IntPtr wParam,
        string lParam,
        int fuFlags,
        int uTimeout,
        out IntPtr lpdwResult);

    /// <summary>
    /// Creates a new instance of WindowsPathHelper with logging enabled.
    /// </summary>
    public WindowsPathHelper()
    {
        try
        {
            string tempPath = Path.GetTempPath();
            string logFileName = $"dotnetup_path_changes_{DateTime.Now:yyyyMMdd}.log";
            _logFilePath = Path.Combine(tempPath, logFileName);
            _logWriter = new StreamWriter(_logFilePath, append: true);
            _logWriter.AutoFlush = true;
            LogMessage($"=== WindowsPathHelper session started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to create log file for PATH changes.", ex);
        }
    }

    /// <summary>
    /// Logs a message to the log file.
    /// </summary>
    private void LogMessage(string message)
    {
        _logWriter?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            LogMessage($"=== WindowsPathHelper session ended at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            _logWriter?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Reads the machine-wide (system) PATH environment variable from the registry.
    /// </summary>
    /// <param name="expand">If true, expands environment variables in the PATH value.</param>
    public static string ReadSystemPath(bool expand = false)
    {
        using var key = Registry.LocalMachine.OpenSubKey(RegistryEnvironmentPath, writable: false);
        if (key == null)
        {
            throw new InvalidOperationException("Unable to open registry key for environment variables.");
        }

        var pathValue = key.GetValue(PathVariableName, null, expand ? RegistryValueOptions.None : RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        return pathValue ?? string.Empty;
    }

    /// <summary>
    /// Writes the machine-wide (system) PATH environment variable to the registry.
    /// </summary>
    public static void WriteSystemPath(string path)
    {
        using var key = Registry.LocalMachine.OpenSubKey(RegistryEnvironmentPath, writable: true);
        if (key == null)
        {
            throw new InvalidOperationException("Unable to open registry key for environment variables. Administrator privileges required.");
        }

        key.SetValue(PathVariableName, path, RegistryValueKind.ExpandString);
    }

    /// <summary>
    /// Reads the user PATH environment variable from the registry.
    /// </summary>
    /// <param name="expand">If true, expands environment variables in the PATH value.</param>
    public static string ReadUserPath(bool expand = false)
    {
        using var key = Registry.CurrentUser.OpenSubKey("Environment", writable: false);
        if (key == null)
        {
            return string.Empty;
        }

        var pathValue = key.GetValue(PathVariableName, null, expand ? RegistryValueOptions.None : RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        return pathValue ?? string.Empty;
    }

    /// <summary>
    /// Writes the user PATH environment variable to the registry.
    /// </summary>
    public static void WriteUserPath(string path)
    {
        using var key = Registry.CurrentUser.OpenSubKey("Environment", writable: true);
        if (key == null)
        {
            throw new InvalidOperationException("Unable to open registry key for user environment variables.");
        }

        key.SetValue(PathVariableName, path, RegistryValueKind.ExpandString);
    }

    /// <summary>
    /// Gets the default Program Files dotnet installation path(s) by reading from the registry.
    /// For each architecture subkey under HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\{arch}:
    ///   1. First checks {arch}\sharedhost → Path value (preferred, set by the .NET host installer)
    ///   2. Falls back to {arch} → InstallLocation value
    /// If the registry yields no results, falls back to %ProgramFiles%\dotnet if it exists.
    /// See https://github.com/dotnet/designs/blob/main/accepted/2021/install-location-per-architecture.md
    /// See https://github.com/dotnet/runtime/issues/109974
    /// </summary>
    public static List<string> GetProgramFilesDotnetPaths()
    {
        var paths = new List<string>();

        // Read from registry to find actual dotnet installations.
        // Use 32-bit registry hive to ensure we get the correct view on WoW64.
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        using var key = baseKey.OpenSubKey(@"SOFTWARE\dotnet\Setup\InstalledVersions");
        if (key != null)
        {
            foreach (var archName in key.GetSubKeyNames())
            {
                using var archKey = key.OpenSubKey(archName);
                if (archKey == null)
                {
                    continue;
                }

                // Prefer sharedhost\Path — this is the canonical location set by the host installer.
                string? installPath = null;
                using (var sharedHostKey = archKey.OpenSubKey("sharedhost"))
                {
                    installPath = sharedHostKey?.GetValue("Path") as string;
                }

                // Fallback to the InstallLocation value on the architecture key.
                installPath ??= archKey.GetValue("InstallLocation") as string;

                if (!string.IsNullOrEmpty(installPath))
                {
                    var normalized = installPath.TrimEnd(Path.DirectorySeparatorChar);
                    if (Directory.Exists(normalized) && !paths.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    {
                        paths.Add(normalized);
                    }
                }
            }
        }

        // Fallback: if registry yielded nothing, check the default Program Files location.
        if (paths.Count == 0)
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles))
            {
                var defaultPath = Path.Combine(programFiles, "dotnet");
                if (Directory.Exists(defaultPath))
                {
                    paths.Add(defaultPath);
                }
            }
        }

        return paths;
    }

    /// <summary>
    /// Splits a PATH string into entries.
    /// Uses EnvironmentProvider's SplitPaths for proper processing.
    /// </summary>
    public static List<string> SplitPath(string path)
    {
        var envProvider = new Microsoft.DotNet.Cli.Utils.EnvironmentProvider();
        return [.. envProvider.SplitPaths(path)];
    }

    /// <summary>
    /// Verifies that the PATH-editing helpers can safely map entries between the expanded and
    /// unexpanded PATH by index. Those helpers detect entries against the expanded PATH but apply
    /// the edit to the unexpanded PATH by position, which is only valid when the two lists are
    /// element-aligned. Alignment holds exactly when every unexpanded entry expands to a single
    /// non-empty segment. A variable that expands to a value containing ';' (adding entries) or to
    /// nothing (dropping an entry) breaks that invariant, so rather than silently corrupt the PATH
    /// we fail loudly.
    /// </summary>
    /// <param name="unexpandedEntries">The unexpanded PATH split into entries.</param>
    private static void EnsureExpandedPathAligned(List<string> unexpandedEntries)
    {
        foreach (var entry in unexpandedEntries)
        {
            if (SplitPath(Environment.ExpandEnvironmentVariables(entry)).Count != 1)
            {
                throw new InvalidOperationException(
                    $"Cannot safely modify PATH: the entry '{entry}' expands to a value that is empty or " +
                    "contains ';', changing the number of ';'-separated entries. Entries cannot be reliably " +
                    "matched between the expanded and unexpanded PATH.");
            }
        }
    }

    /// <summary>
    /// Finds the indices of entries in a PATH that match the specified paths.
    /// This method is designed for unit testing without registry access.
    /// </summary>
    /// <param name="pathEntries">The list of PATH entries to search.</param>
    /// <param name="programFilesDotnetPaths">The list of paths to match.</param>
    /// <returns>A list of indices where paths were found.</returns>
    public static List<int> FindPathIndices(List<string> pathEntries, List<string> programFilesDotnetPaths)
    {
        var indices = new List<int>();
        for (int i = 0; i < pathEntries.Count; i++)
        {
            var normalizedEntry = Path.TrimEndingDirectorySeparator(pathEntries[i]);
            if (programFilesDotnetPaths.Any(pfPath =>
                normalizedEntry.Equals(Path.TrimEndingDirectorySeparator(pfPath), StringComparison.OrdinalIgnoreCase)))
            {
                indices.Add(i);
            }
        }
        return indices;
    }

    /// <summary>
    /// Removes entries at the specified indices from a PATH string.
    /// This method is designed for unit testing without registry access.
    /// </summary>
    /// <param name="path">The PATH string to modify.</param>
    /// <param name="indicesToRemove">The indices of entries to remove.</param>
    /// <returns>The modified PATH string with entries removed.</returns>
    public static string RemovePathEntriesByIndices(string path, List<int> indicesToRemove)
    {
        if (indicesToRemove.Count == 0)
        {
            return path;
        }

        var pathEntries = SplitPath(path);
        var indicesToRemoveSet = new HashSet<int>(indicesToRemove);

        var filteredEntries = pathEntries
            .Where((entry, index) => !indicesToRemoveSet.Contains(index))
            .ToList();

        return string.Join(';', filteredEntries);
    }

    /// <summary>
    /// Checks if a PATH contains any Program Files dotnet paths.
    /// This method is designed for unit testing without registry access.
    /// </summary>
    /// <param name="pathEntries">The list of PATH entries to check.</param>
    /// <param name="programFilesDotnetPaths">The list of Program Files dotnet paths to match.</param>
    /// <returns>True if any dotnet path is found, false otherwise.</returns>
    public static bool PathContainsDotnet(List<string> pathEntries, List<string> programFilesDotnetPaths)
    {
        return FindPathIndices(pathEntries, programFilesDotnetPaths).Count > 0;
    }

    /// <summary>
    /// Adds a path entry to the given PATH strings if it's not already present.
    /// Uses the expanded PATH for detection but modifies the unexpanded PATH to preserve environment variables.
    /// If the command already resolves to the pathToAdd, no changes are made.
    /// If the path is already present but the command doesn't resolve to it, moves it to the front.
    /// </summary>
    /// <param name="unexpandedPath">The unexpanded PATH string to modify.</param>
    /// <param name="expandedPath">The expanded PATH string to use for detection.</param>
    /// <param name="pathToAdd">The path to add.</param>
    /// <param name="commandName">The command name that should resolve from pathToAdd (e.g., "dotnet").</param>
    /// <returns>The modified unexpanded PATH string.</returns>
    public static string AddPathEntry(string unexpandedPath, string expandedPath, string pathToAdd, string commandName)
    {
        var expandedEntries = SplitPath(expandedPath);
        var unexpandedEntries = SplitPath(unexpandedPath);
        EnsureExpandedPathAligned(unexpandedEntries);

        // Check if the command already resolves to the pathToAdd using EnvironmentProvider
        var envProvider = new Microsoft.DotNet.Cli.Utils.EnvironmentProvider(searchPathsOverride: expandedEntries);
        var resolvedCommandPath = envProvider.GetCommandPath(commandName);

        var normalizedPathToAdd = Path.TrimEndingDirectorySeparator(pathToAdd);

        if (resolvedCommandPath != null)
        {
            var normalizedResolvedDir = Path.TrimEndingDirectorySeparator(
                ExecutablePathResolver.ResolveRealDirectory(resolvedCommandPath) ?? string.Empty);

            if (normalizedResolvedDir.Equals(normalizedPathToAdd, StringComparison.OrdinalIgnoreCase))
            {
                // Command already resolves to the pathToAdd, no changes needed
                return unexpandedPath;
            }
        }

        // Check if pathToAdd is already in the expanded PATH
        int existingIndex = expandedEntries.FindIndex(expandedEntry =>
            Path.TrimEndingDirectorySeparator(expandedEntry).Equals(normalizedPathToAdd, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            // Path already exists - only move it to the front if the command doesn't already resolve to it
            // (we know it doesn't resolve to pathToAdd from the check above, so move it to front)
            string unexpandedEntry = unexpandedEntries[existingIndex];
            unexpandedEntries.RemoveAt(existingIndex);
            unexpandedEntries.Insert(0, unexpandedEntry);
            return string.Join(';', unexpandedEntries);
        }
        else
        {
            // Add to the beginning of the unexpanded PATH
            unexpandedEntries.Insert(0, pathToAdd);
            return string.Join(';', unexpandedEntries);
        }
    }

    /// <summary>
    /// Inserts a path entry into the given PATH strings immediately before the machine-wide
    /// Program Files dotnet entry. When no Program Files dotnet entry is present, the path is
    /// appended to the end instead, so that a machine-wide install added later lands after the
    /// inserted entry, which therefore keeps precedence.
    /// Uses the expanded PATH for detection but modifies the unexpanded PATH to preserve
    /// environment variables. If the entry is already positioned ahead of the Program Files
    /// dotnet entry (or is already present when there is no Program Files dotnet entry), the PATH
    /// is returned unchanged.
    /// </summary>
    /// <param name="unexpandedPath">The unexpanded PATH string to modify.</param>
    /// <param name="expandedPath">The expanded PATH string to use for detection.</param>
    /// <param name="pathToInsert">The path to insert (e.g. the user's dotnet directory).</param>
    /// <returns>The modified unexpanded PATH string.</returns>
    public static string InsertPathEntryBeforeProgramFilesDotnet(string unexpandedPath, string expandedPath, string pathToInsert)
    {
        var programFilesDotnetPaths = GetProgramFilesDotnetPaths();
        return InsertPathEntryBeforeDotnet(unexpandedPath, expandedPath, pathToInsert, programFilesDotnetPaths);
    }

    /// <summary>
    /// Testable core of <see cref="InsertPathEntryBeforeProgramFilesDotnet"/> that takes the
    /// machine-wide dotnet paths as an argument instead of reading them from the registry.
    /// </summary>
    /// <param name="unexpandedPath">The unexpanded PATH string to modify.</param>
    /// <param name="expandedPath">The expanded PATH string to use for detection.</param>
    /// <param name="pathToInsert">The path to insert.</param>
    /// <param name="programFilesDotnetPaths">The machine-wide dotnet paths to position ahead of.</param>
    /// <returns>The modified unexpanded PATH string.</returns>
    public static string InsertPathEntryBeforeDotnet(
        string unexpandedPath,
        string expandedPath,
        string pathToInsert,
        List<string> programFilesDotnetPaths)
    {
        var expandedEntries = SplitPath(expandedPath);
        var unexpandedEntries = SplitPath(unexpandedPath);
        EnsureExpandedPathAligned(unexpandedEntries);

        var normalizedPathToInsert = Path.TrimEndingDirectorySeparator(pathToInsert);

        var programFilesIndices = FindPathIndices(expandedEntries, programFilesDotnetPaths);
        int firstProgramFilesIndex = programFilesIndices.Count > 0 ? programFilesIndices.Min() : -1;

        int existingIndex = expandedEntries.FindIndex(expandedEntry =>
            Path.TrimEndingDirectorySeparator(expandedEntry).Equals(normalizedPathToInsert, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            // Already positioned ahead of the Program Files dotnet entry (or already present when
            // there is no Program Files dotnet entry at all) — the inserted entry already wins.
            if (firstProgramFilesIndex < 0 || existingIndex < firstProgramFilesIndex)
            {
                return unexpandedPath;
            }

            // Present, but after the Program Files dotnet entry. Remove it so it can be
            // re-inserted immediately before that entry. It sits after firstProgramFilesIndex, so
            // removing it does not shift that index.
            unexpandedEntries.RemoveAt(existingIndex);
        }

        if (firstProgramFilesIndex >= 0)
        {
            unexpandedEntries.Insert(firstProgramFilesIndex, pathToInsert);
        }
        else
        {
            unexpandedEntries.Add(pathToInsert);
        }

        return string.Join(';', unexpandedEntries);
    }

    /// <summary>
    /// Removes a specific path entry from the given PATH strings.
    /// Uses the expanded PATH for detection but modifies the unexpanded PATH to preserve environment variables.
    /// </summary>
    /// <param name="unexpandedPath">The unexpanded PATH string to modify.</param>
    /// <param name="expandedPath">The expanded PATH string to use for detection.</param>
    /// <param name="pathsToRemove">The paths to remove.</param>
    /// <returns>The modified unexpanded PATH string.</returns>
    public static string RemovePathEntries(string unexpandedPath, string expandedPath, List<string> pathsToRemove)
    {
        var expandedEntries = SplitPath(expandedPath);
        var unexpandedEntries = SplitPath(unexpandedPath);
        EnsureExpandedPathAligned(unexpandedEntries);

        // Find indices to remove using the expanded path
        var indicesToRemove = FindPathIndices(expandedEntries, pathsToRemove);

        // Remove those indices from the unexpanded path
        return RemovePathEntriesByIndices(unexpandedPath, indicesToRemove);
    }

    /// <summary>
    /// Checks if the system PATH contains the Program Files dotnet path.
    /// Uses the expanded PATH for accurate detection.
    /// </summary>
    public static bool SystemPathContainsProgramFilesDotnet()
    {
        return SystemPathContainsProgramFilesDotnet(out _);
    }

    /// <summary>
    /// Checks if the system PATH contains the Program Files dotnet path.
    /// Uses the expanded PATH for accurate detection.
    /// </summary>
    /// <param name="foundDotnetPaths">The list of dotnet paths found in the system PATH.</param>
    /// <returns>True if any dotnet path is found, false otherwise.</returns>
    public static bool SystemPathContainsProgramFilesDotnet(out List<string> foundDotnetPaths)
    {
        var systemPath = ReadSystemPath(expand: true);
        var pathEntries = SplitPath(systemPath);
        var programFilesDotnetPaths = GetProgramFilesDotnetPaths();

        foundDotnetPaths = [];
        var indices = FindPathIndices(pathEntries, programFilesDotnetPaths);

        foreach (var index in indices)
        {
            foundDotnetPaths.Add(pathEntries[index]);
        }

        return foundDotnetPaths.Count > 0;
    }

    /// <summary>
    /// Inserts the dotnet directory into the system PATH immediately before the machine-wide
    /// Program Files dotnet entry (or appends it to the end when there is no machine-wide dotnet on
    /// the system PATH). This is the main orchestrating method that should be called by commands.
    /// </summary>
    /// <param name="dotnetDir">The dotnet directory to insert. Must be supplied by the
    /// caller because this can run in an elevated child process under a different account, where the
    /// current user's directory cannot be recomputed.</param>
    /// <returns>0 on success, 1 on failure.</returns>
    public int InsertDotnetIntoSystemPath(string dotnetDir)
    {
        try
        {
            LogMessage($"Starting InsertDotnetIntoSystemPath operation for {dotnetDir}");

            string unexpandedPath = ReadSystemPath(expand: false);
            string expandedPath = ReadSystemPath(expand: true);
            LogMessage($"Old PATH (unexpanded): {unexpandedPath}");

            string newPath = InsertPathEntryBeforeProgramFilesDotnet(unexpandedPath, expandedPath, dotnetDir);
            if (string.Equals(newPath, unexpandedPath, StringComparison.Ordinal))
            {
                LogMessage("No changes needed - dotnet directory already positioned ahead of Program Files dotnet");
                return 0;
            }

            LogMessage($"New PATH (unexpanded): {newPath}");
            WriteSystemPath(newPath);
            LogMessage("PATH written to registry");

            BroadcastEnvironmentChange();

            LogMessage("InsertDotnetIntoSystemPath operation completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Failed to insert dotnet directory into system PATH: {ex.Message}");
            LogMessage($"ERROR: {ex.ToString()}");
            return 1;
        }
    }

    /// <summary>
    /// Removes the dotnet directory from the system PATH. This undoes
    /// <see cref="InsertDotnetIntoSystemPath"/> and is the main orchestrating method that
    /// should be called by commands.
    /// </summary>
    /// <param name="dotnetDir">The dotnet directory to remove. Must be supplied by the
    /// caller because this can run in an elevated child process under a different account.</param>
    /// <returns>0 on success, 1 on failure.</returns>
    public int RemoveDotnetFromSystemPath(string dotnetDir)
    {
        try
        {
            LogMessage($"Starting RemoveDotnetFromSystemPath operation for {dotnetDir}");

            string unexpandedPath = ReadSystemPath(expand: false);
            string expandedPath = ReadSystemPath(expand: true);
            LogMessage($"Old PATH (unexpanded): {unexpandedPath}");

            string newPath = RemovePathEntries(unexpandedPath, expandedPath, [dotnetDir]);
            if (string.Equals(newPath, unexpandedPath, StringComparison.Ordinal))
            {
                LogMessage("No changes needed - dotnet directory not present on system PATH");
                return 0;
            }

            LogMessage($"New PATH (unexpanded): {newPath}");
            WriteSystemPath(newPath);
            LogMessage("PATH written to registry");

            BroadcastEnvironmentChange();

            LogMessage("RemoveDotnetFromSystemPath operation completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Failed to remove dotnet directory from system PATH: {ex.Message}");
            LogMessage($"ERROR: {ex.ToString()}");
            return 1;
        }
    }

    /// <summary>
    /// Broadcasts a WM_SETTINGCHANGE message to notify other applications that the environment has changed.
    /// </summary>
    private void BroadcastEnvironmentChange()
    {
        try
        {
            SendMessageTimeout(
                new IntPtr(HWND_BROADCAST),
                WM_SETTINGCHANGE,
                IntPtr.Zero,
                "Environment",
                SMTO_ABORTIFHUNG,
                5000,
                out IntPtr result);

            LogMessage("Environment change notification broadcasted");
        }
        catch (Exception ex)
        {
            LogMessage($"WARNING: Failed to broadcast environment change: {ex.ToString()}");
        }
    }

    /// <summary>
    /// Starts an elevated process to modify the system PATH and waits for it to complete.
    /// </summary>
    /// <param name="operation">The elevated operation to perform (<c>insertdotnet</c>/<c>removedotnet</c>).</param>
    /// <param name="dotnetDir">The dotnet directory the elevated process should insert into or remove
    /// from the system PATH. This must be supplied by the caller because the elevated process can run
    /// under a different account than the invoking user — when a standard user elevates by supplying
    /// an administrator's credentials, the child runs as that administrator, whose per-user directory
    /// (e.g. %LOCALAPPDATA%) differs — so the invoking user's directory cannot be recomputed there.</param>
    /// <exception cref="DotnetInstallException">Thrown when the user declines the UAC elevation prompt.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the process cannot be started or returns a non-zero exit code.</exception>
    public static void StartElevatedProcess(string operation, string dotnetDir)
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath))
        {
            throw new InvalidOperationException("Unable to determine current process path.");
        }

        //  We can't capture output directly from an elevated process, so we pass it a filename where
        //  it should write any output that should be displayed
        var tempDirectory = Directory.CreateTempSubdirectory("dotnetup_elevated");
        string outputFilePath = Path.Combine(tempDirectory.FullName, "output.txt");

        try
        {
            string arguments = $"elevatedsystempath {operation} \"{outputFilePath}\" --dotnet-dir \"{dotnetDir}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = processPath,
                Arguments = arguments,
                Verb = "runas", // This triggers UAC elevation
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            RunElevatedProcessCore(startInfo);
        }
        finally
        {
            DisplayElevatedProcessOutput(outputFilePath);

            // Clean up temporary directory
            try
            {
                tempDirectory.Delete(recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Starts the elevated process described by <paramref name="startInfo"/>, waits for it to exit,
    /// and validates the exit code.
    /// </summary>
    /// <exception cref="DotnetInstallException">Thrown when the user cancels the UAC prompt.</exception>
    private static void RunElevatedProcessCore(ProcessStartInfo startInfo)
    {
        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start elevated process.");
            }

            process.WaitForExit();

            if (process.ExitCode == -2147450730)
            {
                //  NOTE: Process exit code -2147450730 means that the right .NET runtime could not be found
                //  This should not happen when using NativeAOT dotnetup, but when testing using IL it can happen and
                //  can be caused if DOTNET_ROOT has been set to a path that doesn't have the right runtime to run dotnetup.
                throw new InvalidOperationException("Elevated process failed: Unable to find matching .NET Runtime." + Environment.NewLine +
                    "This is probably because dotnetup is not being run as self-contained and DOTNET_ROOT is set to a path that doesn't have a matching runtime.");
            }
            else if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Elevated process returned exit code {process.ExitCode}");
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // User cancelled UAC prompt or elevation failed
            // ERROR_CANCELLED = 1223
            if (ex.NativeErrorCode == 1223)
            {
                // Surface the declined prompt as a clean, user-category error (no stack trace)
                // instead of returning a status that every caller would have to re-wrap.
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.PermissionDenied,
                    Strings.EnvElevationCancelled);
            }
            throw;
        }
    }

    /// <summary>
    /// Reads and displays any output written by the elevated process to <paramref name="outputFilePath"/>.
    /// </summary>
    private static void DisplayElevatedProcessOutput(string outputFilePath)
    {
        if (File.Exists(outputFilePath))
        {
            string outputContent = File.ReadAllText(outputFilePath);
            if (!string.IsNullOrEmpty(outputContent))
            {
                Console.WriteLine(outputContent);
            }
        }
    }
}
