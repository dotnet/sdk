// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

#if !CLI_AOT
using Microsoft.DotNet.Cli.Commands.Workload;
#endif
using Microsoft.DotNet.Cli.Utils;
#if !CLI_AOT
using LocalizableStrings = Microsoft.DotNet.Cli.Utils.LocalizableStrings;
using RuntimeEnvironment = Microsoft.DotNet.Cli.Utils.RuntimeEnvironment;
#endif

namespace Microsoft.DotNet.Cli;

public class CommandLineInfo
{
    public static void PrintVersion()
    {
        Reporter.Output.WriteLine(Product.Version);
    }

    public static void PrintInfo()
    {
        DotnetVersionFile versionFile = DotnetFiles.VersionFileObject;
        var commitSha = versionFile.CommitSha ?? "N/A";
#if CLI_AOT
        Reporter.Output.WriteLine(".NET SDK:");
#else
        Reporter.Output.WriteLine($"{LocalizableStrings.DotNetSdkInfoLabel}");
#endif
        Reporter.Output.WriteLine($" Version:           {Product.Version}");
        Reporter.Output.WriteLine($" Commit:            {commitSha}");
#if !CLI_AOT
        Reporter.Output.WriteLine($" Workload version:  {WorkloadInfoHelper.GetWorkloadsVersion()}");
        Reporter.Output.WriteLine($" MSBuild version:   {MSBuildForwardingAppWithoutLogging.MSBuildVersion}");
#endif
        Reporter.Output.WriteLine();
#if CLI_AOT
        Reporter.Output.WriteLine("Runtime Environment:");
        Reporter.Output.WriteLine($" OS Name:     {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        Reporter.Output.WriteLine($" OS Platform: {(OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "Darwin" : "Linux")}");
#else
        Reporter.Output.WriteLine($"{LocalizableStrings.DotNetRuntimeInfoLabel}");
        Reporter.Output.WriteLine($" OS Name:     {RuntimeEnvironment.OperatingSystem}");
        Reporter.Output.WriteLine($" OS Version:  {RuntimeEnvironment.OperatingSystemVersion}");
        Reporter.Output.WriteLine($" OS Platform: {RuntimeEnvironment.OperatingSystemPlatform}");
#endif
        Reporter.Output.WriteLine($" RID:         {RuntimeInformation.RuntimeIdentifier}");
        Reporter.Output.WriteLine($" Base Path:   {AppContext.BaseDirectory}");
#if !CLI_AOT
        PrintWorkloadsInfo();
#endif
    }

#if !CLI_AOT
    private static void PrintWorkloadsInfo()
    {
        Reporter.Output.WriteLine();
        Reporter.Output.WriteLine($"{LocalizableStrings.DotnetWorkloadInfoLabel}");
        new WorkloadInfoHelper(isInteractive: false).ShowWorkloadsInfo(showVersion: false);
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
#endif
}
