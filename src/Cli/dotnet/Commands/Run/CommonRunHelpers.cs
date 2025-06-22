// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Run;

internal static class CommonRunHelpers
{
    /// <param name="globalProperties">
    /// Should have <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// </param>
    public static void AddUserPassedProperties(Dictionary<string, string> globalProperties, IReadOnlyList<string> args)
    {
        Debug.Assert(globalProperties.Comparer == StringComparer.OrdinalIgnoreCase);

        var fakeCommand = new System.CommandLine.Command("dotnet") { CommonOptions.PropertiesOption };
        var propertyParsingConfiguration = new System.CommandLine.CommandLineConfiguration(fakeCommand);
        var propertyParseResult = propertyParsingConfiguration.Parse(args);
        var propertyValues = propertyParseResult.GetValue(CommonOptions.PropertiesOption);

        if (propertyValues is null)
        {
            return;
        }

        foreach (var property in propertyValues)
        {
            foreach (var (key, value) in MSBuildPropertyParser.ParseProperties(property))
            {
                globalProperties[key] = value;
            }
        }
    }

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

        AddUserPassedProperties(globalProperties, args);
        return globalProperties;
    }
}
