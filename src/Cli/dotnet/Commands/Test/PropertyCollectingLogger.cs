// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal class PropertyCollectingLogger : ILogger
{
    // Key: Composite key (ProjectContextId, ProjectFilePath), Value: Property dictionary for that context
    private readonly Dictionary<(int ContextId, string ProjectPath), IReadOnlyDictionary<string, string>> _buildContexts = new();

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
        ProjectProperties.BuildInParallel,
        ProjectProperties.OutputType
    };

    public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> CollectedProperties
    {
        get
        {
            var result = new Dictionary<string, List<IReadOnlyDictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in _buildContexts)
            {
                string projectPath = kvp.Key.ProjectPath;

                if (!string.IsNullOrEmpty(projectPath))
                {
                    // Group by project file path
                    if (!result.TryGetValue(projectPath, out var list))
                    {
                        list = new List<IReadOnlyDictionary<string, string>>();
                        result[projectPath] = list;
                    }

                    list.Add(kvp.Value);

                }
            }

            // Convert to the required return type
            return result.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<IReadOnlyDictionary<string, string>>)kvp.Value.AsReadOnly(),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Initialize(IEventSource eventSource)
    {
        // Listen for custom property messages
        eventSource.MessageRaised += (sender, e) =>
        {
            if (e is PropertyReassignmentEventArgs args)
            {
                if (AdditionalPropNames.Contains(args.PropertyName, StringComparer.OrdinalIgnoreCase) &&
                    args.BuildEventContext?.ProjectContextId != BuildEventContext.InvalidProjectContextId)
                {
                    UpdateProjectProperty(
                        args.BuildEventContext!.ProjectContextId,
                        args.ProjectFile!,
                        args.PropertyName,
                        args.NewValue
                    );
                }
            }
            else if (e.Message?.StartsWith("DOTNET_TEST_PROPS:", StringComparison.Ordinal) == true)
            {
                // Parse custom property message
                ParseAndStorePropertyMessage(e.Message, e.BuildEventContext);
            }
        };
    }

    private void ParseAndStorePropertyMessage(string message, BuildEventContext? context)
    {
        if (context?.ProjectContextId == BuildEventContext.InvalidProjectContextId || context == null)
            return;

        try
        {
            // Expected format: "DOTNET_TEST_PROPS: ProjectPath=...|TargetFramework=...|RunCommand=...|..."
            var propsData = message.Substring("DOTNET_TEST_PROPS:".Length).Trim();
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in propsData.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                var keyValue = pair.Split('=', 2);
                if (keyValue.Length == 2)
                {
                    properties[keyValue[0].Trim()] = keyValue[1].Trim();
                }
            }

            if (properties.TryGetValue(ProjectProperties.ProjectFullPath, out string? projectPath) && !string.IsNullOrEmpty(projectPath))
            {
                AddProjectProperties(context.ProjectContextId, projectPath, properties);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse property message: {ex.Message}");
        }
    }

    private void AddProjectProperties(int contextId, string projectFilePath, Dictionary<string, string> props)
    {
        // Simply skip invalid context IDs
        if (contextId == BuildEventContext.InvalidProjectContextId || contextId < 0)
        {
            // Console.WriteLine($"Skipping invalid ContextId {contextId} for {projectFilePath}");
            return;
        }

        var key = (contextId, projectFilePath);
        _buildContexts[key] = props;
    }

    private void UpdateProjectProperty(int contextId, string projectFilePath, string propertyName, string newValue)
    {
        // Skip invalid context IDs
        if (contextId == BuildEventContext.InvalidProjectContextId || contextId < 0)
        {
            return;
        }

        var key = (contextId, projectFilePath);

        if (_buildContexts.TryGetValue(key, out var existingProps))
        {
            var updatedProps = new Dictionary<string, string>(existingProps, StringComparer.OrdinalIgnoreCase)
            {
                [propertyName] = newValue
            };
            _buildContexts[key] = updatedProps;
        }
        else
        {
            // Only create entries for valid context IDs
            var minimalProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProjectProperties.ProjectFullPath] = projectFilePath,
                [propertyName] = newValue
            };

            _buildContexts[key] = minimalProps;
        }
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
