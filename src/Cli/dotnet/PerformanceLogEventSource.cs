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
        internal void OSInfo(string osname, string osversion, string osplatform)
        {
            WriteEvent(1, osname, osversion, osplatform);
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

        [Event(5)]
        internal void CLIStart()
        {
            WriteEvent(5);
        }

        [Event(6)]
        internal void CLIStop()
        {
            WriteEvent(6);
        }

        [Event(7)]
        internal void FirstTimeConfigurationStart()
        {
            WriteEvent(7);
        }

        [Event(8)]
        internal void FirstTimeConfigurationStop()
        {
            WriteEvent(8);
        }

        [Event(9)]
        internal void TelemetryRegistrationStart()
        {
            WriteEvent(9);
        }

        [Event(10)]
        internal void TelemetryRegistrationStop()
        {
            WriteEvent(10);
        }

        [Event(11)]
        internal void TelemetrySendIfEnabledStart()
        {
            WriteEvent(11);
        }

        [Event(12)]
        internal void TelemetrySendIfEnabledStop()
        {
            WriteEvent(12);
        }

        [Event(13)]
        internal void BuiltInCommandStart()
        {
            WriteEvent(13);
        }

        [Event(14)]
        internal void BuiltInCommandStop()
        {
            WriteEvent(14);
        }

        [Event(15)]
        internal void BuiltInCommandParserStart()
        {
            WriteEvent(15);
        }

        [Event(16)]
        internal void BuiltInCommandParserStop()
        {
            WriteEvent(16);
        }

        [Event(17)]
        internal void ExtensibleCommandResolverStart()
        {
            WriteEvent(17);
        }

        [Event(18)]
        internal void ExtensibleCommandResolverStop()
        {
            WriteEvent(18);
        }

        [Event(19)]
        internal void ExtensibleCommandStart()
        {
            WriteEvent(19);
        }

        [Event(20)]
        internal void ExtensibleCommandStop()
        {
            WriteEvent(20);
        }

        [Event(21)]
        internal void TelemetryClientFlushStart()
        {
            WriteEvent(21);
        }

        [Event(22)]
        internal void TelemetryClientFlushStop()
        {
            WriteEvent(22);
        }
    }
}
