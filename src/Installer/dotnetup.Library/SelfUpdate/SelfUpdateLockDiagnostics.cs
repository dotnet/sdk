using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Reports a Windows lock holder best-effort, without changing locks or process state.</summary>
internal static partial class SelfUpdateLockDiagnostics
{
    private const uint ErrorMoreData = 234;
    private const uint MaximumProcessCount = 64;

    public static string Describe(string lockPath)
    {
        try
        {
            if (!OperatingSystem.IsWindows() || !File.Exists(lockPath))
            {
                return string.Empty;
            }

            return DescribeWindows(Path.GetFullPath(lockPath));
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static unsafe string DescribeWindows(string lockPath)
    {
        var sessionKey = stackalloc char[33];
        if (RmStartSession(out var session, 0, sessionKey) != 0)
        {
            return string.Empty;
        }

        try
        {
            fixed (char* fileName = lockPath)
            {
                var resource = fileName;
                if (RmRegisterResources(session, 1, &resource, 0, null, 0, null) != 0)
                {
                    return string.Empty;
                }
            }

            uint count = 0;
            if (RmGetList(session, out var needed, ref count, null, out _) != ErrorMoreData ||
                needed == 0 || needed > MaximumProcessCount)
            {
                return string.Empty;
            }

            var processes = new RestartManagerProcessInfo[(int)needed];
            count = needed;
            fixed (RestartManagerProcessInfo* processBuffer = processes)
            {
                if (RmGetList(session, out _, ref count, processBuffer, out _) != 0 || count > processes.Length)
                {
                    return string.Empty;
                }
            }

            for (var index = 0; index < count; index++)
            {
                var description = DescribeProcess(processes[index].Process);
                if (description.Length != 0)
                {
                    return description;
                }
            }

            return string.Empty;
        }
        finally
        {
            _ = RmEndSession(session);
        }
    }

    internal static string DescribeProcess(RestartManagerUniqueProcess processInfo)
    {
        try
        {
            if (processInfo.ProcessId == 0 || processInfo.ProcessId > int.MaxValue)
            {
                return string.Empty;
            }

            using var process = Process.GetProcessById((int)processInfo.ProcessId);
            var startTime = ((long)processInfo.ProcessStartTime.dwHighDateTime << 32) |
                (uint)processInfo.ProcessStartTime.dwLowDateTime;
            if (process.StartTime.ToFileTimeUtc() != startTime)
            {
                return string.Empty;
            }

            var name = process.ProcessName;
            if (string.IsNullOrWhiteSpace(name) || name.Length > 255 ||
                name.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not (' ' or '.' or '-' or '_')))
            {
                return string.Empty;
            }

            return string.Create(CultureInfo.InvariantCulture, $"Lock holder: {name} (PID {processInfo.ProcessId}).");
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    [LibraryImport("rstrtmgr.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial uint RmStartSession(out uint session, uint flags, char* sessionKey);

    [LibraryImport("rstrtmgr.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial uint RmRegisterResources(uint session, uint fileCount, char** fileNames,
        uint applicationCount, RestartManagerUniqueProcess* applications, uint serviceCount, char** serviceNames);

    [LibraryImport("rstrtmgr.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial uint RmGetList(uint session, out uint needed, ref uint count,
        RestartManagerProcessInfo* processes, out uint rebootReasons);

    [LibraryImport("rstrtmgr.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint RmEndSession(uint session);
}