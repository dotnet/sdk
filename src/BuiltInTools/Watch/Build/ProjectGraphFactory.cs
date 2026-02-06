// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.DotNet.ProjectTools;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.DotNet.Watch;

internal sealed class ProjectGraphFactory
{
    // Roughly matches the VS project load settings for their design time builds.
    private const ProjectLoadSettings ProjectLoadSettings =
        ProjectLoadSettings.RejectCircularImports |
        ProjectLoadSettings.IgnoreEmptyImports |
        ProjectLoadSettings.IgnoreMissingImports |
        ProjectLoadSettings.IgnoreInvalidImports |
        ProjectLoadSettings.DoNotEvaluateElementsWithFalseCondition |
        ProjectLoadSettings.FailOnUnresolvedSdk |
        // Glob evaluation requires this setting. If not present MSBuild reevaluates the project with the setting.
        ProjectLoadSettings.RecordEvaluatedItemElements;

    /// <summary>
    /// Reuse <see cref="ProjectCollection"/> with XML element caching to improve performance.
    ///
    /// The cache is automatically updated when build files change.
    /// https://github.com/dotnet/msbuild/blob/b6f853defccd64ae1e9c7cf140e7e4de68bff07c/src/Build/Definition/ProjectCollection.cs#L343-L354
    /// </summary>
    private readonly ProjectCollection _collection;

    private readonly ImmutableDictionary<string, string> _globalOptions;
    private readonly ProjectRepresentation _rootProject;

    // Only the root project can be virtual. #:project does not support targeting other single-file projects.
    private readonly VirtualProjectBuilder? _virtualRootProjectBuilder;

    public ProjectGraphFactory(
        ProjectRepresentation rootProject,
        string? targetFramework,
        ImmutableDictionary<string, string> globalOptions)
    {
        _collection = new(
            globalProperties: globalOptions,
            loggers: [],
            remoteLoggers: [],
            ToolsetDefinitionLocations.Default,
            maxNodeCount: 1,
            onlyLogCriticalEvents: false,
            loadProjectsReadOnly: false,
            useAsynchronousLogging: false,
            reuseProjectRootElementCache: true);

        _globalOptions = globalOptions;
        _rootProject = rootProject;

        if (rootProject.EntryPointFilePath != null)
        {
            _virtualRootProjectBuilder = new VirtualProjectBuilder(rootProject.EntryPointFilePath, targetFramework ?? GetProductTargetFramework());
        }
    }

    private static string GetProductTargetFramework()
    {
        var attribute = typeof(VirtualProjectBuilder).Assembly.GetCustomAttribute<TargetFrameworkAttribute>() ?? throw new InvalidOperationException();
        var version = new FrameworkName(attribute.FrameworkName).Version;
        return $"net{version.Major}.{version.Minor}";
    }

    /// <summary>
    /// Tries to create a project graph by running the build evaluation phase on the <see cref="rootProjectFile"/>.
    /// </summary>
    public ProjectGraph? TryLoadProjectGraph(
        ILogger logger,
        bool projectGraphRequired,
        out IReadOnlyDictionary<ProjectInstance, Project> projects,
        CancellationToken cancellationToken)
    {
        var entryPoint = new ProjectGraphEntryPoint(_rootProject.ProjectGraphPath, _globalOptions);
        var projectsBuilder = new Dictionary<ProjectInstance, Project>();
        projects = projectsBuilder;

        try
        {
            return new ProjectGraph([entryPoint], _collection, (path, globalProperties, collection) => CreateProjectInstance(path, globalProperties, collection, projectsBuilder, logger), cancellationToken);
        }
        catch (ProjectCreationFailedException)
        {
            // Errors have already been reported.
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // ProjectGraph aggregates OperationCanceledException exception,
            // throw here to propagate the cancellation.
            cancellationToken.ThrowIfCancellationRequested();

            logger.LogDebug("Failed to load project graph.");

            if (e is AggregateException { InnerExceptions: var innerExceptions })
            {
                foreach (var inner in innerExceptions)
                {
                    if (inner is not ProjectCreationFailedException)
                    {
                        Report(inner);
                    }
                }
            }
            else
            {
                Report(e);
            }

            void Report(Exception e)
            {
                if (projectGraphRequired)
                {
                    logger.LogError(e.Message);
                }
                else
                {
                    logger.LogWarning(e.Message);
                }
            }
        }

        return null;
    }

    private ProjectInstance CreateProjectInstance(string projectPath, Dictionary<string, string> globalProperties, ProjectCollection projectCollection, Dictionary<ProjectInstance, Project> projects, ILogger logger)
    {
        Project project;
        ProjectInstance projectInstance;

        if (_virtualRootProjectBuilder != null && projectPath == _rootProject.ProjectGraphPath)
        {
            var anyError = false;

            _virtualRootProjectBuilder.CreateProjectInstance(
                projectCollection,
                (sourceFile, textSpan, message) =>
                {
                    anyError = true;
                    logger.LogError("{Location}: {Message}", sourceFile.GetLocationString(textSpan), message);
                },
                out projectInstance,
                out var projectRootElement,
                out _);

            if (anyError)
            {
                throw new ProjectCreationFailedException();
            }

            project = CreateProject(projectRootElement);
        }
        else
        {
            var projectRootElement = ProjectRootElement.Open(projectPath, projectCollection, preserveFormatting: false);
            project = CreateProject(projectRootElement);
            projectInstance = new ProjectInstance(project, ProjectInstanceSettings.None);
        }

        lock (projects)
        {
            projects.Add(projectInstance, project);
        }

        return projectInstance;

        Project CreateProject(ProjectRootElement projectRootElement)
            => new(projectRootElement, globalProperties, toolsVersion: "Current", projectCollection, ProjectLoadSettings);
    }

    private sealed class ProjectCreationFailedException() : Exception();
}
