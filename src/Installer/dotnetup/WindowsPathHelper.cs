// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Helper class for Windows-specific PATH management operations.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsPathHelper : IDisposable
{
    private const string RegistryEnvironmentPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
    private const string PathVariableName = "Path";
    private const int HWND_BROADCAST = 0xffff;
    private const int WM_SETTINGCHANGE = 0x001A;
    private const int SMTO_ABORTIFHUNG = 0x0002;

    private readonly StreamWriter? _logWriter;
    private readonly string? _logFilePath;
    private bool _disposed;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
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
    /// Reads the machine-wide PATH environment variable from the registry.
    /// </summary>
    /// <param name="expand">If true, expands environment variables in the PATH value.</param>
    public static string ReadAdminPath(bool expand = false)
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
    /// Writes the machine-wide PATH environment variable to the registry.
    /// </summary>
    public static void WriteAdminPath(string path)
    {
        using var key = Registry.LocalMachine.OpenSubKey(RegistryEnvironmentPath, writable: true);
        if (key == null)
        {
            throw new InvalidOperationException("Unable to open registry key for environment variables. Administrator privileges required.");
        }

        key.SetValue(PathVariableName, path, RegistryValueKind.ExpandString);
    }

    /// <summary>
    /// Gets the default Program Files dotnet installation path(s).
    /// </summary>
    public static List<string> GetProgramFilesDotnetPaths()
    {
        var paths = new List<string>();

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles))
        {
            paths.Add(Path.Combine(programFiles, "dotnet"));
        }

        // On 64-bit Windows, also check Program Files (x86)
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(programFilesX86) && !programFilesX86.Equals(programFiles, StringComparison.OrdinalIgnoreCase))
        {
            paths.Add(Path.Combine(programFilesX86, "dotnet"));
        }

        return paths;
    }

    /// <summary>
    /// Splits a PATH string into entries.
    /// </summary>
    private static List<string> SplitPath(string path)
    {
        return path.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    /// <summary>
    /// Finds the indices of entries in a PATH that match the Program Files dotnet paths.
    /// This method is designed for unit testing without registry access.
    /// </summary>
    /// <param name="pathEntries">The list of PATH entries to search.</param>
    /// <param name="programFilesDotnetPaths">The list of Program Files dotnet paths to match.</param>
    /// <returns>A list of indices where dotnet paths were found.</returns>
    public static List<int> FindDotnetPathIndices(List<string> pathEntries, List<string> programFilesDotnetPaths)
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
        return FindDotnetPathIndices(pathEntries, programFilesDotnetPaths).Count > 0;
    }

    /// <summary>
    /// Removes the Program Files dotnet path from the given PATH strings.
    /// Uses the expanded PATH for detection but modifies the unexpanded PATH to preserve environment variables.
    /// </summary>
    /// <param name="unexpandedPath">The unexpanded PATH string to modify.</param>
    /// <param name="expandedPath">The expanded PATH string to use for detection.</param>
    /// <returns>The modified unexpanded PATH string.</returns>
    public static string RemoveProgramFilesDotnetFromPath(string unexpandedPath, string expandedPath)
    {
        // Find indices to remove using the expanded path
        var expandedEntries = SplitPath(expandedPath);
        var programFilesDotnetPaths = GetProgramFilesDotnetPaths();
        var indicesToRemove = FindDotnetPathIndices(expandedEntries, programFilesDotnetPaths);

        // Remove those indices from the unexpanded path
        return RemovePathEntriesByIndices(unexpandedPath, indicesToRemove);
    }

    /// <summary>
    /// Gets the admin PATH with Program Files dotnet path removed while preserving unexpanded environment variables.
    /// This does not modify the registry, only returns the modified PATH string.
    /// </summary>
    /// <returns>The modified unexpanded PATH string.</returns>
    public static string GetAdminPathWithProgramFilesDotnetRemoved()
    {
        // Read both expanded and unexpanded versions
        string expandedPath = ReadAdminPath(expand: true);
        string unexpandedPath = ReadAdminPath(expand: false);

        return RemoveProgramFilesDotnetFromPath(unexpandedPath, expandedPath);
    }

    /// <summary>
    /// Adds the Program Files dotnet path to the given PATH string if it's not already present.
    /// </summary>
    public static string AddProgramFilesDotnetToPath(string path)
    {
        var pathEntries = SplitPath(path);
        var programFilesDotnetPaths = GetProgramFilesDotnetPaths();

        // Get the primary Program Files dotnet path (non-x86)
        string primaryDotnetPath = programFilesDotnetPaths.FirstOrDefault() ?? string.Empty;
        if (string.IsNullOrEmpty(primaryDotnetPath))
        {
            return path;
        }

        // Check if any Program Files dotnet path is already in PATH
        bool alreadyExists = PathContainsDotnet(pathEntries, programFilesDotnetPaths);

        if (!alreadyExists)
        {
            pathEntries.Insert(0, primaryDotnetPath);
        }

        return string.Join(';', pathEntries);
    }

    /// <summary>
    /// Checks if the admin PATH contains the Program Files dotnet path.
    /// Uses the expanded PATH for accurate detection.
    /// </summary>
    public static bool AdminPathContainsProgramFilesDotnet()
    {
        var adminPath = ReadAdminPath(expand: true);
        var pathEntries = SplitPath(adminPath);
        var programFilesDotnetPaths = GetProgramFilesDotnetPaths();

        return PathContainsDotnet(pathEntries, programFilesDotnetPaths);
    }

    /// <summary>
    /// Removes the Program Files dotnet path from the admin PATH.
    /// This is the main orchestrating method that should be called by commands.
    /// </summary>
    /// <returns>0 on success, 1 on failure.</returns>
    public int RemoveDotnetFromAdminPath()
    {
        try
        {
            LogMessage("Starting RemoveDotnetFromAdminPath operation");
            
            string oldPath = ReadAdminPath(expand: false);
            LogMessage($"Old PATH (unexpanded): {oldPath}");

            if (!AdminPathContainsProgramFilesDotnet())
            {
                LogMessage("No changes needed - dotnet path not found");
                return 0;
            }

            LogMessage("Removing dotnet paths from admin PATH");
            string newPath = GetAdminPathWithProgramFilesDotnetRemoved();
            LogMessage($"New PATH (unexpanded): {newPath}");

            WriteAdminPath(newPath);
            LogMessage("PATH written to registry");

            // Broadcast environment change
            BroadcastEnvironmentChange();

            LogMessage("RemoveDotnetFromAdminPath operation completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Failed to remove dotnet from admin PATH: {ex.Message}");
            LogMessage($"ERROR: {ex.ToString()}");
            return 1;
        }
    }

    /// <summary>
    /// Adds the Program Files dotnet path to the admin PATH.
    /// This is the main orchestrating method that should be called by commands.
    /// </summary>
    /// <returns>0 on success, 1 on failure.</returns>
    public int AddDotnetToAdminPath()
    {
        try
        {
            LogMessage("Starting AddDotnetToAdminPath operation");
            
            string oldPath = ReadAdminPath(expand: false);
            LogMessage($"Old PATH (unexpanded): {oldPath}");

            if (AdminPathContainsProgramFilesDotnet())
            {
                LogMessage("No changes needed - dotnet path already exists");
                return 0;
            }

            LogMessage("Adding dotnet path to admin PATH");
            string newPath = AddProgramFilesDotnetToPath(oldPath);
            LogMessage($"New PATH (unexpanded): {newPath}");

            WriteAdminPath(newPath);
            LogMessage("PATH written to registry");

            // Broadcast environment change
            BroadcastEnvironmentChange();

            LogMessage("AddDotnetToAdminPath operation completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Failed to add dotnet to admin PATH: {ex.Message}");
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
    /// Starts an elevated process with the given arguments.
    /// </summary>
    /// <returns>True if the process succeeded (exit code 0), false if elevation was cancelled.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the process cannot be started or returns a non-zero exit code.</exception>
    public static bool StartElevatedProcess(string arguments)
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath))
        {
            throw new InvalidOperationException("Unable to determine current process path.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            Arguments = arguments,
            Verb = "runas", // This triggers UAC elevation
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start elevated process.");
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Elevated process returned exit code {process.ExitCode}.{Environment.NewLine}" +
                    $"STDOUT: {stdout}{Environment.NewLine}" +
                    $"STDERR: {stderr}");
            }

            return true;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // User cancelled UAC prompt or elevation failed
            // ERROR_CANCELLED = 1223
            if (ex.NativeErrorCode == 1223)
            {
                return false;
            }
            throw;
        }
    }
}
