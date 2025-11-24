// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Packaging;

namespace Microsoft.DotNet.Cli.MSBuildEvaluation;

/// <summary>
/// Factory for creating DotNetProjectEvaluator instances with standard configurations
/// used throughout the dotnet CLI commands.
/// </summary>
public static class DotNetProjectEvaluatorFactory
{
    /// <summary>
    /// Creates a DotNetProjectEvaluator with standard configuration for command usage.
    /// This includes MSBuildExtensionsPath and properties from command line arguments.
    /// </summary>
    /// <param name="msbuildArgs">Optional MSBuild arguments to extract properties from.</param>
    /// <param name="additionalLoggers">Additional loggers to include.</param>
    /// <returns>A configured DotNetProjectEvaluator.</returns>
    public static DotNetProjectEvaluator CreateForCommand(MSBuildArgs? msbuildArgs = null, IEnumerable<ILogger>? additionalLoggers = null)
    {
        var globalProperties = CommonRunHelpers.GetGlobalPropertiesFromArgs(msbuildArgs ?? MSBuildArgs.ForHelp);
        return new DotNetProjectEvaluator(globalProperties, additionalLoggers);
    }

    /// <summary>
    /// Creates a DotNetProjectEvaluator optimized for restore operations.
    /// This includes restore optimization properties that disable default item globbing.
    /// </summary>
    /// <param name="msbuildArgs">Optional MSBuild arguments to extract properties from.</param>
    /// <param name="additionalLoggers">Additional loggers to include.</param>
    /// <returns>A configured DotNetProjectEvaluator for restore scenarios.</returns>
    public static DotNetProjectEvaluator CreateForRestore(MSBuildArgs? msbuildArgs = null, IEnumerable<ILogger>? additionalLoggers = null)
    {
        var globalProperties = CommonRunHelpers.GetGlobalPropertiesFromArgs(msbuildArgs ?? MSBuildArgs.ForHelp);

        foreach (var kvp in RestoringCommand.RestoreOptimizationProperties)
        {
            globalProperties[kvp.Key] = kvp.Value;
        }

        return new DotNetProjectEvaluator(globalProperties, additionalLoggers);
    }

    /// <summary>
    /// Creates a DotNetProjectEvaluator for template evaluation operations.
    /// This configuration is used by the template engine for project analysis.
    /// </summary>
    /// <param name="additionalLoggers">Additional loggers to include.</param>
    /// <returns>A configured DotNetProjectEvaluator for template scenarios.</returns>
    public static DotNetProjectEvaluator CreateForTemplate(IEnumerable<ILogger>? additionalLoggers = null)
    {
        var globalProperties = GetBaseGlobalProperties();
        return new DotNetProjectEvaluator(globalProperties, additionalLoggers);
    }

    /// <summary>
    /// Creates a DotNetProjectEvaluator for release property detection.
    /// This is used by commands like publish and pack to determine if release optimizations should be applied.
    /// </summary>
    /// <param name="targetFramework">Optional target framework to scope the evaluation to.</param>
    /// <param name="configuration">Optional configuration (Debug/Release).</param>
    /// <param name="additionalLoggers">Additional loggers to include.</param>
    /// <returns>A configured DotNetProjectEvaluator for release property detection.</returns>
    public static DotNetProjectEvaluator CreateForReleaseProperty(ReadOnlyDictionary<string, string>? userProperties, string? targetFramework = null, string? configuration = null, IEnumerable<ILogger>? additionalLoggers = null)
    {
        var globalProperties = GetBaseGlobalProperties();
        if (userProperties != null)
        {
            globalProperties.AddRange(userProperties);
        }

        if (!string.IsNullOrEmpty(targetFramework))
        {
            globalProperties["TargetFramework"] = targetFramework;
        }

        if (!string.IsNullOrEmpty(configuration))
        {
            globalProperties["Configuration"] = configuration;
        }

        return new DotNetProjectEvaluator(globalProperties, additionalLoggers);
    }

    /// <summary>
    /// Creates a DotNetProjectEvaluator with custom global properties.
    /// </summary>
    /// <param name="globalProperties">Custom global properties to use.</param>
    /// <param name="additionalLoggers">Additional loggers to include.</param>
    /// <returns>A configured DotNetProjectEvaluator.</returns>
    public static DotNetProjectEvaluator Create(IDictionary<string, string>? globalProperties = null, IEnumerable<ILogger>? additionalLoggers = null)
    {
        var mergedProperties = GetBaseGlobalProperties();

        if (globalProperties != null)
        {
            foreach (var kvp in globalProperties)
            {
                mergedProperties[kvp.Key] = kvp.Value;
            }
        }

        return new DotNetProjectEvaluator(mergedProperties, additionalLoggers);
    }

    /// <summary>
    /// Gets the base global properties that are common across all evaluator configurations.
    /// </summary>
    /// <returns>A dictionary of base global properties.</returns>
    private static Dictionary<string, string> GetBaseGlobalProperties()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Constants.MSBuildExtensionsPath] = AppContext.BaseDirectory
        };
    }

    /// <summary>
    /// Creates global properties from MSBuild arguments with virtual project settings.
    /// This is used for scenarios where projects might not exist on disk.
    /// </summary>
    /// <param name="msbuildArgs">MSBuild arguments to extract properties from.</param>
    /// <param name="additionalLoggers">Additional loggers to include.</param>
    /// <returns>A configured DotNetProjectEvaluator for virtual project scenarios.</returns>
    public static DotNetProjectEvaluator CreateForVirtualProject(MSBuildArgs? msbuildArgs = null, IEnumerable<ILogger>? additionalLoggers = null)
    {
        var globalProperties = GetBaseGlobalProperties();

        // Add virtual project properties
        globalProperties["_BuildNonexistentProjectsByDefault"] = "true";
        globalProperties["RestoreUseSkipNonexistentTargets"] = "false";
        globalProperties["ProvideCommandLineArgs"] = "true";

        // Add restore optimization properties for better performance
        foreach (var kvp in RestoringCommand.RestoreOptimizationProperties)
        {
            globalProperties[kvp.Key] = kvp.Value;
        }

        // Add properties from command line arguments
        if (msbuildArgs != null)
        {
            var argsProperties = CommonRunHelpers.GetGlobalPropertiesFromArgs(msbuildArgs);
            foreach (var kvp in argsProperties)
            {
                globalProperties[kvp.Key] = kvp.Value;
            }
        }

        return new DotNetProjectEvaluator(globalProperties, additionalLoggers);
    }
}
