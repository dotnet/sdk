// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Common;

internal static class SlnFileExtensions
{
    public static void AddProject(this SlnFile slnFile, string fullProjectPath, IList<string> solutionFolders)
    {
        ArgumentException.ThrowIfNullOrEmpty(fullProjectPath);

        var relativeProjectPath = Path.GetRelativePath(
            PathUtility.EnsureTrailingSlash(slnFile.BaseDirectory),
            fullProjectPath);

        if (slnFile.Projects.Any((p) =>
                string.Equals(p.FilePath, relativeProjectPath, StringComparison.OrdinalIgnoreCase)))
        {
            Reporter.Output.WriteLine(string.Format(
                CommonLocalizableStrings.SolutionAlreadyContainsProject,
                slnFile.FullPath,
                relativeProjectPath));
        }
        else
        {
            ProjectRootElement rootElement = null;
            ProjectInstance projectInstance = null;
            try
            {
                rootElement = ProjectRootElement.Open(fullProjectPath);
                projectInstance = new ProjectInstance(rootElement);
            }
            catch (InvalidProjectFileException e)
            {
                Reporter.Error.WriteLine(string.Format(
                    CommonLocalizableStrings.InvalidProjectWithExceptionMessage,
                    fullProjectPath,
                    e.Message));
                return;
            }

            var slnProject = new SlnProject
            {
                Id = projectInstance.GetProjectId(),
                TypeGuid = rootElement.GetProjectTypeGuid() ?? projectInstance.GetDefaultProjectTypeGuid(),
                Name = Path.GetFileNameWithoutExtension(relativeProjectPath),
                FilePath = relativeProjectPath
            };

            if (string.IsNullOrEmpty(slnProject.TypeGuid))
            {
                Reporter.Error.WriteLine(
                    string.Format(
                        CommonLocalizableStrings.UnsupportedProjectType,
                        projectInstance.FullPath));
                return;
            }

            // NOTE: The order you create the sections determines the order they are written to the sln
            // file. In the case of an empty sln file, in order to make sure the solution configurations
            // section comes first we need to add it first. This doesn't affect correctness but does
            // stop VS from re-ordering things later on. Since we are keeping the SlnFile class low-level
            // it shouldn't care about the VS implementation details. That's why we handle this here.
            if (AreBuildConfigurationsApplicable(slnProject.TypeGuid))
            {
                slnFile.AddDefaultBuildConfigurations();

                slnFile.MapSolutionConfigurationsToProject(
                    projectInstance,
                    slnFile.ProjectConfigurationsSection.GetOrCreatePropertySet(slnProject.Id));
            }

            SetupSolutionFolders(slnFile, solutionFolders, relativeProjectPath, slnProject);

            slnFile.Projects.Add(slnProject);

            Reporter.Output.WriteLine(
                string.Format(CommonLocalizableStrings.ProjectAddedToTheSolution, relativeProjectPath));
        }
    }

    public static void AddSolutionFolder(this SlnFile slnFile, string fullFolderPath, IList<string> solutionFolders)
    {
        ArgumentException.ThrowIfNullOrEmpty(fullFolderPath);

        var relativeProjectPath = Path.GetRelativePath(
            PathUtility.EnsureTrailingSlash(slnFile.BaseDirectory),
            fullFolderPath);

        if (slnFile.Projects.Any((p) =>
                string.Equals(p.FilePath, relativeProjectPath, StringComparison.OrdinalIgnoreCase)))
        {
            Reporter.Output.WriteLine(string.Format(
                Tools.Sln.LocalizableStrings.SolutionAlreadyContainsFolder,
                slnFile.FullPath,
                relativeProjectPath));
        }
        else
        {
            var slnProject = new SlnProject
            {
                Id = Guid.NewGuid().ToString("B").ToUpper(),
                TypeGuid = ProjectTypeGuids.SolutionFolderGuid,
                Name = Path.GetFileNameWithoutExtension(relativeProjectPath),
                FilePath = relativeProjectPath
            };

            SetupSolutionFolders(slnFile, solutionFolders, relativeProjectPath, slnProject);

            slnFile.Projects.Add(slnProject);

            Reporter.Output.WriteLine(
                string.Format(Tools.Sln.LocalizableStrings.SolutionFolderAddedToTheSolution, relativeProjectPath));
        }
    }

    public static bool SolutionFolderContainsSolutionItem(this SlnFile slnFile, string solutionFolder, string relativeFilePath) =>
        slnFile.Projects
            .Any(p => p.TypeGuid == ProjectTypeGuids.SolutionFolderGuid
                && StringComparer.OrdinalIgnoreCase.Equals(p.Name, solutionFolder)
                && p.ContainsSolutionItem(Path.GetFileName(relativeFilePath)));

    private static bool AreBuildConfigurationsApplicable(string projectTypeGuid)
    {
        return !projectTypeGuid.Equals(ProjectTypeGuids.SharedProjectGuid, StringComparison.OrdinalIgnoreCase);
    }

    public static void AddSolutionItem(this SlnFile slnFile, string relativeFilePath, IList<string> solutionFolderNames)
    {
        ArgumentException.ThrowIfNullOrEmpty(relativeFilePath);

        if (solutionFolderNames == null)
        {
            var defaultSolutionFolder = slnFile.GetOrCreateSolutionFolder();
            slnFile.AddSolutionItemToSolutionFolder(defaultSolutionFolder.Id, relativeFilePath);
            return;
        }

        var nestedProjectsSection = solutionFolderNames.Count > 1
            ? slnFile.Sections.GetOrCreateSection("NestedProjects", SlnSectionType.PreProcess)
            : slnFile.Sections.GetSection("NestedProjects", SlnSectionType.PreProcess);

        for (int i = 0; i < solutionFolderNames.Count; i++)
        {
            string currentSolutionFolderName = solutionFolderNames[i];
            SlnProject currentSolutionFolder = slnFile.GetOrCreateSolutionFolder(currentSolutionFolderName);

            if (i > 0 && nestedProjectsSection != null)
            {
                string previousSolutionFolderName = solutionFolderNames[i - 1];
                SlnProject previousSolutionFolder = slnFile.GetOrCreateSolutionFolder(previousSolutionFolderName);
                nestedProjectsSection.Properties.TryAdd(currentSolutionFolder.Id, previousSolutionFolder.Id);
            }

            if (i == solutionFolderNames.Count - 1)
            {
                slnFile.AddSolutionItemToSolutionFolder(currentSolutionFolder.Id, relativeFilePath);
            }
        }
    }

    private static void AddSolutionItemToSolutionFolder(
        this SlnFile slnFile,
        string solutionFolderId,
        string relativeFilePath)
    {
        var solutionFolder = slnFile.Projects.GetProject(solutionFolderId);
        var solutionItemsSection = solutionFolder.Sections.GetOrCreateSection("SolutionItems", SlnSectionType.PreProcess);
        var solutionItems = new Dictionary<string, string>(solutionItemsSection.GetContent(), StringComparer.OrdinalIgnoreCase);

        if (solutionItems.TryAdd(relativeFilePath, relativeFilePath))
        {
            solutionItemsSection.SetContent(solutionItems);
            var solutionItemsAfterAddition = solutionFolder.Sections.GetSection("SolutionItems", SlnSectionType.PreProcess);
            var stringifiedDictionary = string.Join(", ", solutionItemsAfterAddition.GetContent().Select(kvp => $"{kvp.Key}: {kvp.Value}"));

            Reporter.Output.WriteLine(string.Format(Tools.Sln.LocalizableStrings.SolutionItemAddedToTheSolution, relativeFilePath, solutionFolder.Name));
        }
        else
        {
            throw new GracefulException(string.Format(
                Tools.Sln.LocalizableStrings.SolutionAlreadyContainsFile,
                slnFile.FullPath,
                relativeFilePath));
        }
    }

    private static SlnProject GetOrCreateSolutionFolder(this SlnFile slnFile, string solutionFolderName = "Solution Items")
    {
        var existingSolutionFolder = slnFile.Projects.SingleOrDefault(
            p => StringComparer.OrdinalIgnoreCase.Equals(p.Name, solutionFolderName));
        if (existingSolutionFolder != null) { return existingSolutionFolder; }

        var solutionFolder = new SlnProject
        {
            Id = Guid.NewGuid().ToString("B").ToUpper(),
            TypeGuid = ProjectTypeGuids.SolutionFolderGuid,
            Name = solutionFolderName,
            FilePath = solutionFolderName,
        };
        slnFile.Projects.Add(solutionFolder);
        return solutionFolder;
    }

    private static void SetupSolutionFolders(SlnFile slnFile, IList<string> solutionFolders, string relativeProjectPath, SlnProject slnProject)
    {
        if (solutionFolders == null)
        {
            return;
        }

        if (solutionFolders.Any())
        {
            // Before adding a solution folder, check if the name conflicts with any existing projects in the solution
            var duplicateProjects = slnFile.Projects.Where(p => solutionFolders.Contains(p.Name)
                                                            && p.TypeGuid != ProjectTypeGuids.SolutionFolderGuid).ToList();
            foreach (SlnProject duplicateProject in duplicateProjects)
            {
                slnFile.AddSolutionFolders(duplicateProject, [Path.GetDirectoryName(duplicateProject.FilePath)]);
            }
        }
        else
        {
            // If a project and solution folder have the same name, add its own folder as a solution folder
            // eg. foo\extensions.csproj and extensions\library\library.csproj would have a project and solution folder with conflicting names
            var duplicateProject = slnFile.Projects.Where(p => string.Equals(p.Name, slnProject.Name, StringComparison.OrdinalIgnoreCase)
                                                           && p.TypeGuid == ProjectTypeGuids.SolutionFolderGuid).FirstOrDefault();
            if (duplicateProject != null)
            {
                // Try making a new folder for the project to put it under so we can still add it despite there being one with the same name already in the parent folder
                slnFile.AddSolutionFolders(slnProject, [Path.GetDirectoryName(relativeProjectPath)]);
            }
        }
        // Even if we added a solution folder above for a duplicate, we still need to add the expected folder for the current project
        slnFile.AddSolutionFolders(slnProject, solutionFolders);
    }

    private static void AddDefaultBuildConfigurations(this SlnFile slnFile)
    {
        var configurationsSection = slnFile.SolutionConfigurationsSection;

        if (!configurationsSection.IsEmpty)
        {
            return;
        }

        var defaultConfigurations = new List<string>()
        {
            "Debug|Any CPU",
            "Debug|x64",
            "Debug|x86",
            "Release|Any CPU",
            "Release|x64",
            "Release|x86",
        };

        foreach (var config in defaultConfigurations)
        {
            configurationsSection[config] = config;
        }
    }

    private static void MapSolutionConfigurationsToProject(
        this SlnFile slnFile,
        ProjectInstance projectInstance,
        SlnPropertySet solutionProjectConfigs)
    {
        var (projectConfigurations, defaultProjectConfiguration) = GetKeysDictionary(projectInstance.GetConfigurations());
        var (projectPlatforms, defaultProjectPlatform) = GetKeysDictionary(projectInstance.GetPlatforms());

        foreach (var solutionConfigKey in slnFile.SolutionConfigurationsSection.Keys)
        {
            var projectConfigKey = MapSolutionConfigKeyToProjectConfigKey(
                solutionConfigKey,
                projectConfigurations,
                defaultProjectConfiguration,
                projectPlatforms,
                defaultProjectPlatform);
            if (projectConfigKey == null)
            {
                continue;
            }

            var activeConfigKey = $"{solutionConfigKey}.ActiveCfg";
            if (!solutionProjectConfigs.ContainsKey(activeConfigKey))
            {
                solutionProjectConfigs[activeConfigKey] = projectConfigKey;
            }

            var buildKey = $"{solutionConfigKey}.Build.0";
            if (!solutionProjectConfigs.ContainsKey(buildKey))
            {
                solutionProjectConfigs[buildKey] = projectConfigKey;
            }
        }
    }

    private static (Dictionary<string, string> Keys, string DefaultKey) GetKeysDictionary(IEnumerable<string> keys)
    {
        // A dictionary mapping key -> key is used instead of a HashSet so the original case of the key can be retrieved from the set
        var dictionary = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);

        foreach (var key in keys)
        {
            dictionary[key] = key;
        }

        return (dictionary, keys.FirstOrDefault());
    }

    private static string GetMatchingProjectKey(Dictionary<string, string> projectKeys, string solutionKey)
    {
        if (projectKeys.TryGetValue(solutionKey, out string projectKey))
        {
            return projectKey;
        }

        var keyWithoutWhitespace = string.Concat(solutionKey.Where(c => !char.IsWhiteSpace(c)));
        if (projectKeys.TryGetValue(keyWithoutWhitespace, out projectKey))
        {
            return projectKey;
        }

        return null;
    }

    private static string MapSolutionConfigKeyToProjectConfigKey(
        string solutionConfigKey,
        Dictionary<string, string> projectConfigurations,
        string defaultProjectConfiguration,
        Dictionary<string, string> projectPlatforms,
        string defaultProjectPlatform)
    {
        var pair = solutionConfigKey.Split(['|'], 2);
        if (pair.Length != 2)
        {
            return null;
        }

        var projectConfiguration = GetMatchingProjectKey(projectConfigurations, pair[0]) ?? defaultProjectConfiguration;
        if (projectConfiguration == null)
        {
            return null;
        }

        var projectPlatform = GetMatchingProjectKey(projectPlatforms, pair[1]) ?? defaultProjectPlatform;
        if (projectPlatform == null)
        {
            return null;
        }

        // VS stores "Any CPU" platform in the solution regardless of how it is named at the project level
        return $"{projectConfiguration}|{(projectPlatform == "AnyCPU" ? "Any CPU" : projectPlatform)}";
    }

    private static void AddSolutionFolders(this SlnFile slnFile, SlnProject slnProject, IList<string> solutionFolders)
    {
        if (solutionFolders.Any())
        {
            var nestedProjectsSection = slnFile.Sections.GetOrCreateSection(
                "NestedProjects",
                SlnSectionType.PreProcess);

            var pathToGuidMap = slnFile.GetSolutionFolderPaths(nestedProjectsSection.Properties);

            if (nestedProjectsSection.Properties.ContainsKey(slnProject.Id))
            {
                return;
            }

            string parentDirGuid = null;
            var solutionFolderHierarchy = string.Empty;
            foreach (var dir in solutionFolders)
            {
                solutionFolderHierarchy = Path.Combine(solutionFolderHierarchy, dir);
                if (pathToGuidMap.TryGetValue(solutionFolderHierarchy, out string? guid))
                {
                    parentDirGuid = guid;
                }
                else
                {

                    if(HasDuplicateNameForSameValueOfNestedProjects(nestedProjectsSection, dir, parentDirGuid, slnFile.Projects))
                    {
                        throw new GracefulException(CommonLocalizableStrings.SolutionFolderAlreadyContainsProject, slnFile.FullPath, slnProject.Name, slnFile.Projects.FirstOrDefault(p => p.Id == parentDirGuid).Name);
                    }

                    var solutionFolder = new SlnProject
                    {
                        Id = Guid.NewGuid().ToString("B").ToUpper(),
                        TypeGuid = ProjectTypeGuids.SolutionFolderGuid,
                        Name = dir,
                        FilePath = dir
                    };

                    slnFile.Projects.Add(solutionFolder);

                    if (parentDirGuid != null)
                    {
                        nestedProjectsSection.Properties[solutionFolder.Id] = parentDirGuid;
                    }
                    parentDirGuid = solutionFolder.Id;
                }
            }
            if (HasDuplicateNameForSameValueOfNestedProjects(nestedProjectsSection, slnProject.Name, parentDirGuid, slnFile.Projects))
            {
                throw new GracefulException(CommonLocalizableStrings.SolutionFolderAlreadyContainsProject, slnFile.FullPath, slnProject.Name, slnFile.Projects.FirstOrDefault(p => p.Id == parentDirGuid).Name);
            }
            nestedProjectsSection.Properties[slnProject.Id] = parentDirGuid;
        }
    }

    private static bool HasDuplicateNameForSameValueOfNestedProjects(SlnSection nestedProjectsSection, string name, string value, IList<SlnProject> projects)
    {
        foreach (var property in nestedProjectsSection.Properties)
        {
            if (property.Value == value)
            {
                var existingProject = projects.FirstOrDefault(p => p.Id == property.Key);

                if (existingProject != null && existingProject.Name == name)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static Dictionary<string, string> GetSolutionFolderPaths(
        this SlnFile slnFile,
        SlnPropertySet nestedProjects)
    {
        var solutionFolderPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var solutionFolderProjects = slnFile.Projects.GetProjectsByType(ProjectTypeGuids.SolutionFolderGuid);
        foreach (var slnProject in solutionFolderProjects)
        {
            var path = slnProject.FilePath;
            var id = slnProject.Id;
            while (nestedProjects.ContainsKey(id))
            {
                id = nestedProjects[id];
                var parentSlnProject = solutionFolderProjects.SingleOrDefault(p => p.Id == id)
                    // see: https://github.com/dotnet/sdk/pull/28811
                    ?? throw new GracefulException(CommonLocalizableStrings.CorruptSolutionProjectFolderStructure, slnFile.FullPath, id);
                path = Path.Combine(parentSlnProject.FilePath, path);
            }

            solutionFolderPaths[path] = slnProject.Id;
        }

        return solutionFolderPaths;
    }

    public static bool RemoveProject(this SlnFile slnFile, string projectPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);

        var projectPathNormalized = PathUtility.GetPathWithDirectorySeparator(projectPath);

        var projectsToRemove = slnFile.Projects.Where((p) =>
                string.Equals(p.FilePath, projectPathNormalized, StringComparison.OrdinalIgnoreCase)).ToList();

        bool projectRemoved = false;
        if (projectsToRemove.Count == 0)
        {
            Reporter.Output.WriteLine(string.Format(
                CommonLocalizableStrings.ProjectNotFoundInTheSolution,
                projectPath));
        }
        else
        {
            foreach (var slnProject in projectsToRemove)
            {
                var buildConfigsToRemove = slnFile.ProjectConfigurationsSection.GetPropertySet(slnProject.Id);
                if (buildConfigsToRemove != null)
                {
                    slnFile.ProjectConfigurationsSection.Remove(buildConfigsToRemove);
                }

                var nestedProjectsSection = slnFile.Sections.GetSection(
                    "NestedProjects",
                    SlnSectionType.PreProcess);
                if (nestedProjectsSection != null && nestedProjectsSection.Properties.ContainsKey(slnProject.Id))
                {
                    nestedProjectsSection.Properties.Remove(slnProject.Id);
                }

                slnFile.Projects.Remove(slnProject);
                Reporter.Output.WriteLine(
                    string.Format(CommonLocalizableStrings.ProjectRemovedFromTheSolution, slnProject.FilePath));
            }

            foreach (var project in slnFile.Projects)
            {
                var dependencies = project.Dependencies;
                if (dependencies == null)
                {
                    continue;
                }

                dependencies.SkipIfEmpty = true;

                foreach (var removed in projectsToRemove)
                {
                    dependencies.Properties.Remove(removed.Id);
                }
            }

            projectRemoved = true;
        }

        return projectRemoved;
    }

    public static void RemoveEmptyConfigurationSections(this SlnFile slnFile)
    {
        if (slnFile.Projects.Count == 0)
        {
            var solutionConfigs = slnFile.Sections.GetSection("SolutionConfigurationPlatforms");
            if (solutionConfigs != null)
            {
                slnFile.Sections.Remove(solutionConfigs);
            }

            var projectConfigs = slnFile.Sections.GetSection("ProjectConfigurationPlatforms");
            if (projectConfigs != null)
            {
                slnFile.Sections.Remove(projectConfigs);
            }
        }
    }

    public static void RemoveEmptySolutionFolders(this SlnFile slnFile)
    {
        var solutionFolderProjects = slnFile.Projects
            .GetProjectsByType(ProjectTypeGuids.SolutionFolderGuid)
            .ToList();

        if (solutionFolderProjects.Count != 0)
        {
            var nestedProjectsSection = slnFile.Sections.GetSection(
                "NestedProjects",
                SlnSectionType.PreProcess);

            if (nestedProjectsSection == null)
            {
                foreach (var solutionFolderProject in solutionFolderProjects)
                {
                    if (solutionFolderProject.Sections.Count == 0)
                    {
                        slnFile.Projects.Remove(solutionFolderProject);
                    }
                }
            }
            else
            {
                var solutionFoldersInUse = slnFile.GetSolutionFoldersThatContainProjectsInItsHierarchy(
                    nestedProjectsSection.Properties);

                solutionFoldersInUse.UnionWith(slnFile.GetSolutionFoldersThatContainSolutionItemsInItsHierarchy(
                    nestedProjectsSection.Properties));

                foreach (var solutionFolderProject in solutionFolderProjects)
                {
                    if (!solutionFoldersInUse.Contains(solutionFolderProject.Id))
                    {
                        nestedProjectsSection.Properties.Remove(solutionFolderProject.Id);
                        if (solutionFolderProject.Sections.Count == 0)
                        {
                            slnFile.Projects.Remove(solutionFolderProject);
                        }
                    }
                }

                if (nestedProjectsSection.IsEmpty)
                {
                    slnFile.Sections.Remove(nestedProjectsSection);
                }
            }
        }
    }

    private static HashSet<string> GetSolutionFoldersThatContainProjectsInItsHierarchy(
        this SlnFile slnFile,
        SlnPropertySet nestedProjects)
    {
        var solutionFoldersInUse = new HashSet<string>();

        IEnumerable<SlnProject> nonSolutionFolderProjects;
        nonSolutionFolderProjects = slnFile.Projects.GetProjectsNotOfType(
             ProjectTypeGuids.SolutionFolderGuid);

        foreach (var nonSolutionFolderProject in nonSolutionFolderProjects)
        {
            var id = nonSolutionFolderProject.Id;
            while (nestedProjects.ContainsKey(id))
            {
                id = nestedProjects[id];
                solutionFoldersInUse.Add(id);
            }
        }

        return solutionFoldersInUse;
    }

    private static HashSet<string> GetSolutionFoldersThatContainSolutionItemsInItsHierarchy(
        this SlnFile slnFile,
        SlnPropertySet nestedProjects)
    {
        var solutionFoldersInUse = new HashSet<string>();

        var solutionItemsFolderProjects = slnFile.Projects
                .GetProjectsByType(ProjectTypeGuids.SolutionFolderGuid)
                .Where(p => p.GetSolutionItemsSectionOrDefault() != null);

        foreach (var solutionItemsFolderProject in solutionItemsFolderProjects)
        {
            var id = solutionItemsFolderProject.Id;
            solutionFoldersInUse.Add(id);

            while (nestedProjects.ContainsKey(id))
            {
                id = nestedProjects[id];
                solutionFoldersInUse.Add(id);
            }
        }

        return solutionFoldersInUse;
    }
}
