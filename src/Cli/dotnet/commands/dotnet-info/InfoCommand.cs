// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using System.Text.Json;  
using Microsoft.DotNet.Cli.Utils;

using LocalizableStrings = Microsoft.DotNet.Cli.Utils.LocalizableStrings;
using RuntimeEnvironment = Microsoft.DotNet.Cli.Utils.RuntimeEnvironment;

namespace Microsoft.DotNet.Tools.Info
{
    public class InfoCommand
    {
        private readonly ParseResult _parseResult;

        public InfoCommand(ParseResult parseResult)
        {
            _parseResult = parseResult;
        }

        public static int Run(ParseResult result)
        {
            result.HandleDebugSwitch();
            var format = result.GetValue(InfoCommandParser.FormatOption);
            if (format != InfoCommandParser.FormatOptions.json) {
                PrintInfo();
            } else  {
                // To be implemented
                PrintJsonInfo();
            }
            return 0;
        }

        public static void PrintVersion()
        {
            Reporter.Output.WriteLine(Product.Version);
        }

        public static void PrintInfo()
        {
            DotnetVersionFile versionFile = DotnetFiles.VersionFileObject;
            var commitSha = versionFile.CommitSha ?? "N/A";
            Reporter.Output.WriteLine($"{LocalizableStrings.DotNetSdkInfoLabel}");
            Reporter.Output.WriteLine($" Version:           {Product.Version}");
            Reporter.Output.WriteLine($" Commit:            {commitSha}");
            Reporter.Output.WriteLine($" Workload version:  {WorkloadCommandParser.GetWorkloadsVersion()}");
            Reporter.Output.WriteLine();
            Reporter.Output.WriteLine($"{LocalizableStrings.DotNetRuntimeInfoLabel}");
            Reporter.Output.WriteLine($" OS Name:     {RuntimeEnvironment.OperatingSystem}");
            Reporter.Output.WriteLine($" OS Version:  {RuntimeEnvironment.OperatingSystemVersion}");
            Reporter.Output.WriteLine($" OS Platform: {RuntimeEnvironment.OperatingSystemPlatform}");
            Reporter.Output.WriteLine($" RID:         {GetDisplayRid(versionFile)}");
            Reporter.Output.WriteLine($" Base Path:   {AppContext.BaseDirectory}");
            PrintWorkloadsInfo();
        }

        private static void PrintWorkloadsInfo()
        {
            Reporter.Output.WriteLine();
            Reporter.Output.WriteLine($"{LocalizableStrings.DotnetWorkloadInfoLabel}");
            WorkloadCommandParser.ShowWorkloadsInfo();
        }

        private static string GetDisplayRid(DotnetVersionFile versionFile)
        {
            FrameworkDependencyFile fxDepsFile = new();

            string currentRid = RuntimeInformation.RuntimeIdentifier;

            // if the current RID isn't supported by the shared framework, display the RID the CLI was
            // built with instead, so the user knows which RID they should put in their "runtimes" section.
            return fxDepsFile.IsRuntimeSupported(currentRid) ?
                currentRid :
                versionFile.BuildRid;
        }

        public class RuntimeEnvironmentInfo
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public string Platform { get; set; }
            public string Rid { get; set; }
        }

        public class SdkInfo
        {
            public string Commit { get; set; }
            public string Version { get; set; }
        }
        public class HostInfo
        {
            public string Arch { get; set; }
            public string Commit { get; set; }
            public string Version { get; set; }
        }

        public class InstalledRuntime
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string Version { get; set; }
        }

        public class OtherArchInfo
        {
            public string Arch { get; set; }
            public string BasePath { get; set; }
        }

        public class WorkloadManifest
        {
            public string InstallType { get; set; }
            public string Path { get; set; }
            public string Version { get; set; }
        }

        public class Workload
        {
            public List<InstallSource> InstallSources { get; set; }
            public WorkloadManifest Manifest { get; set; }
            public string Name { get; set; }
        }

        public class InstallSource
        {
            public string Name { get; set; }
            public string Version { get; set; }
        }

        public class DotNetInfo
        {
            public string BasePath { get; set; }
            public Dictionary<string, string> EnvVars { get; set; }
            public string GlobalJson { get; set; }
            public HostInfo Host { get; set; }
            public List<InstalledRuntime> InstalledRuntimes { get; set; }
            public List<OtherArchInfo> OtherArch { get; set; }
            public RuntimeEnvironmentInfo RuntimeEnv { get; set; }
            public SdkInfo Sdk { get; set; }
            public List<Workload> Workloads { get; set; }

            public string ToJson()
            {
                return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            }
        }

        public static void PrintJsonInfo()
        {
            DotnetVersionFile versionFile = DotnetFiles.VersionFileObject;
            var commitSha = versionFile.CommitSha ?? "N/A";
            var basePath = AppContext.BaseDirectory;

            var dotNetInfo = new DotNetInfo
            {
                BasePath = basePath,
                EnvVars = new Dictionary<string, string>
                {
                    // Populate with environment variables
                },
                GlobalJson = Environment.CurrentDirectory,
                Host = new HostInfo
                {
                    Arch = RuntimeInformation.ProcessArchitecture.ToString(),
                    Commit = commitSha,
                    Version = Product.Version
                },
                InstalledRuntimes = new List<InstalledRuntime>
                {
                    // Populate with installed runtime information
                },
                OtherArch = new List<OtherArchInfo>
                {
                    // Populate with other architecture information
                },
                RuntimeEnv = new RuntimeEnvironmentInfo
                {
                    Name = RuntimeEnvironment.OperatingSystem,
                    Platform = RuntimeEnvironment.OperatingSystemPlatform.ToString(),
                    Rid = GetDisplayRid(versionFile),
                    Version = RuntimeEnvironment.OperatingSystemVersion
                },
                Sdk = new SdkInfo
                {
                    Commit = commitSha,
                    Version = Product.Version
                },
                Workloads = WorkloadCommandParser.GetWorkloadsInfo()
            };

            string jsonOutput = dotNetInfo.ToJson();
            Reporter.Output.WriteLine(jsonOutput);
        }
    }
}
