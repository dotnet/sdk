// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Help;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
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

internal class HandleDiagnosticAction(Option<bool> option) : InvocableOptionAction(option)
{
    public override bool Terminating => false;

    // S.CL will always invoke this option action because:
    // 1. A boolean option has a default value of `false`.
    // 2. The option has Terminating set to `false`.
    // When a default value exists (non-null) and the option is not terminating, the option action is always invoked.
    public override int Invoke(ParseResult parseResult)
    {
        // This check is necessary as per comment above.
        if (!parseResult.HasOption(Option) || !parseResult.GetValue(option)
            // Only set verbose output on built-in commands.
            || !parseResult.IsDotnetBuiltInCommand())
        {
            return 0;
        }

        // Determine whether the diagnostic option should be attached to the dotnet command or the subcommand.
        if (OptionPrecedesSubcommand(parseResult.Tokens.Select(t => t.Value), parseResult.RootSubCommandResult()))
        {
            Environment.SetEnvironmentVariable(CommandLoggingContext.Variables.Verbose, bool.TrueString);
            CommandLoggingContext.SetVerbose(true);
            Reporter.Reset();

            var home = Env.GetEnvironmentVariable(CliFolderPathCalculator.DotnetHomeVariableName);
            if (!string.IsNullOrEmpty(home))
            {
                // Output DOTNET_CLI_HOME usage when verbosity is enabled.
                Reporter.Verbose.WriteLine(string.Format(LocalizableStrings.DotnetCliHomeUsed, home, CliFolderPathCalculator.DotnetHomeVariableName));
            }
        }

        return 0;
    }

    private bool OptionPrecedesSubcommand(IEnumerable<string> tokens, string subCommand)
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

            if (Option.Name == token || Option.Aliases.Contains(token))
            {
                return true;
            }
        }

        return false;
    }
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
        if (!parseResult.HasOption(Option) || !parseResult.GetValue(option)
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
        if (!parseResult.HasOption(Option) || !parseResult.GetValue(option)
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

internal class PrintCliSchemaAction(Option<bool> option) : InvocableOptionAction(option)
{
    public override bool Terminating => true;

    public override int Invoke(ParseResult parseResult)
    {
        if (!parseResult.HasOption(Option) || !parseResult.GetValue(option))
        {
            return 0;
        }

        CliSchema.PrintCliSchema(parseResult, parseResult.InvocationConfiguration.Output, Program.TelemetryInstance);

        return 0;
    }
}
