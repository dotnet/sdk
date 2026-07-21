// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectTools;

using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils.Extensions;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli;

internal class MsbuildProject
{
    public static string GetProjectFileFromDirectory(string projectDirectory)
        => ProjectLocator.TryGetProjectFileFromDirectory(projectDirectory, out var projectFilePath, out var error)
            ? projectFilePath
            : throw new GracefulException(error);

    public static bool TryGetProjectFileFromDirectory(string projectDirectory, [NotNullWhen(true)] out string projectFilePath)
        => ProjectLocator.TryGetProjectFileFromDirectory(projectDirectory, out projectFilePath, out _);

    const string ProjectItemElementType = "ProjectReference";

    public ProjectRootElement ProjectRootElement { get; private set; }
    public string ProjectDirectory { get; private set; }

    private readonly ProjectCollection _projects;
    private List<NuGetFramework> _cachedTfms = null;
    private IEnumerable<string> cachedRuntimeIdentifiers;
    private IEnumerable<string> cachedConfigurations;
    private ProjectInstance _cachedEvaluatedProject;
    private readonly bool _interactive = false;

    private MsbuildProject(ProjectCollection projects, ProjectRootElement project, bool interactive)
    {
        _projects = projects;
        ProjectRootElement = project;
        ProjectDirectory = PathUtilities.EnsureTrailingSlash(ProjectRootElement.DirectoryPath);
        _interactive = interactive;
    }

    public static MsbuildProject FromFileOrDirectory(ProjectCollection projects, string fileOrDirectory, bool interactive)
    {
        if (File.Exists(fileOrDirectory))
        {
            return FromFile(projects, fileOrDirectory, interactive);
        }
        else
        {
            return FromDirectory(projects, fileOrDirectory, interactive);
        }
    }

    public static MsbuildProject FromFile(ProjectCollection projects, string projectPath, bool interactive)
    {
        if (!File.Exists(projectPath))
        {
            throw new GracefulException(CliStrings.ProjectDoesNotExist, projectPath);
        }

        var project = TryOpenProject(projects, projectPath);
        if (project == null)
        {
            throw new GracefulException(CliStrings.ProjectIsInvalid, projectPath);
        }

        return new MsbuildProject(projects, project, interactive);
    }

    public static MsbuildProject FromDirectory(ProjectCollection projects, string projectDirectory, bool interactive)
    {
        var projectFilePath = GetProjectFileFromDirectory(projectDirectory);

        var project = TryOpenProject(projects, projectFilePath);
        if (project == null)
        {
            throw new GracefulException(CliStrings.FoundInvalidProject, projectFilePath);
        }

        return new MsbuildProject(projects, project, interactive);
    }

    public int AddProjectToProjectReferences(string framework, IEnumerable<string> refs)
    {
        int numberOfAddedReferences = 0;

        ProjectItemGroupElement itemGroup = ProjectRootElement.FindUniformOrCreateItemGroupWithCondition(
            ProjectItemElementType,
            framework);
        foreach (var @ref in refs.Select((r) => PathUtility.GetPathWithBackSlashes(r)))
        {
            if (ProjectRootElement.HasExistingItemWithCondition(framework, @ref))
            {
                Reporter.Output.WriteLine(string.Format(
                    CliStrings.ProjectAlreadyHasAreference,
                    @ref));
                continue;
            }

            numberOfAddedReferences++;
            itemGroup.AppendChild(ProjectRootElement.CreateItemElement(ProjectItemElementType, @ref));

            Reporter.Output.WriteLine(string.Format(CliStrings.ReferenceAddedToTheProject, @ref));
        }

        return numberOfAddedReferences;
    }

    public int RemoveProjectToProjectReferences(string framework, IEnumerable<string> refs)
    {
        int totalNumberOfRemovedReferences = 0;

        foreach (var @ref in refs)
        {
            totalNumberOfRemovedReferences += RemoveProjectToProjectReferenceAlternatives(framework, @ref);
        }

        return totalNumberOfRemovedReferences;
    }

    public IEnumerable<ProjectItemElement> GetProjectToProjectReferences()
    {
        return ProjectRootElement.GetAllItemsWithElementType(ProjectItemElementType);
    }

    public IEnumerable<string> GetRuntimeIdentifiers()
    {
        return cachedRuntimeIdentifiers ??= GetEvaluatedProject().GetRuntimeIdentifiers();
    }

    public IEnumerable<NuGetFramework> GetTargetFrameworks()
    {
        if (_cachedTfms != null)
        {
            return _cachedTfms;
        }

        var project = GetEvaluatedProject();
        _cachedTfms = [.. project.GetTargetFrameworks()];
        return _cachedTfms;
    }
    public IEnumerable<string> GetConfigurations()
    {
        return cachedConfigurations ??= GetEvaluatedProject().GetPropertyCommaSeparatedValues("Configurations");
    }

    public bool CanWorkOnFramework(NuGetFramework framework)
    {
        foreach (var tfm in GetTargetFrameworks())
        {
            if (DefaultCompatibilityProvider.Instance.IsCompatible(framework, tfm))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsTargetingFramework(NuGetFramework framework)
    {
        foreach (var tfm in GetTargetFrameworks())
        {
            if (framework.Equals(tfm))
            {
                return true;
            }
        }

        return false;
    }
    private ProjectInstance GetEvaluatedProject()
    {
        // Reuse a single evaluation across the RID/TFM/Configuration getters. A ProjectInstance is a
        // standalone snapshot that is never stored in the ProjectCollection's loaded-project cache, so
        // (unlike a cached Project) it can never be silently re-evaluated to Full by a later LoadProject.
        if (_cachedEvaluatedProject is not null)
        {
            return _cachedEvaluatedProject;
        }

        try
        {
            // Only evaluated properties (RuntimeIdentifier(s), TargetFramework(s), Configurations) are read
            // from the returned instance, never items or targets, so stop after the Properties pass instead
            // of running a full evaluation.
            var options = new ProjectOptions
            {
                ProjectCollection = _projects,
                EvaluationStage = ProjectEvaluationStage.Properties,
            };
            if (_interactive)
            {
                // NuGet need this environment variable to call plugin dll
                Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", new Muxer().MuxerPath);
                // Even during evaluation time, the SDK resolver may need to output auth instructions, so set a logger.
                _projects.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Minimal));
                options.GlobalProperties = new Dictionary<string, string>
                { ["NuGetInteractive"] = "true" };
            }

            return _cachedEvaluatedProject = ProjectInstance.FromFile(ProjectRootElement.FullPath, options);
        }
        catch (InvalidProjectFileException e)
        {
            throw new GracefulException(string.Format(
                CliStrings.ProjectCouldNotBeEvaluated,
                ProjectRootElement.FullPath, e.Message));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", null);
        }
    }

    private int RemoveProjectToProjectReferenceAlternatives(string framework, string reference)
    {
        int numberOfRemovedRefs = 0;
        foreach (var r in GetIncludeAlternativesForRemoval(reference))
        {
            foreach (var existingItem in ProjectRootElement.FindExistingItemsWithCondition(framework, r))
            {
                ProjectElementContainer itemGroup = existingItem.Parent;
                itemGroup.RemoveChild(existingItem);
                if (itemGroup.Children.Count == 0)
                {
                    itemGroup.Parent.RemoveChild(itemGroup);
                }

                numberOfRemovedRefs++;
                Reporter.Output.WriteLine(string.Format(CliStrings.ProjectReferenceRemoved, r));
            }
        }

        if (numberOfRemovedRefs == 0)
        {
            Reporter.Output.WriteLine(string.Format(
                CliStrings.ProjectReferenceCouldNotBeFound,
                reference));
        }

        return numberOfRemovedRefs;
    }

    // Easiest way to explain rationale for this function is on the example. Let's consider following directory structure:
    // .../a/b/p.proj <project>
    // .../a/d/ref.proj <reference>
    // .../a/e/f/ <current working directory>
    // Project = /some/path/a/b/p.proj
    //
    // We do not know the format of passed reference so
    // path references to consider for removal are following:
    // - full path to ref.proj [/some/path/a/d/ref.proj]
    // - string which is passed as reference is relative to project [../d/ref.proj]
    // - string which is passed as reference is relative to current dir [../../d/ref.proj]
    private IEnumerable<string> GetIncludeAlternativesForRemoval(string reference)
    {
        // We do not care about duplicates in case when i.e. reference is already full path
        List<string> ret = [reference];

        string fullPath = Path.GetFullPath(reference);
        ret.Add(fullPath);
        ret.Add(Path.GetRelativePath(ProjectDirectory, fullPath));

        return ret;
    }

    // There is ProjectRootElement.TryOpen but it does not work as expected
    // I.e. it returns null for some valid projects
    private static ProjectRootElement TryOpenProject(ProjectCollection projects, string filename)
    {
        try
        {
            return ProjectRootElement.Open(filename, projects, preserveFormatting: true);
        }
        catch (InvalidProjectFileException)
        {
            return null;
        }
    }
}
