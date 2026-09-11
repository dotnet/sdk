// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if CLI_AOT
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Handles eligible file-based application launches inside the Native AOT CLI.
/// </summary>
internal static class AotRunCommand
{
    /// <summary>
    /// Configures the Native AOT implementation for the <c>run</c> command.
    /// </summary>
    /// <param name="command">The shared run command definition.</param>
    internal static void ConfigureCommand(RunCommandDefinition command)
        => command.SetAction(parseResult => Execute(parseResult, Launch));

    /// <summary>
    /// Executes an eligible file-based application through the Native AOT launcher.
    /// </summary>
    /// <param name="parseResult">The parsed run invocation.</param>
    /// <returns>The launched process exit code.</returns>
    internal static int Execute(ParseResult parseResult)
        => Execute(parseResult, Launch);

    /// <summary>
    /// Plans and executes an eligible file-based application using an injected launcher.
    /// </summary>
    /// <param name="parseResult">The parsed run invocation.</param>
    /// <param name="launch">Launches the committed invocation.</param>
    /// <param name="currentDirectory">The current directory used for discovery and relative paths.</param>
    /// <returns>The launcher exit code.</returns>
    /// <exception cref="CommandNotAvailableInAotException">The invocation cannot be handled safely by the Native AOT path.</exception>
    /// <exception cref="GracefulException">Managed project discovery reports a user-facing input error.</exception>
    internal static int Execute(
        ParseResult parseResult,
        Func<AotRunInvocation, int> launch,
        string? currentDirectory = null)
    {
        currentDirectory ??= Environment.CurrentDirectory;
        var definition = (RunCommandDefinition)parseResult.CommandResult.Command;
        if (!TryGetEligibleInvocationInputs(
            parseResult,
            definition,
            currentDirectory,
            out bool noBuild,
            out string? entryPointFileFullPath,
            out string[]? applicationArguments,
            out IReadOnlyDictionary<string, string>? environmentVariables,
            out string fallbackReason))
        {
            throw CreateManagedFallbackException(fallbackReason);
        }

        LaunchProfileReadResult profileResult = ReadLaunchProfile(
            parseResult,
            definition,
            entryPointFileFullPath);

        string command;
        string commandArguments;
        string? workingDirectory;
        string? artifactsPath = null;
        RunProperties? validatedRunProperties = null;
        RunTier runTier;
        RunDecisionReason decisionReason;
        RunPlan? plan = null;
        if (noBuild && profileResult.Profile is not ExecutableLaunchProfile)
        {
            artifactsPath = VirtualProjectBuilder.GetArtifactsPath(entryPointFileFullPath);
            RunPlan noBuildPlan = FileBasedAppRunPlan.AnalyzeAotNoBuildSynthetic(
                entryPointFileFullPath,
                artifactsPath);
            if (noBuildPlan.Tier == RunTier.LaunchOnly)
            {
                plan = noBuildPlan;
            }
        }

        bool executableCanBypassCache = noBuild &&
            profileResult.Profile is ExecutableLaunchProfile;
        // A failed synthetic no-build check can still use an authoritative cached RunProperties
        // contract, so continue to full cache validation before falling back.
        if (plan is null && !executableCanBypassCache)
        {
            if (!TryGetRuntimeVersion(out string? runtimeVersion))
            {
                throw CreateManagedFallbackException("the runtime version could not be read");
            }

            artifactsPath ??= VirtualProjectBuilder.GetArtifactsPath(entryPointFileFullPath);
            RunPlan cachedPlan = FileBasedAppRunPlan.AnalyzeCachedLaunch(
                entryPointFileFullPath,
                artifactsPath,
                CreateGlobalProperties(parseResult, definition),
                Product.Version,
                runtimeVersion);
            if (cachedPlan.Tier == RunTier.CachedLaunch)
            {
                plan = cachedPlan;
            }
        }

        if (profileResult.Profile is ExecutableLaunchProfile executableProfile &&
            (executableCanBypassCache || plan?.Tier == RunTier.CachedLaunch))
        {
            EnsureLaunchProfileDoesNotRequireMSBuild(
                executableProfile,
                baseArguments: null,
                applicationArguments);
            if (noBuild)
            {
                artifactsPath = null;
            }
            command = executableProfile.ExecutablePath;
            commandArguments = CommonRunHelpers.CombineRunArguments(
                baseArguments: null,
                applicationArguments,
                executableProfile.CommandLineArgs);
            workingDirectory = executableProfile.WorkingDirectory
                ?? Path.GetDirectoryName(entryPointFileFullPath)!;
            runTier = RunTier.LaunchOnly;
            decisionReason = RunDecisionReason.ExecutableLaunchProfile;
        }
        else if (plan is { Launch: { } launchInfo })
        {
            validatedRunProperties = launchInfo.RunProperties;
            EnsureLaunchProfileDoesNotRequireMSBuild(
                profileResult.Profile,
                validatedRunProperties?.Arguments,
                applicationArguments);
            command = launchInfo.Command;
            commandArguments = CommonRunHelpers.CombineRunArguments(
                validatedRunProperties?.Arguments,
                applicationArguments,
                profileResult.Profile?.CommandLineArgs,
                appendApplicationArgumentsToBase: validatedRunProperties is not null);
            workingDirectory = validatedRunProperties?.WorkingDirectory ?? currentDirectory;
            runTier = plan.Tier;
            decisionReason = plan.Reason;
        }
        else
        {
            throw CreateManagedFallbackException("no eligible cached launch contract was found");
        }

        var launchEnvironment = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (profileResult.Profile is not ExecutableLaunchProfile)
        {
            string? rootVariableName = EnvironmentVariableNames.TryGetDotNetRootVariableName(
                validatedRunProperties?.RuntimeIdentifier ?? RuntimeInformation.RuntimeIdentifier,
                validatedRunProperties?.DefaultAppHostRuntimeIdentifier ?? RuntimeInformation.RuntimeIdentifier,
                validatedRunProperties?.TargetFrameworkVersion ?? $"v{Product.TargetFrameworkVersion}");
            if (rootVariableName is not null && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(rootVariableName)))
            {
                if (string.IsNullOrEmpty(NativeEntryPoint.DotnetRoot))
                {
                    throw CreateManagedFallbackException("the dotnet root could not be determined");
                }

                launchEnvironment[rootVariableName] = NativeEntryPoint.DotnetRoot;
            }
        }

        CommonRunHelpers.ApplyLaunchEnvironmentVariables(
            profileResult.Profile,
            environmentVariables,
            (name, value) => launchEnvironment[name] = value);

        profileResult.WriteMessages();
        if (!noBuild && profileResult.Profile?.DotNetRunMessages == true)
        {
            Reporter.Output.WriteLine(CliCommandStrings.RunCommandBuilding);
        }
        Reporter.Verbose.WriteLine($"AOT run tier: {runTier} ({decisionReason}).");

        if (artifactsPath is not null)
        {
            FileBasedAppRunPlan.MarkArtifactsPathUsed(artifactsPath);
        }

        int exitCode = launch(new AotRunInvocation(
            command,
            commandArguments,
            launchEnvironment,
            workingDirectory,
            artifactsPath));
        return exitCode;
    }

    private static void EnsureLaunchProfileDoesNotRequireMSBuild(
        LaunchProfile? launchProfile,
        string? baseArguments,
        string[] applicationArguments)
    {
        bool requiresMSBuild = launchProfile switch
        {
            ExecutableLaunchProfile profile =>
                LaunchProfileParser.RequiresMSBuildExpansion(profile.ExecutablePath) ||
                LaunchProfileParser.RequiresMSBuildExpansion(profile.WorkingDirectory) ||
                profile.EnvironmentVariables.Values.Any(LaunchProfileParser.RequiresMSBuildExpansion),
            ProjectLaunchProfile profile =>
                LaunchProfileParser.RequiresMSBuildExpansion(profile.ApplicationUrl) ||
                profile.EnvironmentVariables.Values.Any(LaunchProfileParser.RequiresMSBuildExpansion),
            _ => false,
        };

        if (requiresMSBuild ||
            (applicationArguments.Length == 0 &&
             string.IsNullOrEmpty(baseArguments) &&
             LaunchProfileParser.RequiresMSBuildExpansion(launchProfile?.CommandLineArgs)))
        {
            throw CreateManagedFallbackException("the launch profile contains MSBuild properties");
        }
    }

    private static int Launch(AotRunInvocation invocation)
    {
        var commandSpec = new CommandSpec(
            invocation.Command,
            invocation.CommandArguments);
        Microsoft.DotNet.Cli.Utils.Command command = CommandFactoryUsingResolver.Create(commandSpec);
        command.WorkingDirectory(invocation.WorkingDirectory);
        foreach ((string name, string? value) in invocation.EnvironmentVariables)
        {
            command.EnvironmentVariable(name, value);
        }

        ConsoleCancelEventHandler cancelHandler = static (_, eventArgs) => eventArgs.Cancel = true;
        Console.CancelKeyPress += cancelHandler;
        try
        {
            return command.Execute().ExitCode;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static LaunchProfileReadResult ReadLaunchProfile(
        ParseResult parseResult,
        RunCommandDefinition definition,
        string entryPointFileFullPath)
    {
        var messages = new List<(string Message, bool IsError)>();
        string? launchProfile = parseResult.GetValue(definition.LaunchProfileOption);
        LaunchProfileParseResult result = CommonRunHelpers.ReadLaunchProfile(
            entryPointFileFullPath,
            launchProfile,
            parseResult.HasOption(definition.NoLaunchProfileOption),
            // Explicit verbosity is not eligible for the AOT path, so every reachable invocation
            // has the managed command's default non-quiet run verbosity.
            reportUsingLaunchSettings: true,
            (message, isError) => messages.Add((message, isError)),
            new LaunchProfileParserOptions(
                EvaluateExpression: null,
                ExpandProjectProfile: false,
                ExpandExecutableProfile: false,
                ExpandCommandLineArgs: false),
            out _);
        if (result.FailureReason is not null)
        {
            messages.Add((string.Format(
                CliCommandStrings.RunCommandExceptionCouldNotApplyLaunchSettings,
                LaunchProfileParser.GetLaunchProfileDisplayName(launchProfile),
                result.FailureReason).Bold().Red(), IsError: true));
        }

        return new LaunchProfileReadResult(result.Profile, messages);
    }

    private static Dictionary<string, string> CreateGlobalProperties(
        ParseResult parseResult,
        RunCommandDefinition definition)
    {
        Dictionary<string, string> globalProperties = CommonRunHelpers.CreateFileBasedRunGlobalProperties();
        // Mirror the managed option's --property:NuGetInteractive forwarding without constructing
        // MSBuildArgs solely for cache validation.
        globalProperties["NuGetInteractive"] = parseResult.GetValue(definition.InteractiveOption) ? "true" : "false";
        return globalProperties;
    }

    private static bool TryGetRuntimeVersion([NotNullWhen(true)] out string? runtimeVersion)
    {
        runtimeVersion = null;
        try
        {
            using var stream = File.OpenRead(Path.Join(SdkPaths.SdkDirectory, "dotnet.runtimeconfig.json"));
            using JsonDocument document = JsonDocument.Parse(stream);
            runtimeVersion = document.RootElement
                .GetProperty("runtimeOptions")
                .GetProperty("framework")
                .GetProperty("version")
                .GetString();
            return !string.IsNullOrWhiteSpace(runtimeVersion);
        }
        catch (Exception exception)
        {
            Reporter.Verbose.WriteLine($"Failed to read the runtime version: {exception}");
            return false;
        }
    }

    private static bool TryGetEligibleInvocationInputs(
        ParseResult parseResult,
        RunCommandDefinition definition,
        string currentDirectory,
        out bool noBuild,
        [NotNullWhen(true)] out string? entryPointFileFullPath,
        [NotNullWhen(true)] out string[]? applicationArguments,
        [NotNullWhen(true)] out IReadOnlyDictionary<string, string>? environmentVariables,
        out string fallbackReason)
    {
        noBuild = parseResult.HasOption(definition.NoBuildOption);
        entryPointFileFullPath = null;
        applicationArguments = null;
        environmentVariables = null;
        fallbackReason = string.Empty;

        if (GetUnsupportedOption(parseResult, definition) is { } unsupportedOption)
        {
            fallbackReason = $"option '{unsupportedOption.Name}' is not supported by the native path";
            return false;
        }

        string[] parsedApplicationArguments = parseResult.GetValue(definition.ApplicationArguments) ?? [];
        if (!CommonRunHelpers.TrySplitApplicationArgumentsAtDoubleDash(
            parseResult,
            parsedApplicationArguments,
            out int argumentCountBeforeDoubleDash,
            out string[] argumentsAfterDoubleDash))
        {
            fallbackReason = "application arguments could not be separated at '--'";
            return false;
        }

        if (argumentCountBeforeDoubleDash > 0 && parsedApplicationArguments[0] == "-")
        {
            fallbackReason = "standard-input source code requires the managed run implementation";
            return false;
        }

        string? entryPointPath = parseResult.GetValue(definition.FileOption);
        if (string.IsNullOrEmpty(entryPointPath))
        {
            string? projectFilePath;
            try
            {
                projectFilePath = CommonRunHelpers.TryFindSingleProjectInDirectory(currentDirectory);
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                SecurityException)
            {
                fallbackReason = "the current directory could not be searched safely";
                return false;
            }

            if (projectFilePath is not null)
            {
                fallbackReason = "the current directory contains a project";
                return false;
            }

            if (argumentCountBeforeDoubleDash == 0)
            {
                throw new GracefulException(CliCommandStrings.RunCommandExceptionNoProjects, currentDirectory, "--project");
            }

            if (argumentCountBeforeDoubleDash != 1)
            {
                fallbackReason = "positional file discovery did not identify exactly one entry-point argument";
                return false;
            }

            entryPointPath = parsedApplicationArguments[0];
        }
        else if (argumentCountBeforeDoubleDash != 0)
        {
            fallbackReason = "application arguments before '--' are ambiguous with an explicit --file option";
            return false;
        }

        try
        {
            entryPointFileFullPath = Path.GetFullPath(entryPointPath, currentDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or SecurityException)
        {
            fallbackReason = "the entry-point path could not be normalized";
            return false;
        }

        if (!VirtualProjectBuilder.IsValidEntryPointPath(entryPointFileFullPath))
        {
            if (string.IsNullOrEmpty(parseResult.GetValue(definition.FileOption)))
            {
                throw new GracefulException(CliCommandStrings.RunCommandExceptionNoProjects, currentDirectory, "--project");
            }

            fallbackReason = "the entry-point path is not a supported C# file";
            return false;
        }

        applicationArguments = argumentsAfterDoubleDash;
        environmentVariables = parseResult.GetValue(definition.EnvOption)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        fallbackReason = string.Empty;
        return true;
    }

    private static Option? GetUnsupportedOption(ParseResult parseResult, RunCommandDefinition definition)
        => parseResult.CommandResult.Children
            .OfType<OptionResult>()
            .FirstOrDefault(optionResult =>
                !optionResult.Implicit
                && optionResult.Option != definition.FileOption
                && optionResult.Option != definition.LaunchProfileOption
                && optionResult.Option != definition.NoLaunchProfileOption
                && optionResult.Option != definition.NoBuildOption
                && optionResult.Option != definition.NoRestoreOption
                && optionResult.Option != definition.EnvOption)
            ?.Option;

    private static CommandNotAvailableInAotException CreateManagedFallbackException(string reason)
    {
        Reporter.Verbose.WriteLine($"AOT run is falling back to the managed CLI because {reason}.");
        return new CommandNotAvailableInAotException();
    }

}
#endif
