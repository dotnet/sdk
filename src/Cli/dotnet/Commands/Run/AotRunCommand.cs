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
    internal static int Execute(
        ParseResult parseResult,
        Func<AotRunInvocation, int> launch,
        string? currentDirectory = null)
    {
        currentDirectory ??= Environment.CurrentDirectory;
        Reporter.Reset();
        var definition = (RunCommandDefinition)parseResult.CommandResult.Command;
        if (!TryGetEligibleInvocationInputs(
            parseResult,
            definition,
            currentDirectory,
            out bool noBuild,
            out string? entryPointFileFullPath,
            out string[]? applicationArguments,
            out IReadOnlyDictionary<string, string>? environmentVariables))
        {
            Reporter.Verbose.WriteLine("AOT run deferred because the invocation is outside the native cached-launch option set.");
            throw new CommandNotAvailableInAotException();
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
        var planMessages = new List<string>();
        RunPlan? plan = null;
        if (noBuild && profileResult.Profile is not ExecutableLaunchProfile)
        {
            artifactsPath = VirtualProjectBuilder.GetArtifactsPath(entryPointFileFullPath);
            RunPlan noBuildPlan = FileBasedAppRunPlan.AnalyzeNoBuildSynthetic(
                entryPointFileFullPath,
                artifactsPath,
                () => FileBasedAppDirectiveProbe.Probe(entryPointFileFullPath),
                planMessages.Add);
            if (noBuildPlan.Tier == RunTier.LaunchOnly)
            {
                plan = noBuildPlan;
            }
        }

        bool executableCanBypassCache = noBuild &&
            profileResult.Profile is ExecutableLaunchProfile &&
            FileBasedAppDirectiveProbe.Probe(entryPointFileFullPath) == FileBasedAppDirectiveProbeResult.None;
        if (plan is null && !executableCanBypassCache)
        {
            if (!TryGetRuntimeVersion(out string? runtimeVersion))
            {
                throw new CommandNotAvailableInAotException();
            }

            artifactsPath ??= VirtualProjectBuilder.GetArtifactsPath(entryPointFileFullPath);
            planMessages.Clear();
            RunPlan cachedPlan = FileBasedAppRunPlan.AnalyzeCachedLaunch(
                entryPointFileFullPath,
                artifactsPath,
                CreateGlobalProperties(parseResult, definition),
                Product.Version,
                runtimeVersion,
                planMessages.Add);
            if (cachedPlan.Tier == RunTier.CachedLaunch)
            {
                plan = cachedPlan;
            }
        }

        if (profileResult.Profile is ExecutableLaunchProfile executableProfile &&
            (executableCanBypassCache || plan?.Tier == RunTier.CachedLaunch))
        {
            if (noBuild)
            {
                artifactsPath = null;
                planMessages.Clear();
            }
            command = executableProfile.ExecutablePath;
            commandArguments = AotRunArguments.Combine(
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
            command = launchInfo.Command;
            commandArguments = AotRunArguments.Combine(
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
            throw new CommandNotAvailableInAotException();
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
                    Reporter.Verbose.WriteLine("AOT run deferred because the dotnet root could not be determined.");
                    throw new CommandNotAvailableInAotException();
                }

                launchEnvironment[rootVariableName] = NativeEntryPoint.DotnetRoot;
            }
        }

        if (profileResult.Profile is ProjectLaunchProfile { ApplicationUrl.Length: > 0 } projectProfile)
        {
            launchEnvironment["ASPNETCORE_URLS"] = projectProfile.ApplicationUrl;
        }
        if (profileResult.Profile is { } profile)
        {
            launchEnvironment["DOTNET_LAUNCH_PROFILE"] = profile.LaunchProfileName;
            foreach ((string name, string value) in profile.EnvironmentVariables)
            {
                launchEnvironment[name] = value;
            }
        }
        foreach ((string name, string value) in environmentVariables)
        {
            launchEnvironment[name] = value;
        }

        foreach (string message in planMessages)
        {
            Reporter.Verbose.WriteLine(message);
        }
        profileResult.WriteMessages();
        if (!noBuild && profileResult.Profile?.DotNetRunMessages == true)
        {
            Reporter.Output.WriteLine(CliCommandStrings.RunCommandBuilding);
        }
        Reporter.Verbose.WriteLine($"AOT run tier: {runTier} ({decisionReason}).");

        if (artifactsPath is not null)
        {
            try
            {
                Directory.SetLastWriteTimeUtc(artifactsPath, DateTime.UtcNow);
            }
            catch (Exception exception)
            {
                Reporter.Verbose.WriteLine($"Cannot touch folder '{artifactsPath}': {exception}");
            }
        }

        int exitCode = launch(new AotRunInvocation(
            command,
            commandArguments,
            launchEnvironment,
            workingDirectory,
            artifactsPath));
        return exitCode;
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
        if (parseResult.HasOption(definition.NoLaunchProfileOption))
        {
            return new LaunchProfileReadResult(Profile: null, Messages: []);
        }

        var messages = new List<(string Message, bool IsError)>();
        string? launchProfile = parseResult.GetValue(definition.LaunchProfileOption);
        string? path = LaunchSettings.TryFindLaunchSettingsFile(
            entryPointFileFullPath,
            launchProfile,
            (message, isError) => messages.Add((message, isError)));
        if (path is null)
        {
            return new LaunchProfileReadResult(Profile: null, messages);
        }

        messages.Add((string.Format(CliCommandStrings.UsingLaunchSettingsFromMessage, path), IsError: true));
        LaunchProfileParseResult result = LaunchSettings.ReadProfileSettingsFromFile(path, launchProfile);
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
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["NuGetInteractive"] = parseResult.GetValue(definition.InteractiveOption) ? "true" : "false",
            ["_BuildNonexistentProjectsByDefault"] = bool.TrueString,
            ["RestoreUseSkipNonexistentTargets"] = bool.FalseString,
            ["ProvideCommandLineArgs"] = bool.TrueString,
        };

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
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            SecurityException or
            JsonException or
            KeyNotFoundException or
            InvalidOperationException)
        {
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
        [NotNullWhen(true)] out IReadOnlyDictionary<string, string>? environmentVariables)
    {
        noBuild = parseResult.HasOption(definition.NoBuildOption);
        entryPointFileFullPath = null;
        applicationArguments = null;
        environmentVariables = null;

        if (HasUnsupportedOptions(parseResult, definition))
        {
            return false;
        }

        string[] parsedApplicationArguments = parseResult.GetValue(definition.ApplicationArguments) ?? [];
        int doubleDashIndex = parseResult.Tokens.ToList().FindIndex(static token => token.Type == TokenType.DoubleDash);
        string[] argumentsAfterDoubleDash = doubleDashIndex < 0
            ? []
            : [.. parseResult.Tokens.Skip(doubleDashIndex + 1).Select(static token => token.Value)];
        int argumentCountBeforeDoubleDash = parsedApplicationArguments.Length - argumentsAfterDoubleDash.Length;
        if (argumentCountBeforeDoubleDash < 0 ||
            !parsedApplicationArguments.Skip(argumentCountBeforeDoubleDash).SequenceEqual(argumentsAfterDoubleDash, StringComparer.Ordinal))
        {
            return false;
        }

        string? entryPointPath = parseResult.GetValue(definition.FileOption);
        if (string.IsNullOrEmpty(entryPointPath))
        {
            if (argumentCountBeforeDoubleDash != 1 ||
                CurrentDirectoryContainsProject(currentDirectory))
            {
                return false;
            }

            entryPointPath = parsedApplicationArguments[0];
        }
        else if (argumentCountBeforeDoubleDash != 0)
        {
            return false;
        }

        try
        {
            entryPointFileFullPath = Path.GetFullPath(entryPointPath, currentDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or SecurityException)
        {
            return false;
        }

        if (!VirtualProjectBuilder.IsValidEntryPointPath(entryPointFileFullPath))
        {
            return false;
        }

        applicationArguments = argumentsAfterDoubleDash;
        environmentVariables = parseResult.GetValue(definition.EnvOption)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return true;
    }

    private static bool CurrentDirectoryContainsProject(string currentDirectory)
    {
        try
        {
            return Directory.GetFiles(currentDirectory, "*.*proj").Length != 0;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            SecurityException)
        {
            return true;
        }
    }

    private static bool HasUnsupportedOptions(ParseResult parseResult, RunCommandDefinition definition)
        => parseResult.HasOption(definition.ConfigurationOption) ||
            parseResult.HasOption(definition.FrameworkOption) ||
            parseResult.HasOption(definition.ProjectOption) ||
            parseResult.HasOption(definition.PropertyOption) ||
            parseResult.HasOption(definition.DeviceOption) ||
            parseResult.HasOption(definition.ListDevicesOption) ||
            parseResult.HasOption(definition.NoCacheOption) ||
            parseResult.HasOption(definition.SelfContainedOption) ||
            parseResult.HasOption(definition.NoSelfContainedOption) ||
            parseResult.HasOption(definition.InteractiveOption) ||
            parseResult.HasOption(definition.VerbosityOption) ||
            parseResult.HasOption(definition.DisableBuildServersOption) ||
            parseResult.HasOption(definition.ArtifactsPathOption) ||
            parseResult.HasOption(definition.TargetPlatformOptions.RuntimeOption) ||
            parseResult.HasOption(definition.TargetPlatformOptions.ArchitectureOption) ||
            parseResult.HasOption(definition.TargetPlatformOptions.OperatingSystemOption);

}
#endif
