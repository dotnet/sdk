// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable CA1837 // Use 'Environment.ProcessId'
#pragma warning disable CA1416 // Validate platform compatibility

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;

namespace Microsoft.Testing.TestInfrastructure;

public class DebuggerUtility
{
    public static bool AttachCurrentProcessToParentVSProcess(bool enableLog = false) => AttachVSToProcess(Process.GetCurrentProcess().Id, null, enableLog);

    public static bool AttachCurrentProcessToVSProcessPID(int vsProcessPid, bool enableLog = false) => AttachVSToProcess(Process.GetCurrentProcess().Id, vsProcessPid, enableLog);

    private static bool AttachVSToProcess(int? pid, int? vsPid, bool enableLog = false)
    {
        try
        {
            if (pid == null)
            {
                Trace($"FAIL: Pid is null.", enabled: enableLog);
                return false;
            }

            var process = Process.GetProcessById(pid.Value);
            Trace($"Starting with pid '{pid}({process.ProcessName})', and vsPid '{vsPid}'", enabled: enableLog);
            Trace($"Using pid: {pid} to get parent VS.", enabled: enableLog);
            Process vs = GetVsFromPid(Process.GetProcessById(vsPid ?? process.Id));

            if (vs != null)
            {
                Trace($"Parent VS is {vs.ProcessName} ({vs.Id}).", enabled: enableLog);
                AttachTo(process, vs);
                return true;
            }

            Trace($"Parent VS not found, finding the first VS that started.", enabled: enableLog);
            var firstVs = Process.GetProcesses()
                .Where(p => p.ProcessName == "devenv")
                .Select(p =>
                {
                    try
                    {
                        return new { Process = p, p.StartTime, p.HasExited };
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(p => p != null && !p.HasExited)
                .OrderBy(p => p!.StartTime)
                .FirstOrDefault();

            if (firstVs != null)
            {
                Trace($"Found VS {firstVs.Process.Id}", enabled: enableLog);
                AttachTo(process, firstVs.Process);
                return true;
            }

            Trace("Could not find any started VS.", enabled: enableLog);
        }
        catch (Exception ex)
        {
            Trace($"ERROR: {ex}, {ex.StackTrace}", enabled: enableLog);
        }

        return false;
    }

    private static void AttachTo(Process process, Process vs, bool enableLog = false)
    {
        bool attached = AttachVs(vs, process.Id);
        if (attached)
        {
            // You won't see this in DebugView++ because at this point VS is already attached and all the output goes into Debug window in VS.
            Trace($"SUCCESS: Attached process: {process.ProcessName} ({process.Id})", enabled: enableLog);
        }
        else
        {
            Trace($"FAIL: Could not attach process: {process.ProcessName} ({process.Id})", enabled: enableLog);
        }
    }

    private static bool AttachVs(Process vs, int pid, bool enableLog = false)
    {
        IBindCtx bindCtx = null;
        IRunningObjectTable runningObjectTable = null;
        IEnumMoniker enumMoniker = null;
        try
        {
#pragma warning disable IL2050
            int r = CreateBindCtx(0, out bindCtx);
#pragma warning restore IL2050
            Marshal.ThrowExceptionForHR(r);
            if (bindCtx == null)
            {
                Trace($"BindCtx is null. Cannot attach VS.", enabled: enableLog);
                return false;
            }

            bindCtx.GetRunningObjectTable(out runningObjectTable);
            if (runningObjectTable == null)
            {
                Trace($"RunningObjectTable is null. Cannot attach VS.", enabled: enableLog);
                return false;
            }

            runningObjectTable.EnumRunning(out enumMoniker);
            if (enumMoniker == null)
            {
                Trace($"EnumMoniker is null. Cannot attach VS.", enabled: enableLog);
                return false;
            }

            string dteSuffix = ":" + vs.Id;

            var moniker = new IMoniker[1];
            while (enumMoniker.Next(1, moniker, IntPtr.Zero) == 0 && moniker[0] != null)
            {
                moniker[0].GetDisplayName(bindCtx, null, out string dn);

                if (dn.StartsWith("!VisualStudio.DTE.", StringComparison.Ordinal) && dn.EndsWith(dteSuffix, StringComparison.Ordinal))
                {
                    object dbg, lps;
                    runningObjectTable.GetObject(moniker[0], out object dte);

                    // The COM object can be busy, we retry few times, hoping that it won't be busy next time.
                    for (int i = 0; i < 10; i++)
                    {
                        try
                        {
                            dbg = dte.GetType().InvokeMember("Debugger", BindingFlags.GetProperty, null, dte, null, CultureInfo.InvariantCulture)!;
                            lps = dbg.GetType().InvokeMember("LocalProcesses", BindingFlags.GetProperty, null, dbg, null, CultureInfo.InvariantCulture)!;
                            var lpn = (System.Collections.IEnumerator)lps.GetType().InvokeMember("GetEnumerator", BindingFlags.InvokeMethod, null, lps, null, CultureInfo.InvariantCulture)!;

                            while (lpn.MoveNext())
                            {
                                int pn = Convert.ToInt32(lpn.Current.GetType().InvokeMember("ProcessID", BindingFlags.GetProperty, null, lpn.Current, null, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

                                if (pn == pid)
                                {
                                    lpn.Current.GetType().InvokeMember("Attach", BindingFlags.InvokeMethod, null, lpn.Current, null, CultureInfo.InvariantCulture);
                                    return true;
                                }
                            }
                        }

                        // Catch the exception if it is COMException coming directly, or coming from methodInvocation, otherwise just let it be.
                        catch (Exception ex) when (ex is COMException or TargetInvocationException { InnerException: COMException })
                        {
                            Trace($"ComException: Retrying in 250ms.\n{ex}", enabled: enableLog);
                            Thread.Sleep(250);
                        }
                    }

                    Marshal.ReleaseComObject(moniker[0]);

                    break;
                }

                Marshal.ReleaseComObject(moniker[0]);
            }

            return false;
        }
        finally
        {
            if (enumMoniker != null)
            {
                try
                {
                    Marshal.ReleaseComObject(enumMoniker);
                }
                catch
                {
                }
            }

            if (runningObjectTable != null)
            {
                try
                {
                    Marshal.ReleaseComObject(runningObjectTable);
                }
                catch
                {
                }
            }

            if (bindCtx != null)
            {
                try
                {
                    Marshal.ReleaseComObject(bindCtx);
                }
                catch
                {
                }
            }
        }
    }

    private static Process GetVsFromPid(Process process)
    {
        Process parent = process;
        while (!IsVsOrNull(parent))
        {
            parent = GetParentProcess(parent);
        }

        return parent;
    }

    private static bool IsVsOrNull([NotNullWhen(false)] Process process, bool enableLog = false)
    {
        if (process == null)
        {
            Trace("Parent process is null..", enabled: enableLog);
            return true;
        }

        bool isVs = process.ProcessName.Equals("devenv", StringComparison.OrdinalIgnoreCase);
        if (isVs)
        {
            Trace($"Process {process.ProcessName} ({process.Id}) is VS.", enabled: enableLog);
        }
        else
        {
            Trace($"Process {process.ProcessName} ({process.Id}) is not VS.", enabled: enableLog);
        }

        return isVs;
    }

    private static bool IsCorrectParent(Process currentProcess, Process parent, bool enableLog = false)
    {
        try
        {
            // Parent needs to start before the child, otherwise it might be a different process
            // that is just reusing the same PID.
            if (parent.StartTime <= currentProcess.StartTime)
            {
                return true;
            }

            Trace($"Process {parent.ProcessName} ({parent.Id}) is not a valid parent because it started after the current process.", enabled: enableLog);
        }
        catch
        {
            // Access denied or process exited while we were holding the Process object.
        }

        return false;
    }

    private static Process GetParentProcess(Process process)
    {
        int id = GetParentProcessId(process);
        if (id != -1)
        {
            try
            {
                var parent = Process.GetProcessById(id);
                if (IsCorrectParent(process, parent))
                {
                    return parent;
                }
            }
            catch
            {
                // throws when parent no longer runs
            }
        }

        return null;

        static int GetParentProcessId(Process process)
        {
            try
            {
                IntPtr handle = process.Handle;
                int res = NtQueryInformationProcess(handle, 0, out PROCESS_BASIC_INFORMATION pbi, Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(), out int size);

                int p = res != 0 ? -1 : pbi.InheritedFromUniqueProcessId.ToInt32();

                return p;
            }
            catch
            {
                return -1;
            }
        }
    }

    private static void Trace(string message, [CallerMemberName] string methodName = null, bool enabled = false)
    {
        if (enabled)
        {
            Console.WriteLine($"[AttachVS]{methodName}: {message}");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        public readonly IntPtr ExitStatus;
        public readonly IntPtr PebBaseAddress;
        public readonly IntPtr AffinityMask;
        public readonly IntPtr BasePriority;
        public readonly IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        out PROCESS_BASIC_INFORMATION processInformation,
        int processInformationLength,
        out int returnLength);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);
}
