// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Help;
using Microsoft.DotNet.Cli.Utils;
#if !CLI_AOT
using Microsoft.DotNet.Cli.Commands.Workload;
#endif
using RuntimeEnvironment = Microsoft.DotNet.Cli.Utils.RuntimeEnvironment;

namespace Microsoft.DotNet.Cli;

/// <summary>
/// Represents an option that contains an invocable action.
/// These are essentially commands that are only defined as an option.
/// </summary>
internal abstract class InvocableOptionAction(Option option) : SynchronousCommandLineAction
{
    /// <summary>
    /// The option for which this action is bound.
    /// </summary>
    public Option Option { get; } = option;
}

internal class PrintHelpAction(Option option, HelpBuilder builder) : InvocableOptionAction(option)
{
    public override bool Terminating => true;
    public override bool ClearsParseErrors => true;

    private HelpBuilder Builder { get; } = builder;

    public override int Invoke(ParseResult parseResult)
    {
        var command = parseResult.CommandResult.Command;
        var output = parseResult.InvocationConfiguration.Output;
        var helpContext = new HelpContext(Builder, command, output, parseResult);
        Builder.Write(helpContext);

        return 0;
    }
}

internal class PrintVersionAction(Option<bool> option) : InvocableOptionAction(option)
{
    public override bool Terminating => true;

    public override int Invoke(ParseResult parseResult)
    {
        // Equivalent to parseResult.HasOption(Option): the option was explicitly provided (not an
        // implicit default). Expressed with the System.CommandLine API directly so this action can be
        // shared by the AOT build, which does not reference the Microsoft.DotNet.Cli.CommandLine project.
        if (parseResult.GetResult(Option) is not { Implicit: false } || !parseResult.GetValue(option)
            // Only print for top-level commands.
            || !parseResult.IsTopLevelDotnetCommand())
        {
            return 0;
        }

        Reporter.Output.WriteLine(Product.Version);

        return 0;
    }
}

internal class PrintInfoAction(Option<bool> option) : InvocableOptionAction(option)
{
    public override bool Terminating => true;

    public override int Invoke(ParseResult parseResult)
    {
        if (parseResult.GetResult(Option) is not { Implicit: false } || !parseResult.GetValue(option)
            // Only print for top-level commands.
            || !parseResult.IsTopLevelDotnetCommand())
        {
            return 0;
        }

        DotnetVersionFile versionFile = DotnetFiles.VersionFileObject;
        var commitSha = versionFile.CommitSha ?? "N/A";
        Reporter.Output.WriteLine($"{LocalizableStrings.DotNetSdkInfoLabel}");
        Reporter.Output.WriteLine($" Version:           {Product.Version}");
        Reporter.Output.WriteLine($" Commit:            {commitSha}");
#if !CLI_AOT
        // Workload and MSBuild version reporting are not AOT-compatible yet (they pull in the
        // workload manager and MSBuild forwarding machinery), so they are omitted from the AOT build.
        Reporter.Output.WriteLine($" Workload version:  {WorkloadInfoHelper.GetWorkloadsVersion()}");
        Reporter.Output.WriteLine($" MSBuild version:   {MSBuildForwardingAppWithoutLogging.MSBuildVersion}");
#endif
        Reporter.Output.WriteLine();
        Reporter.Output.WriteLine($"{LocalizableStrings.DotNetRuntimeInfoLabel}");
        Reporter.Output.WriteLine($" OS Name:     {RuntimeEnvironment.OperatingSystem}");
        Reporter.Output.WriteLine($" OS Version:  {RuntimeEnvironment.OperatingSystemVersion}");
        Reporter.Output.WriteLine($" OS Platform: {RuntimeEnvironment.OperatingSystemPlatform}");
#if !CLI_AOT
        Reporter.Output.WriteLine($" RID:         {GetDisplayRid(versionFile)}");
#else
        // GetDisplayRid consults the shared framework's deps file, which isn't available in AOT.
        Reporter.Output.WriteLine($" RID:         {RuntimeInformation.RuntimeIdentifier}");
#endif
        Reporter.Output.WriteLine($" Base Path:   {AppContext.BaseDirectory}");
#if !CLI_AOT
        Reporter.Output.WriteLine();
        Reporter.Output.WriteLine($"{LocalizableStrings.DotnetWorkloadInfoLabel}");
        new WorkloadInfoHelper(isInteractive: false).ShowWorkloadsInfo(showVersion: false);
#endif

        return 0;
    }

#if !CLI_AOT
    private static string? GetDisplayRid(DotnetVersionFile versionFile)
    {
        FrameworkDependencyFile fxDepsFile = new();
        string currentRid = RuntimeInformation.RuntimeIdentifier;
        // If the current RID isn't supported by the shared framework, display the RID the CLI was built with instead,
        // so the user knows which RID they should put in their "runtimes" section.
        return fxDepsFile.IsRuntimeSupported(currentRid) ? currentRid : versionFile.BuildRid;
    }
#endif
}

