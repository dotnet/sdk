// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.CommandLine;
using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectTools;
using Microsoft.NET.Build.Tasks;
using Microsoft.VisualStudio.SolutionPersistence.Model;

namespace Microsoft.DotNet.Cli;

/// <summary>
/// This class is used to enable properties that edit the Configuration property inside of a .*proj file.
/// Properties such as DebugSymbols are evaluated based on the Configuration set before a project file is evaluated, and the project file may have dependencies on the configuration.
/// Because of this, it is 'impossible' for the project file to correctly influence the value of Configuration.
/// This class allows evaluation of Configuration properties set in the project file before build time by giving back a global Configuration property to inject while building.
/// </summary>
internal sealed class ReleasePropertyProjectLocator(
    ReadOnlyDictionary<string, string>? userSpecifiedExplicitMSBuildProperties,
    string propertyToCheck,
    ReleasePropertyProjectLocator.DependentCommandOptions commandOptions)
{
    public readonly struct DependentCommandOptions(IEnumerable<string>? slnOrProjectArgs, string? configOption = null, string? frameworkOption = null)
    {
        public readonly IEnumerable<string> SlnOrProjectArgs = slnOrProjectArgs ?? [];
        public readonly string? FrameworkOption = frameworkOption;
        public readonly string? ConfigurationOption = configOption;
    }

    private const string SolutionFolderGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
    private const string SharedProjectGuid = "{D954291E-2A0B-460D-934E-DC6B0785DB48}";
    
    private bool _isHandlingSolution = false;

    /// <summary>
    /// Return dotnet CLI command-line parameters (or an empty list) to change configuration based on ...
    /// ... a boolean that may or may not exist in the targeted project.
    /// </summary>
    /// <returns>Returns a string such as -property:configuration=value for a projects desired config. May be empty string.</returns>
    public ReadOnlyDictionary<string, string>? GetCustomDefaultConfigurationValueIfSpecified()
    {
        // Setup
        Debug.Assert(propertyToCheck == MSBuildPropertyNames.PUBLISH_RELEASE || propertyToCheck == MSBuildPropertyNames.PACK_RELEASE, "Only PackRelease or PublishRelease are currently expected.");
        if (string.Equals(Environment.GetEnvironmentVariable(EnvironmentVariableNames.DISABLE_PUBLISH_AND_PACK_RELEASE), "true", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Analyze Global Properties
        var globalProperties = userSpecifiedExplicitMSBuildProperties;
        globalProperties = InjectTargetFrameworkIntoGlobalProperties(globalProperties);

        // Configuration doesn't work in a .proj file, but it does as a global property.
        // Detect either A) --configuration option usage OR /p:Configuration=Foo, if so, don't use these properties.
        if (commandOptions.ConfigurationOption != null || globalProperties is not null && globalProperties.ContainsKey(MSBuildPropertyNames.CONFIGURATION))
            return new Dictionary<string, string>(1, StringComparer.OrdinalIgnoreCase) { [EnvironmentVariableNames.DISABLE_PUBLISH_AND_PACK_RELEASE] = "true" }.AsReadOnly(); // Don't throw error if publish* conflicts but global config specified.

        // Determine the project being acted upon
        ProjectInstance? project = GetTargetedProject(globalProperties);

        // Determine the correct value to return
        if (project != null)
        {
            string propertyToCheckValue = project.GetPropertyValue(propertyToCheck);
            if (!string.IsNullOrEmpty(propertyToCheckValue))
            {
                var newConfigurationArgs = new Dictionary<string, string>(2, StringComparer.OrdinalIgnoreCase);

                if (propertyToCheckValue.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    newConfigurationArgs[MSBuildPropertyNames.CONFIGURATION] = MSBuildPropertyNames.CONFIGURATION_RELEASE_VALUE;
                }

                if (_isHandlingSolution) // This will allow us to detect conflicting configuration values during evaluation.
                {
                    newConfigurationArgs[$"_SolutionLevel{propertyToCheck}"] = propertyToCheckValue;
                }

                return newConfigurationArgs.AsReadOnly();
            }
        }
        return null;
    }

    /// <summary>
    /// Mirror the MSBuild logic for discovering a project or a solution and find that item.
    /// </summary>
    /// <returns>A project instance that will be targeted to publish/pack, etc. null if one does not exist.
    /// Will return an arbitrary project in the solution if one exists in the solution and there's no project targeted.</returns>
    public ProjectInstance? GetTargetedProject(ReadOnlyDictionary<string, string>? globalProps)
    {
        foreach (string arg in commandOptions.SlnOrProjectArgs.Append(Directory.GetCurrentDirectory()))
        {
            if (VirtualProjectBuilder.IsValidEntryPointPath(arg))
            {
                return new VirtualProjectBuildingCommand(Path.GetFullPath(arg), MSBuildArgs.FromProperties(globalProps))
                    .CreateProjectInstance(ProjectCollection.GlobalProjectCollection);
            }
            else if (IsValidProjectFilePath(arg))
            {
                return TryGetProjectInstance(arg, globalProps);
            }
            else if (IsValidSlnFilePath(arg))
            {
                return GetArbitraryProjectFromSolution(arg, globalProps);
            }
            else if (Directory.Exists(arg)) // Get here if the user did not provide a .proj or a .sln. (See CWD appended to args above)
            {
                // First, look for a project in the directory.
                if (MsbuildProject.TryGetProjectFileFromDirectory(arg, out var projectFilePath))
                {
                    return TryGetProjectInstance(projectFilePath, globalProps);
                }

                // Fall back to looking for a solution if multiple project files are found, or there's no project in the directory.
                string? potentialSln = SlnFileFactory.ListSolutionFilesInDirectory(arg, false).FirstOrDefault();

                if (!string.IsNullOrEmpty(potentialSln))
                {
                    return GetArbitraryProjectFromSolution(potentialSln, globalProps);
                }
            }
        }
        return null;  // If nothing can be found: that's caught by MSBuild XMake::ProcessProjectSwitch -- don't change the behavior by failing here.
    }

    /// <returns>An arbitrary existant project in a solution file. Returns null if no projects exist.
    /// Throws exception if two+ projects disagree in PublishRelease, PackRelease, or whatever _propertyToCheck is, and have it defined.</returns>
    public ProjectInstance? GetArbitraryProjectFromSolution(string slnPath, ReadOnlyDictionary<string, string>? globalProps)
    {
        string slnFullPath = Path.GetFullPath(slnPath);
        if (!Path.Exists(slnFullPath))
        {
            return null;
        }
        SolutionModel sln;
        try
        {
            sln = SlnFileFactory.CreateFromFileOrDirectory(slnFullPath, false, false);
        }
        catch (GracefulException)
        {
            return null; // This can be called if a solution doesn't exist. MSBuild will catch that for us.
        }

        _isHandlingSolution = true;
        List<ProjectInstance> configuredProjects = [];
        HashSet<string> configValues = [];
        object projectDataLock = new();

        if (string.Equals(Environment.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_LAZY_PUBLISH_AND_PACK_RELEASE_FOR_SOLUTIONS), "true", StringComparison.OrdinalIgnoreCase))
        {
            // Evaluate only one project for speed if this environment variable is used. Will break more customers if enabled (adding 8.0 project to SLN with other project TFMs with no Publish or PackRelease.)
            return GetSingleProjectFromSolution(sln, slnFullPath, globalProps);
        }

        Parallel.ForEach(sln.SolutionProjects.AsEnumerable(), (project, state) =>
        {
#pragma warning disable CS8604 // Possible null reference argument.
            string projectFullPath = Path.GetFullPath(project.FilePath, Path.GetDirectoryName(slnFullPath));
#pragma warning restore CS8604 // Possible null reference argument.
            if (IsUnanalyzableProjectInSolution(project, projectFullPath))
                return;

            var projectData = TryGetProjectInstance(projectFullPath, globalProps);
            if (projectData == null)
            {
                return;
            }

            string pReleasePropertyValue = projectData.GetPropertyValue(propertyToCheck);
            if (!string.IsNullOrEmpty(pReleasePropertyValue))
            {
                lock (projectDataLock)
                {
                    configuredProjects.Add(projectData);
                    configValues.Add(pReleasePropertyValue.ToLower());
                }
            }
        });

        if (configuredProjects.Any() && configValues.Count > 1)
        {
            // Note:
            // 1) This error should not be thrown in VS because it is part of the SDK CLI code
            // 2) If PublishRelease or PackRelease is disabled via opt out, or Configuration is specified, we won't get to this code, so we won't error
            // 3) This code only gets hit if we are in a solution publish setting, so we don't need to worry about it failing other publish scenarios
            throw new GracefulException(Strings.SolutionProjectConfigurationsConflict, propertyToCheck, string.Join("\n", (configuredProjects).Select(x => x.FullPath)));
        }
        return configuredProjects.FirstOrDefault();
    }

    /// <summary>
    /// Returns an arbitrary project for the solution. Relies on the .NET SDK PrepareForPublish or _VerifyPackReleaseConfigurations MSBuild targets to catch conflicting values of a given property, like PublishRelease or PackRelease.
    /// </summary>
    /// <param name="solution">The solution to get an arbitrary project from.</param>
    /// <param name="globalProps">The global properties to load into the project.</param>
    /// <returns>null if no project exists in the solution that can be evaluated properly. Else, the first project in the solution that can be.</returns>
    private ProjectInstance? GetSingleProjectFromSolution(SolutionModel sln, string slnPath, ReadOnlyDictionary<string, string>? globalProps)
    {
        foreach (var project in sln.SolutionProjects.AsEnumerable())
        {
#pragma warning disable CS8604 // Possible null reference argument.
            string projectFullPath = Path.GetFullPath(project.FilePath, Path.GetDirectoryName(slnPath));
#pragma warning restore CS8604 // Possible null reference argument.
            if (IsUnanalyzableProjectInSolution(project, projectFullPath))
                continue;

            var projectData = TryGetProjectInstance(projectFullPath, globalProps);
            if (projectData != null)
            {
                return projectData;
            }
        };

        return null;
    }

    /// <summary>
    /// Analyze if the project appears to be valid and something we can read into memory.
    /// </summary>
    /// <param name="project">The project under a solution to evaluate.</param>
    /// <param name="projectFullPath">The full hard-coded path of the project.</param>
    /// <returns>True if the project is not supported by ProjectInstance class or appears to be invalid.</returns>
    private bool IsUnanalyzableProjectInSolution(SolutionProjectModel project, string projectFullPath)
    {
        return project.TypeId.ToString() == SolutionFolderGuid || project.TypeId.ToString() == SharedProjectGuid || !IsValidProjectFilePath(projectFullPath);
    }

    /// <returns>Creates a ProjectInstance if the project is valid, elsewise, fails.</returns>
    private static ProjectInstance? TryGetProjectInstance(string projectPath, ReadOnlyDictionary<string, string>? globalProperties)
    {
        try
        {
            return new ProjectInstance(projectPath, globalProperties, "Current");
        }
        catch (Exception e) // Catch failed file access, or invalid project files that cause errors when read into memory,
        {
            Reporter.Error.WriteLine(e.Message);
        }
        return null;
    }

    /// <returns>Returns true if the path exists and is a project file type.</returns>
    private static bool IsValidProjectFilePath(string path)
    {
        return File.Exists(path) && Path.GetExtension(path).EndsWith("proj");
    }

    /// <returns>Returns true if the path exists and is a sln file type.</returns>
    private static bool IsValidSlnFilePath(string path)
    {
        return File.Exists(path) && (Path.GetExtension(path).Equals(".sln") || Path.GetExtension(path).Equals(".slnx"));
    }

    /// <summary>
    /// Because command-line options that translate to MSBuild properties aren't in the global arguments from Properties, we need to add the TargetFramework to the collection.
    /// The TargetFramework is the only command-line option besides Configuration that could affect the pre-evaluation.
    /// This allows the pre-evaluation to correctly deduce its Publish or PackRelease value because it will know the actual TargetFramework being used.
    /// </summary>
    /// <param name="oldGlobalProperties">The set of MSBuild properties that were specified explicitly like -p:Property=Foo or in other syntax sugars.</param>
    /// <returns>The same set of global properties for the project, but with the new potential TFM based on -f or --framework.</returns>
    private ReadOnlyDictionary<string, string>? InjectTargetFrameworkIntoGlobalProperties(ReadOnlyDictionary<string, string>? globalProperties)
    {
        if (globalProperties is null || globalProperties.Count == 0)
        {
            if (commandOptions.FrameworkOption != null)
            {
                return new(new Dictionary<string, string>() { [MSBuildPropertyNames.TARGET_FRAMEWORK] = commandOptions.FrameworkOption });
            }
            return null;
        }
        if (commandOptions.FrameworkOption is null)
        {
            return globalProperties;
        }

        var newDictionary = new Dictionary<string, string>(globalProperties, StringComparer.OrdinalIgnoreCase);
        // Note: dotnet -f FRAMEWORK_1 --property:TargetFramework=FRAMEWORK_2 will use FRAMEWORK_1.
        // So we can replace the value in the globals non-dubiously if it exists.
        newDictionary[MSBuildPropertyNames.TARGET_FRAMEWORK] = commandOptions.FrameworkOption;
        return new(newDictionary);
    }
}
