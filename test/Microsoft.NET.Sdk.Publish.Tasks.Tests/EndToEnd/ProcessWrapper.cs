using System;
using System.Diagnostics;
using System.IO;
#if NETFRAMEWORK
using System.Management;
#endif

namespace Microsoft.NET.Sdk.Publish.Tasks.Tests.EndToEnd
{
    public class ProcessWrapper
    {
        public int? RunProcess(string fileName, string arguments, string workingDirectory, out int? processId, bool createDirectoryIfNotExists = true, bool waitForExit = true, ITestOutputHelper testOutputHelper = null)
        {
            if (createDirectoryIfNotExists && !Directory.Exists(workingDirectory))
            {
                Directory.CreateDirectory(workingDirectory);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory,
                FileName = fileName,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            if (!string.IsNullOrEmpty(arguments))
            {
                startInfo.Arguments = arguments;
            }

            Process testProcess = Process.Start(startInfo);
            processId = testProcess?.Id;
            if (waitForExit)
            {
                testProcess.WaitForExit(3 * 60 * 1000);
                var standardOut = testProcess.StandardOutput.ReadToEnd();
                var standardError = testProcess.StandardError.ReadToEnd();
                testOutputHelper?.WriteLine(standardOut);
                testOutputHelper?.WriteLine(standardError);
                return testProcess?.ExitCode;
            }

            return -1;
        }

        public static void KillProcessTree(int processId)
        {
            Process process;
            try
            {
                process = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                // Process might have already exited.
                return;
            }

            using (process)
            {
                try
                {
                    if (!process.HasExited)
                    {
#if NETFRAMEWORK
                        KillProcessTreeInternal(process.Id);
#else
                        process.Kill(entireProcessTree: true);
#endif
                    }
                }
                catch
                {
                }
            }
        }

#if NETFRAMEWORK
        private static void KillProcessTreeInternal(int pid)
        {
            using (ManagementObjectSearcher searcher = new("Select * From Win32_Process Where ParentProcessID=" + pid))
            using (ManagementObjectCollection moc = searcher.Get())
            {
                foreach (ManagementObject mo in moc)
                {
                    using (mo)
                    {
                        KillProcessTreeInternal(Convert.ToInt32(mo["ProcessID"]));
                    }
                }
            }

            try
            {
                using (Process proc = Process.GetProcessById(pid))
                {
                    proc.Kill();
                }
            }
            catch (ArgumentException)
            {
                // Process might have already exited.
            }
        }
#endif
    }
}
