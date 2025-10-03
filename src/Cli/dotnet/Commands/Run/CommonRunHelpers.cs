// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Run;

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

    public static string GetPropertiesLaunchSettingsPath(string directoryPath, string propertiesDirectoryName)
        => Path.Combine(directoryPath, propertiesDirectoryName, "launchSettings.json");

    public static string GetFlatLaunchSettingsPath(string directoryPath, string projectNameWithoutExtension)
        => Path.Join(directoryPath, $"{projectNameWithoutExtension}.run.json");


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
    /// Creates a TerminalLogger or ConsoleLogger based on the provided MSBuild arguments.
    /// If the environment is detected to be an LLM environment, the logger is adjusted to
    /// better suit that environment.
    /// </summary>
    public static Microsoft.Build.Framework.ILogger GetConsoleLogger(MSBuildArgs args) =>
        Microsoft.Build.Logging.TerminalLogger.CreateTerminalOrConsoleLogger([.. AdjustMSBuildForLLMs(args).OtherMSBuildArgs]);
}
