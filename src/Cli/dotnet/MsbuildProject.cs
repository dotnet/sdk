// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.DotNet.Cli.MSBuildEvaluation;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.ProjectTools;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli;

internal class MsbuildProject
{
    const string ProjectItemElementType = "ProjectReference";

    private DotNetProject _project;
    private readonly DotNetProjectEvaluator _evaluator;
    private readonly bool _interactive = false;

    private MsbuildProject(DotNetProjectEvaluator evaluator, DotNetProject project, bool interactive)
    {
        _evaluator = evaluator;
        _project = project;
        _interactive = interactive;
    }
    public string ProjectDirectory => PathUtility.EnsureTrailingSlash(_project.Directory!);
    public string FullPath => _project.FullPath!;

    public static MsbuildProject FromFileOrDirectory(DotNetProjectEvaluator evaluator, string fileOrDirectory, bool interactive)
    {
        if (File.Exists(fileOrDirectory))
        {
            return FromFile(evaluator, fileOrDirectory, interactive);
        }
        else
        {
            return FromDirectory(evaluator, fileOrDirectory, interactive);
        }
    }

    public static MsbuildProject FromFile(DotNetProjectEvaluator evaluator, string projectPath, bool interactive)
    {
        if (!File.Exists(projectPath))
        {
            throw new GracefulException(CliStrings.ProjectDoesNotExist, projectPath);
        }

        var project = TryOpenProject(evaluator, projectPath, interactive);
        if (project == null)
        {
            throw new GracefulException(CliStrings.ProjectIsInvalid, projectPath);
        }

        return new MsbuildProject(evaluator, project, interactive);
    }

    public static MsbuildProject FromDirectory(DotNetProjectEvaluator evaluator, string projectDirectory, bool interactive)
    {
        var projectFilePath = GetProjectFileFromDirectory(projectDirectory);

        var project = TryOpenProject(evaluator, projectFilePath, interactive);
        if (project == null)
        {
            throw new GracefulException(CliStrings.FoundInvalidProject, projectFilePath);
        }

        return new MsbuildProject(evaluator, project, interactive);
    }

    public static string GetProjectFileFromDirectory(string projectDirectory)
        => ProjectLocator.TryGetProjectFileFromDirectory(projectDirectory, out var projectFilePath, out var error)
            ? projectFilePath
            : throw new GracefulException(error);

    public static bool TryGetProjectFileFromDirectory(string projectDirectory, [NotNullWhen(true)] out string? projectFilePath)
        => ProjectLocator.TryGetProjectFileFromDirectory(projectDirectory, out projectFilePath, out _);

    /// <summary>
    /// Adds project-to-project references to the project.
    /// </summary>
    /// <param name="framework">If set, the reference will be conditional on this framework.</param>
    /// <param name="refs">The projects to add references to.</param>
    /// <returns></returns>
    public int AddProjectToProjectReferences(string? framework, IEnumerable<string> refs)
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

    public IEnumerable<DotNetProjectItem> ProjectReferences() => _project.ProjectReferences;

    public IEnumerable<string> GetRuntimeIdentifiers() => _project.RuntimeIdentifiers ?? [];

    public IEnumerable<NuGetFramework> GetTargetFrameworks() => _project.TargetFrameworks ?? [];

    public IEnumerable<string> GetConfigurations() => _project.Configurations ?? [];

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
    private static DotNetProject TryOpenProject(DotNetProjectEvaluator evaluator, string filename, bool interactive)
    {
        try
        {
            DotNetProject project;
            if (interactive)
            {
                // NuGet need this environment variable to call plugin dll
                Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", new Muxer().MuxerPath);
                // Even during evaluation time, the SDK resolver may need to output auth instructions, so set a logger.
                project = evaluator.LoadProject(filename, new Dictionary<string, string>(){ ["NuGetInteractive"] = "true" });
            }
            else
            {
                project = evaluator.LoadProject(filename);
            }

            return project;
        }
        catch (InvalidProjectFileException e)
        {
            throw new GracefulException(string.Format(
                CliStrings.ProjectCouldNotBeEvaluated,
                filename, e.Message));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", null);
        }
    }
}
