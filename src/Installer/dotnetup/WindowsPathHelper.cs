// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using Microsoft.Win32;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Helper class for Windows-specific PATH management operations.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsPathHelper
{
    private const string RegistryEnvironmentPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
    private const string PathVariableName = "Path";
    private const int HWND_BROADCAST = 0xffff;
    private const int WM_SETTINGCHANGE = 0x001A;
    private const int SMTO_ABORTIFHUNG = 0x0002;

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
    /// Checks if the current process is running with elevated (administrator) privileges.
    /// </summary>
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Reads the machine-wide PATH environment variable from the registry.
    /// </summary>
    public static string ReadAdminPath()
    {
        using var key = Registry.LocalMachine.OpenSubKey(RegistryEnvironmentPath, writable: false);
        if (key == null)
        {
            throw new InvalidOperationException("Unable to open registry key for environment variables.");
        }

        var pathValue = key.GetValue(PathVariableName) as string;
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
    /// Removes the Program Files dotnet path from the given PATH string.
    /// </summary>
    public static string RemoveProgramFilesDotnetFromPath(string path)
    {
        var pathEntries = path.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        var programFilesDotnetPaths = GetProgramFilesDotnetPaths();

        // Remove entries that match Program Files dotnet paths (case-insensitive)
        pathEntries = pathEntries.Where(entry =>
        {
            var normalizedEntry = Path.TrimEndingDirectorySeparator(entry);
            return !programFilesDotnetPaths.Any(pfPath =>
                normalizedEntry.Equals(Path.TrimEndingDirectorySeparator(pfPath), StringComparison.OrdinalIgnoreCase));
        }).ToList();

        return string.Join(';', pathEntries);
    }

    /// <summary>
    /// Adds the Program Files dotnet path to the given PATH string if it's not already present.
    /// </summary>
    public static string AddProgramFilesDotnetToPath(string path)
    {
        var pathEntries = path.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        var programFilesDotnetPaths = GetProgramFilesDotnetPaths();

        // Get the primary Program Files dotnet path (non-x86)
        string primaryDotnetPath = programFilesDotnetPaths.FirstOrDefault() ?? string.Empty;
        if (string.IsNullOrEmpty(primaryDotnetPath))
        {
            return path;
        }

        // Check if any Program Files dotnet path is already in PATH
        bool alreadyExists = pathEntries.Any(entry =>
        {
            var normalizedEntry = Path.TrimEndingDirectorySeparator(entry);
            return programFilesDotnetPaths.Any(pfPath =>
                normalizedEntry.Equals(Path.TrimEndingDirectorySeparator(pfPath), StringComparison.OrdinalIgnoreCase));
        });

        if (!alreadyExists)
        {
            pathEntries.Insert(0, primaryDotnetPath);
        }

        return string.Join(';', pathEntries);
    }

    /// <summary>
    /// Checks if the admin PATH contains the Program Files dotnet path.
    /// </summary>
    public static bool AdminPathContainsProgramFilesDotnet()
    {
        var adminPath = ReadAdminPath();
        var pathEntries = adminPath.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var programFilesDotnetPaths = GetProgramFilesDotnetPaths();

        return pathEntries.Any(entry =>
        {
            var normalizedEntry = Path.TrimEndingDirectorySeparator(entry);
            return programFilesDotnetPaths.Any(pfPath =>
                normalizedEntry.Equals(Path.TrimEndingDirectorySeparator(pfPath), StringComparison.OrdinalIgnoreCase));
        });
    }

    /// <summary>
    /// Logs PATH changes to a file in the temp directory.
    /// </summary>
    public static void LogPathChange(string operation, string oldPath, string newPath)
    {
        try
        {
            string tempPath = Path.GetTempPath();
            string logFileName = $"dotnetup_path_changes_{DateTime.Now:yyyyMMdd}.log";
            string logFilePath = Path.Combine(tempPath, logFileName);

            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Operation: {operation}{Environment.NewLine}" +
                            $"Old PATH: {oldPath}{Environment.NewLine}" +
                            $"New PATH: {newPath}{Environment.NewLine}" +
                            $"----------------------------------------{Environment.NewLine}";

            File.AppendAllText(logFilePath, logEntry);

            Console.WriteLine($"PATH changes logged to: {logFilePath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to log PATH changes: {ex.Message}");
        }
    }



    /// <summary>
    /// Broadcasts a WM_SETTINGCHANGE message to notify other applications that the environment has changed.
    /// </summary>
    public static void BroadcastEnvironmentChange()
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

            Console.WriteLine("Environment change notification broadcasted.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to broadcast environment change: {ex.Message}");
        }
    }

    /// <summary>
    /// Starts an elevated process with the given arguments.
    /// </summary>
    public static int StartElevatedProcess(string arguments)
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(processPath))
            {
                Console.Error.WriteLine("Error: Unable to determine current process path.");
                return 1;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = processPath,
                Arguments = arguments,
                Verb = "runas", // This triggers UAC elevation
                UseShellExecute = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Console.Error.WriteLine("Error: Failed to start elevated process.");
                return 1;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // User cancelled UAC prompt
            Console.Error.WriteLine($"Error: Elevation cancelled or failed: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Failed to start elevated process: {ex.Message}");
            return 1;
        }
    }
}
