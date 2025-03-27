// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.Tools.Run.LaunchSettings;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.DotNet.Cli;

internal sealed class MSBuildHandler : IDisposable
{
    private readonly BuildOptions _buildOptions;
    private readonly TestApplicationActionQueue _actionQueue;
    private readonly TerminalTestReporter _output;

    private readonly ConcurrentBag<TestApplication> _testApplications = [];
    private bool _areTestingPlatformApplications = true;

    public MSBuildHandler(BuildOptions buildOptions, TestApplicationActionQueue actionQueue, TerminalTestReporter output)
    {
        _buildOptions = buildOptions;
        _actionQueue = actionQueue;
        _output = output;
    }

    public bool RunMSBuild()
    {
        if (!ValidationUtility.ValidateBuildPathOptions(_buildOptions, _output))
        {
            return false;
        }

        int msBuildExitCode;
        string path;
        PathOptions pathOptions = _buildOptions.PathOptions;

        if (!string.IsNullOrEmpty(pathOptions.ProjectPath))
        {
            path = PathUtility.GetFullPath(pathOptions.ProjectPath);
            msBuildExitCode = RunBuild(path, isSolution: false);
        }
        else if (!string.IsNullOrEmpty(pathOptions.SolutionPath))
        {
            path = PathUtility.GetFullPath(pathOptions.SolutionPath);
            msBuildExitCode = RunBuild(path, isSolution: true);
        }
        else
        {
            path = PathUtility.GetFullPath(pathOptions.DirectoryPath ?? Directory.GetCurrentDirectory());
            msBuildExitCode = RunBuild(path);
        }

        if (msBuildExitCode != ExitCode.Success)
        {
            _output.WriteMessage(string.Format(Tools.Test.LocalizableStrings.CmdMSBuildProjectsPropertiesErrorDescription, msBuildExitCode));
            return false;
        }

        return true;
    }

    private int RunBuild(string directoryPath)
    {
        (bool solutionOrProjectFileFound, string message) = SolutionAndProjectUtility.TryGetProjectOrSolutionFilePath(directoryPath, out string projectOrSolutionFilePath, out bool isSolution);

        if (!solutionOrProjectFileFound)
        {
            _output.WriteMessage(message);
            return ExitCode.GenericFailure;
        }

        (IEnumerable<TestModule> projects, bool restored) = GetProjectsProperties(projectOrSolutionFilePath, isSolution);

        InitializeTestApplications(projects);
        // refactor
        foreach (var project in projects)
        {
            if (!project.IsTestProject && !project.IsTestingPlatformApplication)
            {
                // This should never happen. We should only ever create TestModule if it's a test project.
                throw new UnreachableException($"This program location is thought to be unreachable. Class='{nameof(MSBuildHandler)}' Method='{nameof(InitializeTestApplications)}'");
            }

            if (!TryGetLaunchProfileSettingsIfNeeded(project.ProjectFullPath, out ProjectLaunchSettingsModel? launchSettings))
            {
                return ExitCode.GenericFailure;
            }

            var testApp = new TestApplication(project, _buildOptions, launchSettings);
            _testApplications.Add(testApp);
        }

        return restored ? ExitCode.Success : ExitCode.GenericFailure;
    }

    private int RunBuild(string filePath, bool isSolution)
    {
        (IEnumerable<TestModule> projects, bool restored) = GetProjectsProperties(filePath, isSolution);

        InitializeTestApplications(projects);

        // refactor
        foreach (var project in projects)
        {
            if (!project.IsTestProject && !project.IsTestingPlatformApplication)
            {
                // This should never happen. We should only ever create TestModule if it's a test project.
                throw new UnreachableException($"This program location is thought to be unreachable. Class='{nameof(MSBuildHandler)}' Method='{nameof(InitializeTestApplications)}'");
            }

            if (!TryGetLaunchProfileSettingsIfNeeded(project.ProjectFullPath, out ProjectLaunchSettingsModel? launchSettings))
            {
                return ExitCode.GenericFailure;
            }

            var testApp = new TestApplication(project, _buildOptions, launchSettings);
            _testApplications.Add(testApp);
        }

        return restored ? ExitCode.Success : ExitCode.GenericFailure;
    }

    private bool TryGetLaunchProfileSettingsIfNeeded(string projectFilePath, out ProjectLaunchSettingsModel? launchSettingsModel)
    {
        launchSettingsModel = default;
        if (_buildOptions.LaunchSettingsOption.NoLaunchProfile)
        {
            return true;
        }

        var launchSettingsPath = TryFindLaunchSettings(projectFilePath);
        var launchProfile = _buildOptions.LaunchSettingsOption.LaunchProfile;
        if (!File.Exists(launchSettingsPath))
        {
            if (!string.IsNullOrEmpty(launchProfile))
            {
                _output.WriteMessage(string.Format("RunCommandExceptionCouldNotLocateALaunchSettingsFile").Bold().Red());
            }
            return true;
        }

        //if (_buildOptions.Verbosity.IsQuiet() != true)
        //{
        //    Reporter.Output.WriteLine(string.Format("UsingLaunchSettingsFromMessage"));
        //}

        string profileName = string.IsNullOrEmpty(launchProfile) ? "DefaultLaunchProfileDisplayName" : launchProfile;

        try
        {
            var launchSettingsFileContents = File.ReadAllText(launchSettingsPath);
            var applyResult = LaunchSettingsManager.TryApplyLaunchSettings(launchSettingsFileContents, launchProfile);
            if (!applyResult.Success)
            {
                _output.WriteMessage(string.Format("RunCommandExceptionCouldNotApplyLaunchSettings").Bold().Red());
            }
            else
            {
                launchSettingsModel = applyResult.LaunchSettings;
            }
        }
        catch (IOException ex)
        {
            _output.WriteMessage(string.Format("RunCommandExceptionCouldNotApplyLaunchSettings").Bold().Red());
            _output.WriteMessage(ex.Message.Bold().Red());
            return false;
        }

        return true;

        static string? TryFindLaunchSettings(string projectOrEntryPointFilePath)
        {
            var buildPathContainer = File.Exists(projectOrEntryPointFilePath) ? Path.GetDirectoryName(projectOrEntryPointFilePath) : projectOrEntryPointFilePath;
            if (buildPathContainer is null)
            {
                return null;
            }

            string propsDirectory;

            // VB.NET projects store the launch settings file in the
            // "My Project" directory instead of a "Properties" directory.
            // TODO: use the `AppDesignerFolder` MSBuild property instead, which captures this logic already
            if (string.Equals(Path.GetExtension(projectOrEntryPointFilePath), ".vbproj", StringComparison.OrdinalIgnoreCase))
            {
                propsDirectory = "My Project";
            }
            else
            {
                propsDirectory = "Properties";
            }

            var launchSettingsPath = Path.Combine(buildPathContainer, propsDirectory, "launchSettings.json");
            return launchSettingsPath;
        }
    }

    private void InitializeTestApplications(IEnumerable<TestModule> modules)
    {
        // If one test app has IsTestingPlatformApplication set to false (VSTest and not MTP), then we will not run any of the test apps
        IEnumerable<TestModule> vsTestTestProjects = modules.Where(module => !module.IsTestingPlatformApplication);

        if (vsTestTestProjects.Any())
        {
            _areTestingPlatformApplications = false;

            _output.WriteMessage(
                string.Format(
                    Tools.Test.LocalizableStrings.CmdUnsupportedVSTestTestApplicationsDescription,
                    string.Join(Environment.NewLine, vsTestTestProjects.Select(module => Path.GetFileName(module.ProjectFullPath)))),
                new SystemConsoleColor { ConsoleColor = ConsoleColor.Red });

            return;
        }

        //foreach (TestModule module in modules)
        //{
        //    if (!module.IsTestProject && !module.IsTestingPlatformApplication)
        //    {
        //        // This should never happen. We should only ever create TestModule if it's a test project.
        //        throw new UnreachableException($"This program location is thought to be unreachable. Class='{nameof(MSBuildHandler)}' Method='{nameof(InitializeTestApplications)}'");
        //    }

        //    var testApp = new TestApplication(module, _buildOptions);
        //    _testApplications.Add(testApp);
        //}
    }

    public bool EnqueueTestApplications()
    {
        if (!_areTestingPlatformApplications)
        {
            return false;
        }

        foreach (var testApp in _testApplications)
        {
            _actionQueue.Enqueue(testApp);
        }
        return true;
    }

    private (IEnumerable<TestModule> Projects, bool Restored) GetProjectsProperties(string solutionOrProjectFilePath, bool isSolution)
    {
        (IEnumerable<TestModule> projects, bool isBuiltOrRestored) = isSolution ?
            MSBuildUtility.GetProjectsFromSolution(solutionOrProjectFilePath, _buildOptions) :
            MSBuildUtility.GetProjectsFromProject(solutionOrProjectFilePath, _buildOptions);

        LogProjectProperties(projects);

        return (projects, isBuiltOrRestored);
    }

    private void LogProjectProperties(IEnumerable<TestModule> modules)
    {
        if (!Logger.TraceEnabled)
        {
            return;
        }

        var logMessageBuilder = new StringBuilder();

        foreach (var module in modules)
        {
            logMessageBuilder.AppendLine($"{ProjectProperties.ProjectFullPath}: {module.ProjectFullPath}");
            logMessageBuilder.AppendLine($"{ProjectProperties.IsTestProject}: {module.IsTestProject}");
            logMessageBuilder.AppendLine($"{ProjectProperties.IsTestingPlatformApplication}: {module.IsTestingPlatformApplication}");
            logMessageBuilder.AppendLine($"{ProjectProperties.TargetFramework}: {module.TargetFramework}");
            logMessageBuilder.AppendLine($"{ProjectProperties.RunCommand}: {module.RunProperties.RunCommand}");
            logMessageBuilder.AppendLine($"{ProjectProperties.RunArguments}: {module.RunProperties.RunArguments}");
            logMessageBuilder.AppendLine($"{ProjectProperties.RunWorkingDirectory}: {module.RunProperties.RunWorkingDirectory}");
            logMessageBuilder.AppendLine();
        }

        Logger.LogTrace(() => logMessageBuilder.ToString());
    }

    public void Dispose()
    {
        foreach (var testApplication in _testApplications)
        {
            testApplication.Dispose();
        }
    }
}
