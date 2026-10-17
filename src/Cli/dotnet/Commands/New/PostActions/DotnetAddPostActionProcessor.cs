// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Utils;
using MSBuildProject = Microsoft.Build.Evaluation.Project;

namespace Microsoft.DotNet.Cli.Commands.New.PostActions;

internal class DotnetAddPostActionProcessor(
    Func<string, string, string?, bool, bool>? addPackageReferenceCallback = null,
    Func<string, string, bool>? addProjectReferenceCallback = null) : PostActionProcessorBase
{
    private readonly Func<string, string, string?, bool, bool> _addPackageReferenceCallback = addPackageReferenceCallback ?? DotnetCommandCallbacks.AddPackageReference;
    private readonly Func<string, string, bool> _addProjectReferenceCallback = addProjectReferenceCallback ?? DotnetCommandCallbacks.AddProjectReference;

    public override Guid Id => ActionProcessorId;

    internal static Guid ActionProcessorId { get; } = new("B17581D1-C5C9-4489-8F0A-004BE667B814");

    internal static IReadOnlyList<string> FindProjFileAtOrAbovePath(IPhysicalFileSystem fileSystem, string startPath, HashSet<string> extensionLimiters)
    {
        if (extensionLimiters.Count == 0)
        {
            return FileFindHelpers.FindFilesAtOrAbovePath(fileSystem, startPath, "*.*proj");
        }
        else
        {
            return FileFindHelpers.FindFilesAtOrAbovePath(fileSystem, startPath, "*.*proj", (filename) => extensionLimiters.Contains(Path.GetExtension(filename)));
        }
    }

    protected override bool ProcessInternal(IEngineEnvironmentSettings environment, IPostAction action, ICreationEffects creationEffects, ICreationResult templateCreationResult, string outputBasePath)
    {
        IReadOnlyList<string>? projectsToProcess = GetConfiguredFiles(action.Args, creationEffects, "targetFiles", outputBasePath);

        if (!projectsToProcess.Any())
        {
            //If the author didn't opt in to the new behavior by specifying "targetFiles", search for project file in current output directory or above.
            HashSet<string> extensionLimiters = new(StringComparer.Ordinal);
            if (action.Args.TryGetValue("projectFileExtensions", out string? projectFileExtensions))
            {
                if (projectFileExtensions.Contains('/') || projectFileExtensions.Contains('\\') || projectFileExtensions.Contains('*'))
                {
                    // these must be literals
                    Reporter.Error.WriteLine(CliCommandStrings.PostAction_AddReference_Error_ActionMisconfigured);
                    return false;
                }

                extensionLimiters.UnionWith(projectFileExtensions.Split([';'], StringSplitOptions.RemoveEmptyEntries));
            }
            projectsToProcess = TryFindProjects(environment, outputBasePath, extensionLimiters);
            if (projectsToProcess.Count > 1)
            {
                // multiple projects at the same level. Error.
                Reporter.Error.WriteLine(CliCommandStrings.PostAction_AddReference_Error_UnresolvedProjFile);
                Reporter.Error.WriteLine(CliCommandStrings.PostAction_AddReference_Error_ProjFileListHeader);
                foreach (string projectFile in projectsToProcess)
                {
                    Reporter.Error.WriteLine(string.Format("\t{0}", projectFile));
                }
                return false;
            }
        }

        if (!projectsToProcess.Any())
        {
            projectsToProcess = FindExistingTargetFiles(environment.Host.FileSystem, action.Args, outputBasePath);
        }

        if (!projectsToProcess.Any())
        {
            // no projects found. Error.
            Reporter.Error.WriteLine(CliCommandStrings.PostAction_AddReference_Error_UnresolvedProjFile);
            return false;
        }

        bool success = true;
        foreach (string projectFile in projectsToProcess)
        {
            success &= AddReference(action, projectFile, outputBasePath, creationEffects);

            if (!success)
            {
                return false;
            }
        }
        return true;
    }

    private static IReadOnlyList<string> TryFindProjects(IEngineEnvironmentSettings environment, string outputBasePath, HashSet<string> extensionLimiters)
    {
        try
        {
            return FindProjFileAtOrAbovePath(environment.Host.FileSystem, outputBasePath, extensionLimiters);
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> FindExistingTargetFiles(IPhysicalFileSystem fileSystem, IReadOnlyDictionary<string, string> actionArgs, string outputBasePath)
    {
        var targetFiles = GetTargetFilesPaths(actionArgs, outputBasePath);
        var foundFiles = targetFiles?
            .Where(fileSystem.FileExists)
            .ToList();
        return foundFiles ?? [];
    }

    private bool AddReference(IPostAction actionConfig, string projectFile, string outputBasePath, ICreationEffects creationEffects)
    {
        if (actionConfig.Args == null || !actionConfig.Args.TryGetValue("reference", out string? referenceToAdd))
        {
            Reporter.Error.WriteLine(CliCommandStrings.PostAction_AddReference_Error_ActionMisconfigured);
            return false;
        }

        if (!actionConfig.Args.TryGetValue("referenceType", out string? referenceType))
        {
            Reporter.Error.WriteLine(CliCommandStrings.PostAction_AddReference_Error_ActionMisconfigured);
            return false;
        }

        if (string.Equals(referenceType, "project", StringComparison.OrdinalIgnoreCase))
        {
            // replace the referenced project file's name in case it has been renamed
            string? referenceNameChange = GetTargetForSource((ICreationEffects2)creationEffects, referenceToAdd, outputBasePath).SingleOrDefault();
            string relativeProjectReference = referenceNameChange ?? referenceToAdd;

            referenceToAdd = Path.GetFullPath(relativeProjectReference, outputBasePath);
            return AddProjectReference(projectFile, referenceToAdd);
        }
        else if (string.Equals(referenceType, "package", StringComparison.OrdinalIgnoreCase))
        {
            actionConfig.Args.TryGetValue("version", out string? version);

            // A repository using central package management already owns the version of this
            // package. Passing the template's version would rewrite the existing PackageVersion
            // entry and change it for every project in that repository, so the template version
            // is only supplied when the evaluated project has no active entry for the package yet.
            if (!string.IsNullOrWhiteSpace(version) && HasCentralPackageVersion(projectFile, referenceToAdd))
            {
                version = null;
            }

            // Templates that schedule their own restore post-action opt out of the per-package
            // restore. Left unset, the add keeps restoring, so package existence and
            // compatibility are still validated for every other template.
            bool noRestore = actionConfig.Args.TryGetValue("noRestore", out string? noRestoreValue)
                && bool.TryParse(noRestoreValue, out bool parsedNoRestore)
                && parsedNoRestore;

            return AddPackageReference(projectFile, referenceToAdd, version, noRestore);
        }
        else if (string.Equals(referenceType, "framework", StringComparison.OrdinalIgnoreCase))
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.PostAction_AddReference_Error_FrameworkNotSupported, referenceToAdd));
            return false;
        }
        else
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.PostAction_AddReference_Error_UnsupportedRefType, referenceType));
            return false;
        }
    }

    internal static bool HasCentralPackageVersion(string projectFile, string packageName)
    {
        // MSBuild evaluates from the real disk, so check there rather than through the host file system.
        if (!File.Exists(projectFile))
        {
            return false;
        }

        try
        {
            // Read the evaluated project rather than Directory.Packages.props itself. NuGet's restore
            // collects central versions from an evaluation with TargetFramework set, and
            // Directory.Packages.props is imported before the project sets it, so Choose/When blocks
            // and imports conditioned on the framework only resolve in that per-framework evaluation.
            using var projectCollection = new ProjectCollection();
            MSBuildProject outerProject = projectCollection.LoadProject(projectFile);

            string targetFramework = outerProject.GetPropertyValue("TargetFramework");
            string[] targetFrameworks = string.IsNullOrEmpty(targetFramework)
                ? outerProject.GetPropertyValue("TargetFrameworks").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [targetFramework];
            if (targetFrameworks.Length == 0)
            {
                return DeclaresCentralPackageVersion(outerProject, packageName);
            }

            // The template version is only dropped when every framework already has an entry.
            return targetFrameworks.All(tfm => DeclaresCentralPackageVersion(
                projectCollection.LoadProject(projectFile, new Dictionary<string, string> { ["TargetFramework"] = tfm }, toolsVersion: null),
                packageName));
        }
        catch (Exception e) when (e is InvalidProjectFileException or IOException or UnauthorizedAccessException)
        {
            // A project that does not evaluate keeps the previous behaviour: the template version is passed.
            return false;
        }
    }

    private static bool DeclaresCentralPackageVersion(MSBuildProject project, string packageName)
        => string.Equals(project.GetPropertyValue("ManagePackageVersionsCentrally"), "true", StringComparison.OrdinalIgnoreCase)
            && project.GetItems("PackageVersion").Any(item => string.Equals(item.EvaluatedInclude, packageName, StringComparison.OrdinalIgnoreCase));

    private bool AddPackageReference(string projectPath, string packageName, string? version, bool noRestore)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                Reporter.Output.WriteLine(string.Format(CliCommandStrings.PostAction_AddReference_AddPackageReference, packageName, projectPath));
            }
            else
            {
                Reporter.Output.WriteLine(string.Format(CliCommandStrings.PostAction_AddReference_AddPackageReference_WithVersion, packageName, version, projectPath));
            }
            bool succeeded = _addPackageReferenceCallback(projectPath, packageName, version, noRestore);
            if (succeeded)
            {
                Reporter.Output.WriteLine(CliCommandStrings.PostAction_AddReference_Succeeded);
            }
            else
            {
                Reporter.Error.WriteLine(CliCommandStrings.PostAction_AddReference_Failed);
            }
            return succeeded;
        }
        catch (Exception e)
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.PostAction_AddReference_AddPackageReference_Failed, e.Message));
            return false;
        }
    }

    private bool AddProjectReference(string projectPath, string projectToAdd)
    {
        try
        {
            Reporter.Output.WriteLine(string.Format(CliCommandStrings.PostAction_AddReference_AddProjectReference, projectToAdd, projectPath));
            bool succeeded = _addProjectReferenceCallback(projectPath, projectToAdd);
            if (succeeded)
            {
                Reporter.Output.WriteLine(CliCommandStrings.PostAction_AddReference_Succeeded);
            }
            else
            {
                Reporter.Error.WriteLine(CliCommandStrings.PostAction_AddReference_Failed);
            }
            return succeeded;
        }
        catch (Exception e)
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.PostAction_AddReference_AddProjectReference_Failed, e.Message));
            return false;
        }
    }
}
