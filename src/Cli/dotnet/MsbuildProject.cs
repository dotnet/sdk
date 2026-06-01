// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Reference;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli;

internal class MsbuildProject
{
    const string ProjectItemElementType = "ProjectReference";

    public ProjectRootElement ProjectRootElement { get; private set; }
    public string ProjectDirectory { get; private set; }

    private readonly ProjectCollection _projects;
    private List<NuGetFramework> _cachedTfms = null;
    private IEnumerable<string> cachedRuntimeIdentifiers;
    private IEnumerable<string> cachedConfigurations;
    private readonly bool _interactive = false;
    private readonly string _entryPointFilePath;

    private MsbuildProject(ProjectCollection projects, ProjectRootElement project, bool interactive, string entryPointFilePath = null)
    {
        _projects = projects;
        ProjectRootElement = project;
        ProjectDirectory = PathUtilities.EnsureTrailingSlash(ProjectRootElement.DirectoryPath);
        _interactive = interactive;
        _entryPointFilePath = entryPointFilePath;
    }

    public bool IsFileBasedApp => _entryPointFilePath is not null;

    public static MsbuildProject FromFileOrDirectory(ProjectCollection projects, string fileOrDirectory, bool interactive)
        => FromFileOrDirectory(projects, fileOrDirectory, interactive, AppKinds.ProjectBased);

    public static MsbuildProject FromFileOrDirectory(ProjectCollection projects, string fileOrDirectory, bool interactive, AppKinds allowedAppKinds)
    {
        if (allowedAppKinds.HasFlag(AppKinds.FileBased) && VirtualProjectBuilder.IsValidEntryPointPath(fileOrDirectory))
        {
            return FromFileBasedApp(projects, fileOrDirectory, interactive);
        }

        if (!allowedAppKinds.HasFlag(AppKinds.ProjectBased))
        {
            throw new GracefulException(CliCommandStrings.InvalidFilePath, fileOrDirectory);
        }

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

    private static MsbuildProject FromFileBasedApp(ProjectCollection projects, string entryPointFilePath, bool interactive)
    {
        string entryPointFullPath = Path.GetFullPath(entryPointFilePath);
        var builder = new VirtualProjectBuilder(entryPointFullPath, VirtualProjectBuildingCommand.TargetFramework);

        builder.CreateProjectInstance(
            projects,
            ErrorReporters.IgnoringReporter,
            project: out _,
            out var projectRootElement,
            evaluatedDirectives: out _);

        return new MsbuildProject(projects, projectRootElement, interactive, entryPointFullPath);
    }

    public void Save()
    {
        if (IsFileBasedApp)
        {
            VirtualProjectReferenceReflector.ReflectChangesToDirectives(ProjectRootElement, _entryPointFilePath);
        }
        else
        {
            ProjectRootElement.Save();
        }
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

    public static string GetProjectFileFromDirectory(string projectDirectory)
        => ProjectLocator.TryGetProjectFileFromDirectory(projectDirectory, out var projectFilePath, out var error)
            ? projectFilePath
            : throw new GracefulException(error);

    public static bool TryGetProjectFileFromDirectory(string projectDirectory, [NotNullWhen(true)] out string projectFilePath)
        => ProjectLocator.TryGetProjectFileFromDirectory(projectDirectory, out projectFilePath, out _);

    public int AddProjectToProjectReferences(string framework, IEnumerable<string> refs)
        => AddProjectToProjectReferences(framework, refs.Select(static r => (Include: r, DirectiveInclude: (string)null)));

    public int AddProjectToProjectReferences(string framework, IEnumerable<(string Include, string DirectiveInclude)> refs)
    {
        int numberOfAddedReferences = 0;

        ProjectItemGroupElement itemGroup = ProjectRootElement.FindUniformOrCreateItemGroupWithCondition(
            ProjectItemElementType,
            framework);
        foreach (var reference in refs)
        {
            var @ref = PathUtility.GetPathWithBackSlashes(reference.Include);
            if (ProjectRootElement.HasExistingItemWithCondition(framework, @ref))
            {
                Reporter.Output.WriteLine(string.Format(
                    CliStrings.ProjectAlreadyHasAreference,
                    @ref));
                continue;
            }

            numberOfAddedReferences++;

            // For file-based apps, we keep the original text so it can be reflected back to the directive.
            // For example, `#:project Lib` will be added as `<ProjectReference Include="Lib" />` instead of `<ProjectReference Include="Lib\Lib.csproj" />`.
            // This is fine because ProjectRootElement is not evaluated, it is just a transfer mechanism.
            var itemInclude = IsFileBasedApp && reference.DirectiveInclude is not null
                ? reference.DirectiveInclude
                : @ref;
            var item = ProjectRootElement.CreateItemElement(ProjectItemElementType, itemInclude);
            itemGroup.AppendChild(item);

            Reporter.Output.WriteLine(string.Format(CliStrings.ReferenceAddedToTheProject, @ref));
        }

        return numberOfAddedReferences;
    }

    public int AddFileBasedAppReferences(IEnumerable<(string Include, string DirectiveInclude)> refs)
    {
        int numberOfAddedReferences = 0;

        ProjectItemGroupElement itemGroup = ProjectRootElement.FindUniformOrCreateItemGroupWithCondition(
            ProjectItemElementType,
            framework: null);
        foreach (var reference in refs)
        {
            string displayReference = PathUtility.GetPathWithBackSlashes(reference.DirectiveInclude);
            if (ProjectRootElement.HasExistingItemWithCondition(framework: null, reference.Include))
            {
                Reporter.Output.WriteLine(string.Format(
                    CliStrings.ProjectAlreadyHasAreference,
                    displayReference));
                continue;
            }

            numberOfAddedReferences++;
            var item = ProjectRootElement.CreateItemElement(ProjectItemElementType, reference.Include);
            itemGroup.AppendChild(item);
            item.AddMetadata(VirtualProjectBuilder.RefDirectiveIncludeMetadataName, reference.DirectiveInclude);

            Reporter.Output.WriteLine(string.Format(CliStrings.ReferenceAddedToTheProject, displayReference));
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

    public int RemoveFileBasedAppReferences(IEnumerable<(string Include, string DisplayInclude)> refs)
    {
        int totalNumberOfRemovedReferences = 0;

        foreach (var reference in refs)
        {
            totalNumberOfRemovedReferences += RemoveFileBasedAppReference(reference.Include, reference.DisplayInclude);
        }

        return totalNumberOfRemovedReferences;
    }

    public bool HasFileBasedAppReference(string reference)
    {
        if (!IsFileBasedApp)
        {
            return false;
        }

        string virtualProjectPath = VirtualProjectBuilder.GetVirtualProjectPath(Path.GetFullPath(reference));
        return ProjectRootElement.HasExistingItemWithCondition(framework: null, virtualProjectPath);
    }

    public IEnumerable<ProjectItemElement> GetProjectToProjectReferences()
    {
        var projectReferences = ProjectRootElement.GetAllItemsWithElementType(ProjectItemElementType);

        if (!IsFileBasedApp)
        {
            return projectReferences;
        }

        var projectInstance = new ProjectInstance(ProjectRootElement);
        return projectReferences.Where(p => !VirtualProjectReferenceReflector.IsFileBasedAppReference(p, ProjectRootElement, projectInstance));
    }

    public IEnumerable<string> GetReferencesForDisplay()
    {
        var projectReferences = ProjectRootElement.GetAllItemsWithElementType(ProjectItemElementType);
        var projectInstance = new ProjectInstance(ProjectRootElement);
        var refDirectiveDisplayIncludes = IsFileBasedApp
            ? VirtualProjectReferenceReflector.GetFileBasedAppReferenceDisplayIncludes(ProjectRootElement, _entryPointFilePath)
            : new Dictionary<string, string>();

        foreach (var item in projectReferences)
        {
            foreach (var include in item.Includes())
            {
                if (IsFileBasedApp)
                {
                    var normalizedInclude = VirtualProjectReferenceReflector.NormalizeProjectReferencePath(Path.GetFullPath(include));
                    if (refDirectiveDisplayIncludes.TryGetValue(normalizedInclude, out var displayInclude))
                    {
                        yield return displayInclude;
                        continue;
                    }
                }

                yield return projectInstance.ExpandString(include);
            }
        }
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
        return cachedConfigurations ??= GetEvaluatedProject().GetConfigurations();
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

    private Project GetEvaluatedProject()
    {
        try
        {
            Project project;
            if (_interactive)
            {
                // NuGet need this environment variable to call plugin dll
                Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", new Muxer().MuxerPath);
                // Even during evaluation time, the SDK resolver may need to output auth instructions, so set a logger.
                _projects.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Minimal));
                project = _projects.LoadProject(
                    ProjectRootElement.FullPath,
                    new Dictionary<string, string>
                    { ["NuGetInteractive"] = "true" },
                    null);
            }
            else
            {
                project = _projects.LoadProject(ProjectRootElement.FullPath);
            }

            return project;
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

    private int RemoveFileBasedAppReference(string reference, string displayReference)
    {
        int numberOfRemovedRefs = 0;
        string displayReferenceWithBackSlashes = PathUtility.GetPathWithBackSlashes(displayReference);

        foreach (var existingItem in ProjectRootElement.FindExistingItemsWithCondition(framework: null, reference).ToList())
        {
            ProjectElementContainer itemGroup = existingItem.Parent;
            itemGroup.RemoveChild(existingItem);
            if (itemGroup.Children.Count == 0)
            {
                itemGroup.Parent.RemoveChild(itemGroup);
            }

            numberOfRemovedRefs++;
            Reporter.Output.WriteLine(string.Format(CliStrings.ProjectReferenceRemoved, displayReferenceWithBackSlashes));
        }

        if (numberOfRemovedRefs == 0)
        {
            Reporter.Output.WriteLine(string.Format(
                CliStrings.ProjectReferenceCouldNotBeFound,
                displayReferenceWithBackSlashes));
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
