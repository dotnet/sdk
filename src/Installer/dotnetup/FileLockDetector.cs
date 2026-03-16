// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Detects which processes are holding file locks, preventing deletion.
/// Uses the RestartManager API on Windows and lsof/fuser on Unix.
/// </summary>
internal static partial class FileLockDetector
{
    /// <summary>
    /// Attempts to find process names that are locking files in the given directory.
    /// Returns a human-readable string describing the locking processes, or null if
    /// detection is not available or no locking processes were found.
    /// </summary>
    public static string? GetLockingProcessDescription(string directoryPath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return GetLockingProcessesWindows(directoryPath);
            }
            else
            {
                return GetLockingProcessesUnix(directoryPath);
            }
        }
        catch
        {
            // Best-effort — don't let lock detection failures affect GC
            return null;
        }
    }

    private static unsafe string? GetLockingProcessesWindows(string directoryPath)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
        }
        catch
        {
            return null;
        }

        if (files.Length == 0)
        {
            return null;
        }

        int error = RmStartSession(out uint sessionHandle, 0, Guid.NewGuid().ToString());
        if (error != 0)
        {
            return null;
        }

        try
        {
            error = RmRegisterResources(sessionHandle, (uint)files.Length, files, 0, IntPtr.Zero, 0, IntPtr.Zero);
            if (error != 0)
            {
                return null;
            }

            uint needed = 0;
            uint count = 0;
            error = RmGetList(sessionHandle, out needed, ref count, null, out _);
            if (needed == 0)
            {
                return null;
            }

            // ERROR_MORE_DATA (234) is expected when the buffer is too small
            if (error is not 0 and not 234)
            {
                return null;
            }

            count = needed;
            RM_PROCESS_INFO[] infos = new RM_PROCESS_INFO[count];
            fixed (RM_PROCESS_INFO* ptr = infos)
            {
                error = RmGetList(sessionHandle, out needed, ref count, ptr, out _);
            }

            if (error != 0)
            {
                return null;
            }

            var descriptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < count; i++)
            {
                string appName;
                fixed (char* namePtr = infos[i].strAppName)
                {
                    appName = new string(namePtr);
                }

                descriptions.Add($"{appName} (PID {infos[i].Process.dwProcessId})");
            }

            return descriptions.Count > 0
                ? string.Join(", ", descriptions)
                : null;
        }
        finally
        {
            RmEndSession(sessionHandle);
        }
    }

    private static string? GetLockingProcessesUnix(string directoryPath)
    {
        var result = RunProcess("lsof", $"+D \"{directoryPath}\"")
                  ?? RunProcess("fuser", $"\"{directoryPath}\"");

        if (string.IsNullOrWhiteSpace(result))
        {
            return null;
        }

        // Parse lsof output: COMMAND PID USER FD TYPE DEVICE SIZE/OFF NODE NAME
        var processNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in result.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && !parts[0].Equals("COMMAND", StringComparison.OrdinalIgnoreCase))
            {
                processNames.Add($"{parts[0]} (PID {parts[1]})");
            }
        }

        return processNames.Count > 0
            ? string.Join(", ", processNames)
            : null;
    }

    private static string? RunProcess(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(TimeSpan.FromSeconds(5));
            return output;
        }
        catch
        {
            return null;
        }
    }

    // RestartManager P/Invoke declarations

    [LibraryImport("rstrtmgr.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int RmStartSession(out uint pSessionHandle, uint dwSessionFlags, string strSessionKey);

    [LibraryImport("rstrtmgr.dll")]
    private static partial int RmEndSession(uint dwSessionHandle);

    [LibraryImport("rstrtmgr.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int RmRegisterResources(
        uint dwSessionHandle,
        uint nFiles,
        string[] rgsFileNames,
        uint nApplications,
        IntPtr rgApplications,
        uint nServices,
        IntPtr rgsServiceNames);

    [LibraryImport("rstrtmgr.dll")]
    private static unsafe partial int RmGetList(
        uint dwSessionHandle,
        out uint pnProcInfoNeeded,
        ref uint pnProcInfo,
        RM_PROCESS_INFO* rgAffectedApps,
        out uint lpdwRebootReasons);

    [StructLayout(LayoutKind.Sequential)]
    private struct RM_UNIQUE_PROCESS
    {
        public uint dwProcessId;
        public long ProcessStartTime; // FILETIME
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private unsafe struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;
        public fixed char strAppName[256];
        public fixed char strServiceShortName[64];
        public int ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        public int bRestartable;
    }
}
