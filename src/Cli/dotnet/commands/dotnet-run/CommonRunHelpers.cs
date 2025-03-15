// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli;

internal static class CommonRunHelpers
{
    /// <param name="globalProperties">
    /// Should have <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// </param>
    public static void AddUserPassedProperties(Dictionary<string, string> globalProperties, string[]? args)
    {
        Debug.Assert(globalProperties.Comparer == StringComparer.OrdinalIgnoreCase);

        var fakeCommand = new System.CommandLine.CliCommand("dotnet") { CommonOptions.PropertiesOption };
        var propertyParsingConfiguration = new System.CommandLine.CliConfiguration(fakeCommand);
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

    public static Dictionary<string, string> GetGlobalPropertiesFromArgs(string[]? args)
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
