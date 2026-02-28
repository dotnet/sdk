// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
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

internal class HandleDiagnosticAction(Option option) : InvocableOptionAction(option)
{
    public override bool Terminating => false;

    public override int Invoke(ParseResult parseResult)
    {
        // Determine whether the diagnostic option should be attached to the dotnet command or the subcommand.
        if (DiagOptionPrecedesSubcommand(parseResult.Tokens.Select(t => t.Value), parseResult.RootSubCommandResult()))
        {
            Environment.SetEnvironmentVariable(CommandLoggingContext.Variables.Verbose, bool.TrueString);
            CommandLoggingContext.SetVerbose(true);
            Reporter.Reset();
        }
        return 0;
    }

    private static bool DiagOptionPrecedesSubcommand(IEnumerable<string> tokens, string subCommand)
    {
        if (string.IsNullOrEmpty(subCommand))
        {
            return true;
        }

        foreach (var token in tokens)
        {
            if (token == subCommand)
            {
                return false;
            }

            if (Parser.RootCommand.DiagOption.Name == token
                || Parser.RootCommand.DiagOption.Aliases.Contains(token))
            {
                return true;
            }
        }

        return false;
    }
}

internal class PrintVersionAction(Option option) : InvocableOptionAction(option)
{
    public override bool Terminating => true;

    public override int Invoke(ParseResult parseResult)
    {
        Reporter.Output.WriteLine(Product.Version);
        return 0;
    }
}

internal class PrintInfoAction(Option option) : InvocableOptionAction(option)
{
    public override bool Terminating => true;

    public override int Invoke(ParseResult parseResult)
    {
        DotnetVersionFile versionFile = DotnetFiles.VersionFileObject;
        var commitSha = versionFile.CommitSha ?? "N/A";
        Reporter.Output.WriteLine($"{LocalizableStrings.DotNetSdkInfoLabel}");
        Reporter.Output.WriteLine($" Version:           {Product.Version}");
        Reporter.Output.WriteLine($" Commit:            {commitSha}");
        Reporter.Output.WriteLine($" Workload version:  {WorkloadInfoHelper.GetWorkloadsVersion()}");
        Reporter.Output.WriteLine($" MSBuild version:   {MSBuildForwardingAppWithoutLogging.MSBuildVersion}");
        Reporter.Output.WriteLine();
        Reporter.Output.WriteLine($"{LocalizableStrings.DotNetRuntimeInfoLabel}");
        Reporter.Output.WriteLine($" OS Name:     {RuntimeEnvironment.OperatingSystem}");
        Reporter.Output.WriteLine($" OS Version:  {RuntimeEnvironment.OperatingSystemVersion}");
        Reporter.Output.WriteLine($" OS Platform: {RuntimeEnvironment.OperatingSystemPlatform}");
        Reporter.Output.WriteLine($" RID:         {GetDisplayRid(versionFile)}");
        Reporter.Output.WriteLine($" Base Path:   {AppContext.BaseDirectory}");
        Reporter.Output.WriteLine();
        Reporter.Output.WriteLine($"{LocalizableStrings.DotnetWorkloadInfoLabel}");
        new WorkloadInfoHelper(isInteractive: false).ShowWorkloadsInfo(showVersion: false);
        return 0;
    }

    private static string? GetDisplayRid(DotnetVersionFile versionFile)
    {
        FrameworkDependencyFile fxDepsFile = new();
        string currentRid = RuntimeInformation.RuntimeIdentifier;
        // If the current RID isn't supported by the shared framework, display the RID the CLI was built with instead,
        // so the user knows which RID they should put in their "runtimes" section.
        return fxDepsFile.IsRuntimeSupported(currentRid) ? currentRid : versionFile.BuildRid;
    }
}

internal class PrintCliSchemaAction(Option option) : InvocableOptionAction(option)
{
    public override bool Terminating => true;

    public override int Invoke(ParseResult parseResult)
    {
        CliSchema.PrintCliSchema(parseResult.CommandResult, parseResult.InvocationConfiguration.Output, Program.TelemetryClient);
        return 0;
    }
}
