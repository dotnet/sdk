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
        ProjectProperties.BuildInParallel
    };

    public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> CollectedProperties
    {
        get
        {
            var result = new Dictionary<string, List<IReadOnlyDictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in _buildContexts)
            {
                //Console.WriteLine(kvp.Key.ContextId + " " + kvp.Key.ProjectPath);
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

                    //foreach (var prop in kvp.Value)
                    //{
                    //    Console.WriteLine($"  {prop.Key}: {prop.Value}");
                    //}
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
        //eventSource.ProjectStarted += (sender, e) =>
        //{
        //    Console.WriteLine($"🔍 ProjectStarted: ContextId={e.BuildEventContext?.ProjectContextId}, Project={Path.GetFileName(e.ProjectFile)}");
        //};

        //eventSource.ProjectFinished += (sender, e) =>
        //{
        //    if (e.BuildEventContext?.ProjectContextId != BuildEventContext.InvalidProjectContextId)
        //    {
        //        Console.WriteLine($"🏁 ProjectFinished: ContextId={e.BuildEventContext?.ProjectContextId}, Project={Path.GetFileName(e.ProjectFile)}");
        //    }
        //};

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

            if (properties.TryGetValue("MSBuildProjectFullPath", out string? projectPath) && !string.IsNullOrEmpty(projectPath))
            {
                //Console.WriteLine($"Parsed properties for ContextId {context.ProjectContextId}: {properties.Count} properties");
                AddProjectProperties(context.ProjectContextId, projectPath, properties);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse property message: {ex.Message}");
        }
    }
    private static bool IsTestRelatedTarget(string targetName)
    {
        // Common test-related targets that would have computed final properties
        return targetName switch
        {
            "Build" => true,
            "CoreBuild" => true,
            "ComputeRunArguments" => true,
            "GetTargetPath" => true,
            "GetCopyToOutputDirectoryItems" => true,
            "ResolveAssemblyReferences" => true,
            _ => targetName.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                 targetName.Contains("Run", StringComparison.OrdinalIgnoreCase)
        };
    }

    private bool TryGetPropertiesFromTarget(TargetFinishedEventArgs e, out Dictionary<string, string>? props)
    {
        props = null;

        // TargetFinishedEventArgs doesn't directly expose properties
        // We need to use a different approach - checking if the project file is a test project
        // and capturing properties we can access

        if (string.IsNullOrEmpty(e.ProjectFile))
            return false;

        // For now, create a basic property set and rely on PropertyReassignment to fill in details
        // This is a limitation of the TargetFinished event - it doesn't expose properties directly
        props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProjectProperties.ProjectFullPath] = e.ProjectFile,
            // We'll need to determine test project status differently
            // or rely on PropertyReassignment events to populate these
        };

        // Check if this looks like a test project based on file name/path
        string fileName = Path.GetFileNameWithoutExtension(e.ProjectFile);
        if (fileName.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("Tests", StringComparison.OrdinalIgnoreCase) ||
            e.ProjectFile.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"🧪 Detected potential test project: {fileName}");
            return true;
        }

        return false;
    }

    private static bool TryCollectProperties(
      string? projectFile,
      IEnumerable? properties,
      out Dictionary<string, string>? props)
    {
        props = null;
        if (string.IsNullOrEmpty(projectFile) || properties == null)
            return false;

        // print the value of istestproject and istestingplatformapplication

        bool.TryParse(properties.GetPropertyValue(ProjectProperties.IsTestProject), out bool isTestProject);
        bool.TryParse(properties.GetPropertyValue(ProjectProperties.IsTestingPlatformApplication), out bool isTestingPlatformApplication);
        // Add debug output here

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

    private void AddProjectProperties(int contextId, string projectFilePath, Dictionary<string, string> props)
    {
        // Simply skip invalid context IDs
        if (contextId == BuildEventContext.InvalidProjectContextId || contextId < 0)
        {
            // Optionally log for debugging, but don't save
            // Console.WriteLine($"Skipping invalid ContextId {contextId} for {projectFilePath}");
            return;
        }

        var key = (contextId, projectFilePath);

        //if (_buildContexts.ContainsKey(key))
        //{
        //    Console.WriteLine($"WARNING: Replacing existing data for ContextId {contextId}, Project {projectFilePath}");
        //}
        //else
        //{
        //    Console.WriteLine($"Adding new entry: ContextId {contextId}, Project {projectFilePath}");
        //}

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
            //Console.WriteLine($"Updated property {propertyName} = {newValue} for ContextId {contextId}, Project {projectFilePath}");
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
            //Console.WriteLine($"Created minimal entry for ContextId {contextId}, Project {projectFilePath}");
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
