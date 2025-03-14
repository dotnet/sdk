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
    public static void AddUserPassedProperties(Dictionary<string, string> globalProperties, string[]? userPassedProperties)
    {
        Debug.Assert(globalProperties.Comparer == StringComparer.OrdinalIgnoreCase);

        if (userPassedProperties != null)
        {
            foreach (var property in userPassedProperties)
            {
                foreach (var (key, value) in MSBuildPropertyParser.ParseProperties(property))
                {
                    globalProperties[key] = value;
                }
            }
        }
    }

    public static ProjectInstance EvaluateProject(string? projectFilePath, Func<ProjectCollection, ProjectInstance>? projectFactory, string[]? args, ILogger? binaryLogger)
    {
        Debug.Assert(projectFilePath is not null || projectFactory is not null);

        var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // This property disables default item globbing to improve performance
            // This should be safe because we are not evaluating items, only properties
            { Constants.EnableDefaultItems,  "false" },
            { Constants.MSBuildExtensionsPath, AppContext.BaseDirectory }
        };

        AddUserPassedProperties(globalProperties, GetUserPassedPropertiesFromArgs(args));

        var collection = new ProjectCollection(globalProperties: globalProperties, loggers: binaryLogger is null ? null : [binaryLogger], toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);

        if (projectFilePath is not null)
        {
            return collection.LoadProject(projectFilePath).CreateProjectInstance();
        }

        Debug.Assert(projectFactory is not null);
        return projectFactory(collection);
    }

    public static string[]? GetUserPassedPropertiesFromArgs(string[] args)
    {
        var fakeCommand = new System.CommandLine.CliCommand("dotnet") { CommonOptions.PropertiesOption };
        var propertyParsingConfiguration = new System.CommandLine.CliConfiguration(fakeCommand);
        var propertyParseResult = propertyParsingConfiguration.Parse(args);
        return propertyParseResult.GetValue(CommonOptions.PropertiesOption);
    }
}
