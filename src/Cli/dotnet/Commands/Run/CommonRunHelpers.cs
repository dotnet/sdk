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
    public static Dictionary<string, string> GetGlobalPropertiesFromArgs(string[] args)
    {
        var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // This property disables default item globbing to improve performance
            // This should be safe because we are not evaluating items, only properties
            { Constants.EnableDefaultItems,  "false" },
            { Constants.MSBuildExtensionsPath, AppContext.BaseDirectory }
        };

        var msbuildArgs = MSBuildArgs.AnalyzeMSBuildArguments(args, CommonOptions.PropertiesOption, CommonOptions.RestorePropertiesOption, CommonOptions.MSBuildTargetOption(), CommonOptions.VerbosityOption());
        if (msbuildArgs.GlobalProperties is null)
        {
            return globalProperties;
        }
        foreach (var kv in msbuildArgs.GlobalProperties)
        {
            // If the property is already set, we don't override it
            globalProperties[kv.Key] = kv.Value;
        }
        return globalProperties;
    }
}
