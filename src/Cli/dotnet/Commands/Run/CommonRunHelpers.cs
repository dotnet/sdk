// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Provides behavior shared by managed and Native AOT implementations of the <c>run</c> command.
/// </summary>
internal static class CommonRunHelpers
{
    /// <summary>
    /// Creates a dictionary of global properties for MSBuild from the command line arguments.
    /// This includes properties that are passed via the command line, as well as some
    /// properties that are set to improve performance at the cost of correctness -
    /// specifically Compile, None, and EmbeddedResource items are not globbed by default.
    /// See <see cref="Commands.Restore.RestoringCommand.RestoreOptimizationProperties"/> for more details.
    /// </summary>
    public static Dictionary<string, string> GetGlobalPropertiesFromArgs(MSBuildArgs msbuildArgs)
    {
        var globalProperties = msbuildArgs.GlobalProperties?.ToDictionary() ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        globalProperties[Constants.MSBuildExtensionsPath] = AppContext.BaseDirectory;
        return globalProperties;
    }

    /// <summary>
    /// Applies adjustments to MSBuild arguments to better suit LLM/agentic environments, if such an environment is detected.
    /// </summary>
    public static MSBuildArgs AdjustMSBuildForLLMs(MSBuildArgs msbuildArgs)
    {
        if (new Telemetry.LLMEnvironmentDetectorForTelemetry().IsLLMEnvironment())
        {
            // disable the live-update display of the TerminalLogger, which wastes tokens
            return msbuildArgs.CloneWithAdditionalArgs(Constants.TerminalLogger_DisableNodeDisplay);
        }
        else
        {
            return msbuildArgs;
        }
    }

    /// <summary>
    /// Finds and parses the selected launch profile.
    /// </summary>
    /// <param name="projectOrEntryPointFilePath">The project or entry-point path, or <see langword="null"/> when launch-settings discovery is unavailable.</param>
    /// <param name="launchProfile">The requested launch-profile name.</param>
    /// <param name="noLaunchProfile">Whether launch profiles are disabled.</param>
    /// <param name="reportUsingLaunchSettings">Whether to report the selected launch-settings file.</param>
    /// <param name="report">Receives launch-settings diagnostics and whether each belongs on the error channel.</param>
    /// <returns>The parsed launch profile or its failure reason.</returns>
    public static LaunchProfileParseResult ReadLaunchProfile(
        string? projectOrEntryPointFilePath,
        string? launchProfile,
        bool noLaunchProfile,
        bool reportUsingLaunchSettings,
        Action<string, bool> report)
    {
        if (noLaunchProfile || projectOrEntryPointFilePath is null)
        {
            return LaunchProfileParseResult.Success(model: null);
        }

        string? launchSettingsPath = LaunchSettings.TryFindLaunchSettingsFile(
            projectOrEntryPointFilePath,
            launchProfile,
            report);
        if (launchSettingsPath is null)
        {
            return LaunchProfileParseResult.Success(model: null);
        }

        if (reportUsingLaunchSettings)
        {
            report(string.Format(CliCommandStrings.UsingLaunchSettingsFromMessage, launchSettingsPath), true);
        }

        return LaunchSettings.ReadProfileSettingsFromFile(launchSettingsPath, launchProfile);
    }

    /// <summary>
    /// Applies launch-profile environment variables followed by command-line or evaluated overrides.
    /// </summary>
    /// <param name="launchProfile">The selected launch profile.</param>
    /// <param name="environmentVariables">Environment variables that override profile values.</param>
    /// <param name="apply">Applies one environment variable to the launch.</param>
    public static void ApplyLaunchEnvironmentVariables(
        LaunchProfile? launchProfile,
        IReadOnlyDictionary<string, string> environmentVariables,
        Action<string, string?> apply)
    {
        if (launchProfile is ProjectLaunchProfile { ApplicationUrl.Length: > 0 } projectProfile)
        {
            apply("ASPNETCORE_URLS", projectProfile.ApplicationUrl);
        }

        if (launchProfile is not null)
        {
            apply("DOTNET_LAUNCH_PROFILE", launchProfile.LaunchProfileName);
            foreach ((string name, string value) in launchProfile.EnvironmentVariables)
            {
                apply(name, value);
            }
        }

        foreach ((string name, string value) in environmentVariables)
        {
            apply(name, value);
        }
    }

#if !CLI_AOT
    /// <summary>
    /// Creates a TerminalLogger or ConsoleLogger based on the provided MSBuild arguments.
    /// If the environment is detected to be an LLM environment, the logger is adjusted to
    /// better suit that environment.
    /// </summary>
    /// <remarks>
    /// This uses the in-process MSBuild logging APIs (<c>Microsoft.Build.*</c>) and so is excluded
    /// from the AOT build, which only ever forwards MSBuild out-of-process.
    /// </remarks>
    public static Microsoft.Build.Framework.ILogger GetConsoleLogger(MSBuildArgs args) =>
        Microsoft.Build.Logging.TerminalLogger.CreateTerminalOrConsoleLogger([.. AdjustMSBuildForLLMs(args).OtherMSBuildArgs]);
#endif
}
