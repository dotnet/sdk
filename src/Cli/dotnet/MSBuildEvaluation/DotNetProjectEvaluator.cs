// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable RS0030 // Allowed to use MSBuild APIs in this file.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Cli.MSBuildEvaluation;

/// <summary>
/// Manages project evaluation with caching and consistent global properties.
/// This class provides the primary entry point for loading and evaluating projects
/// while ensuring telemetry integration and proper resource management.
/// </summary>
public sealed class DotNetProjectEvaluator : IDisposable
{
    private readonly ProjectCollection _projectCollection;
    private readonly ILogger? _telemetryCentralLogger;
    private readonly Dictionary<string, DotNetProject> _projectCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the DotNetProjectEvaluator class.
    /// </summary>
    /// <param name="globalProperties">Global properties to use for all project evaluations.</param>
    /// <param name="loggers">Additional loggers to include (telemetry logger is added automatically).</param>
    public DotNetProjectEvaluator(IDictionary<string, string>? globalProperties = null, IEnumerable<ILogger>? loggers = null)
    {
        var (allLoggers, telemetryCentralLogger) = TelemetryUtilities.CreateLoggersWithTelemetry(loggers);
        _telemetryCentralLogger = telemetryCentralLogger;


        _projectCollection = new ProjectCollection(
            globalProperties: globalProperties,
            loggers: allLoggers,
            toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);
    }

    public IReadOnlyDictionary<string, string> GlobalProperties => _projectCollection.GlobalProperties.AsReadOnly();

    /// <summary>
    /// Gets the telemetry central logger that can be reused for build operations.
    /// </summary>
    internal ILogger? TelemetryCentralLogger => _telemetryCentralLogger;

    /// <summary>
    /// Gets the underlying ProjectCollection for scenarios that need direct access.
    /// This should be used sparingly and only for compatibility with existing code.
    /// </summary>
    public ProjectCollection ProjectCollection => _projectCollection;

    /// <summary>
    /// Loads and evaluates a project from the specified path.
    /// Results are cached for subsequent requests with the same path and global properties.
    /// </summary>
    /// <param name="projectPath">The path to the project file to load.</param>
    /// <returns>A DotNetProject wrapper around the loaded project.</returns>
    /// <exception cref="ArgumentException">Thrown when projectPath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the project file doesn't exist.</exception>
    public DotNetProject LoadProject(string projectPath)
    {
        return LoadProject(projectPath, additionalGlobalProperties: null);
    }

    /// <summary>
    /// Loads and evaluates a project from the specified path with additional global properties.
    /// </summary>
    /// <param name="projectPath">The path to the project file to load.</param>
    /// <param name="additionalGlobalProperties">Additional global properties to merge with the base properties.</param>
    /// <param name="useFlexibleLoading">If true, allows flexible loading of projects with missing imports. Defaults to false.</param>
    /// <returns>A DotNetProject wrapper around the loaded project.</returns>
    /// <exception cref="ArgumentException">Thrown when projectPath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the project file doesn't exist.</exception>
    public DotNetProject LoadProject(string projectPath, IDictionary<string, string>? additionalGlobalProperties, bool useFlexibleLoading = false)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DotNetProjectEvaluator));
        }

        if (string.IsNullOrEmpty(projectPath))
        {
            throw new ArgumentException("Project path cannot be null or empty.", nameof(projectPath));
        }

        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException($"Project file not found: {projectPath}", projectPath);
        }

        // Create a cache key that includes the project path and any additional properties
        string cacheKey = CreateCacheKey(projectPath, additionalGlobalProperties);

        if (!_projectCache.TryGetValue(cacheKey, out var cachedProject))
        {
            // If we have additional global properties, we need to create a new ProjectCollection
            // with the merged properties, otherwise we can use the existing one
            ProjectCollection collectionToUse = _projectCollection;
            if (additionalGlobalProperties?.Count > 0)
            {
                var mergedProperties = new Dictionary<string, string>(_projectCollection.GlobalProperties, StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in additionalGlobalProperties)
                {
                    mergedProperties[kvp.Key] = kvp.Value;
                }

                // For now, create a temporary collection. In the future, we could cache these too.
                var (allLoggers, _) = TelemetryUtilities.CreateLoggersWithTelemetry();
                collectionToUse = new ProjectCollection(
                    globalProperties: mergedProperties,
                    loggers: allLoggers,
                    toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);
            }

            try
            {
                var settings = useFlexibleLoading
                    ? ProjectLoadSettings.IgnoreMissingImports | ProjectLoadSettings.IgnoreEmptyImports | ProjectLoadSettings.IgnoreInvalidImports
                    : ProjectLoadSettings.Default;
                var project = new Project(projectPath, globalProperties: null, toolsVersion: null, projectCollection: collectionToUse, loadSettings: settings);
                cachedProject = new DotNetProject(project);
                _projectCache[cacheKey] = cachedProject;
            }
            finally
            {
                // Dispose the temporary collection if we created one
                if (collectionToUse != _projectCollection)
                {
                    collectionToUse.Dispose();
                }
            }
        }

        return cachedProject;
    }

    /// <summary>
    /// Loads and evaluates multiple projects in parallel.
    /// This is more efficient than calling LoadProject multiple times for solution scenarios.
    /// </summary>
    /// <param name="projectPaths">The paths to the project files to load.</param>
    /// <returns>An enumerable of DotNetProject wrappers.</returns>
    public IEnumerable<DotNetProject> LoadProjects(IEnumerable<string> projectPaths)
    {
        if (projectPaths == null)
        {
            throw new ArgumentNullException(nameof(projectPaths));
        }

        var paths = projectPaths.ToArray();
        if (paths.Length == 0)
        {
            return [];
        }

        // Load projects in parallel for better performance
        return paths.AsParallel().Select(LoadProject);
    }

    /// <summary>
    /// Creates a project builder for build operations on the specified project.
    /// The builder will reuse the telemetry central logger from this evaluator.
    /// </summary>
    /// <param name="project">The project to create a builder for.</param>
    /// <returns>A DotNetProjectBuilder configured with telemetry integration.</returns>
    public DotNetProjectBuilder CreateBuilder(DotNetProject project)
    {
        if (project == null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        return new DotNetProjectBuilder(project, this);
    }

    private static string CreateCacheKey(string projectPath, IDictionary<string, string>? additionalGlobalProperties)
    {
        if (additionalGlobalProperties?.Count > 0)
        {
            var sortedProperties = additionalGlobalProperties
                .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => $"{kvp.Key}={kvp.Value}");
            return $"{projectPath}|{string.Join(";", sortedProperties)}";
        }

        return projectPath;
    }

    /// <summary>
    /// Releases all resources used by the DotNetProjectEvaluator.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _projectCollection?.UnloadAllProjects();
            _projectCollection?.Dispose();
            _projectCache.Clear();
            _disposed = true;
        }
    }
}
