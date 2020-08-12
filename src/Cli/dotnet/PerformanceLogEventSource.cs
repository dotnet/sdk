using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli
{
    [EventSource(Name = "Microsoft-Dotnet-CLI-Performance")]
    internal sealed class PerformanceLogEventSource : EventSource
    {
        internal static PerformanceLogEventSource Log = new PerformanceLogEventSource();

        private PerformanceLogEventSource()
        {
        }

        [NonEvent]
        internal void LogStartUpInformation(DateTime mainTimeStamp)
        {
            Process currentProcess = Process.GetCurrentProcess();

            DotnetVersionFile versionFile = DotnetFiles.VersionFileObject;
            string commitSha = versionFile.CommitSha ?? "N/A";

            OSInfo(RuntimeEnvironment.OperatingSystem, RuntimeEnvironment.OperatingSystemVersion, RuntimeEnvironment.OperatingSystemPlatform.ToString());
            SDKInfo(Product.Version, commitSha, GetDisplayRid(versionFile), AppContext.BaseDirectory);
            EnvironmentInfo(Environment.CommandLine);

            TimeSpan latency = mainTimeStamp - currentProcess.StartTime;
            HostLatency(latency.TotalMilliseconds);
        }

        // TODO: This is duplicated from Program.cs
        [NonEvent]
        private static string GetDisplayRid(DotnetVersionFile versionFile)
        {
            FrameworkDependencyFile fxDepsFile = new FrameworkDependencyFile();

            string currentRid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;

            // if the current RID isn't supported by the shared framework, display the RID the CLI was
            // built with instead, so the user knows which RID they should put in their "runtimes" section.
            return fxDepsFile.IsRuntimeSupported(currentRid) ?
                currentRid :
                versionFile.BuildRid;
        }
        
        [Event(1)]
        internal void OSInfo(string name, string version, string platform)
        {
            WriteEvent(1, name, version, platform);
        }

        [Event(2)]
        internal void SDKInfo(string version, string commit, string rid, string basePath)
        {
            WriteEvent(2, version, commit, rid, basePath);
        }

        [Event(3)]
        internal void EnvironmentInfo(string commandLine)
        {
            WriteEvent(3, commandLine);
        }

        [Event(4)]
        internal void HostLatency(double timeInMs)
        {
            WriteEvent(4, timeInMs);
        }
    }
}
