// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal class PropertyCollectingLogger : ILogger
{
    // Key: ProjectFile, Value: List of property dictionaries (one per context/TFM)
    private readonly Dictionary<string, List<IReadOnlyDictionary<string, string>>> _buildContexts = new(StringComparer.OrdinalIgnoreCase);

    public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;

    private string? _parameters;
    public string? Parameters
    {
        get => _parameters;
        set => _parameters = value;
    }

    // Properties to collect (excluding the three key ones)
    private static readonly string[] AdditionalPropNames =
    {
        ProjectProperties.TargetFrameworks,
        ProjectProperties.TargetPath,
        ProjectProperties.ProjectFullPath,
        ProjectProperties.RunCommand,
        ProjectProperties.RunArguments,
        ProjectProperties.RunWorkingDirectory,
        ProjectProperties.AppDesignerFolder,
        ProjectProperties.TestTfmsInParallel,
        ProjectProperties.BuildInParallel
    };

    public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> CollectedProperties
        => _buildContexts.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<IReadOnlyDictionary<string, string>>)kvp.Value.AsReadOnly(),
            StringComparer.OrdinalIgnoreCase);

    public void Initialize(IEventSource eventSource)
    {
        eventSource.ProjectStarted += (sender, e) =>
        {
            if (TryCollectProperties(e.ProjectFile, e.Properties, out var props))
            {
                AddProjectProperties(e.ProjectFile!, props!);
            }
        };

        eventSource.ProjectFinished += (sender, e) => { /* No action needed */ };

        eventSource.StatusEventRaised += (sender, e) =>
        {
            if (e is ProjectEvaluationFinishedEventArgs args)
            {
                if (TryCollectProperties(args.ProjectFile, args.Properties, out var props))
                {
                    AddProjectProperties(args.ProjectFile!, props!);
                }
            }
        };
    }

    private static bool TryCollectProperties(
        string? projectFile,
        IEnumerable? properties,
        out Dictionary<string, string>? props)
    {
        props = null;
        if (string.IsNullOrEmpty(projectFile) || properties == null)
            return false;

        bool.TryParse(properties.GetPropertyValue(ProjectProperties.IsTestProject), out bool isTestProject);
        bool.TryParse(properties.GetPropertyValue(ProjectProperties.IsTestingPlatformApplication), out bool isTestingPlatformApplication);
        string targetFramework = properties.GetPropertyValue(ProjectProperties.TargetFramework);

        if (!isTestProject && !isTestingPlatformApplication)
            return false;

        if (string.IsNullOrWhiteSpace(targetFramework))
            return false;

        props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProjectProperties.IsTestProject] = isTestProject.ToString(),
            [ProjectProperties.IsTestingPlatformApplication] = isTestingPlatformApplication.ToString(),
            [ProjectProperties.TargetFramework] = targetFramework
        };

        foreach (var propName in AdditionalPropNames)
        {
            props[propName] = properties.GetPropertyValue(propName);
        }

        return true;
    }

    private void AddProjectProperties(string projectFile, Dictionary<string, string> props)
    {
        if (!_buildContexts.TryGetValue(projectFile, out var list))
        {
            list = new List<IReadOnlyDictionary<string, string>>();
            _buildContexts[projectFile] = list;
        }
        list.Add(props);
    }

    public void Shutdown() { }
}

public static class MSBuildLoggerExtensions
{
    public static string GetPropertyValue(this IEnumerable properties, string key)
    {
        foreach (var prop in properties)
        {
            if (prop is Microsoft.Build.Execution.ProjectPropertyInstance p && p.Name == key)
                return p.EvaluatedValue ?? string.Empty;

            if (prop is DictionaryEntry entry && entry.Key?.ToString() == key)
                return entry.Value?.ToString() ?? string.Empty;
        }
        return string.Empty;
    }
}
