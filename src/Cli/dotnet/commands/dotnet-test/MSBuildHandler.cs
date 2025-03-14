// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.DotNet.Cli;

internal sealed class MSBuildHandler : IDisposable
{
    private readonly List<string> _args;
    private readonly TestApplicationActionQueue _actionQueue;
    private readonly TerminalTestReporter _output;

    private readonly ConcurrentBag<TestApplication> _testApplications = new();
    private bool _areTestingPlatformApplications = true;

    public MSBuildHandler(List<string> args, TestApplicationActionQueue actionQueue, TerminalTestReporter output)
    {
        _args = args;
        _actionQueue = actionQueue;
        _output = output;
    }

    public bool RunMSBuild(BuildOptions buildOptions)
    {
        if (!ValidationUtility.ValidateBuildPathOptions(buildOptions, _output))
        {
            return false;
        }

        int msBuildExitCode;
        string path;
        PathOptions pathOptions = buildOptions.PathOptions;

        if (!string.IsNullOrEmpty(pathOptions.ProjectPath))
        {
            path = PathUtility.GetFullPath(pathOptions.ProjectPath);
            msBuildExitCode = RunBuild(path, isSolution: false, buildOptions);
        }
        else if (!string.IsNullOrEmpty(pathOptions.SolutionPath))
        {
            path = PathUtility.GetFullPath(pathOptions.SolutionPath);
            msBuildExitCode = RunBuild(path, isSolution: true, buildOptions);
        }
        else
        {
            path = PathUtility.GetFullPath(pathOptions.DirectoryPath ?? Directory.GetCurrentDirectory());
            msBuildExitCode = RunBuild(path, buildOptions);
        }

        if (msBuildExitCode != ExitCode.Success)
        {
            _output.WriteMessage(string.Format(Tools.Test.LocalizableStrings.CmdMSBuildProjectsPropertiesErrorDescription, msBuildExitCode));
            return false;
        }

        return true;
    }

    private int RunBuild(string directoryPath, BuildOptions buildOptions)
    {
        (bool solutionOrProjectFileFound, string message) = SolutionAndProjectUtility.TryGetProjectOrSolutionFilePath(directoryPath, out string projectOrSolutionFilePath, out bool isSolution);

        if (!solutionOrProjectFileFound)
        {
            _output.WriteMessage(message);
            return ExitCode.GenericFailure;
        }

        (IEnumerable<TestModule> projects, bool restored) = GetProjectsProperties(projectOrSolutionFilePath, isSolution, buildOptions);

        InitializeTestApplications(projects);

        return restored ? ExitCode.Success : ExitCode.GenericFailure;
    }

    private int RunBuild(string filePath, bool isSolution, BuildOptions buildOptions)
    {
        (IEnumerable<TestModule> projects, bool restored) = GetProjectsProperties(filePath, isSolution, buildOptions);

        InitializeTestApplications(projects);

        return restored ? ExitCode.Success : ExitCode.GenericFailure;
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

        foreach (TestModule module in modules)
        {
            if (!module.IsTestProject && !module.IsTestingPlatformApplication)
            {
                // This should never happen. We should only ever create TestModule if it's a test project.
                throw new UnreachableException($"This program location is thought to be unreachable. Class='{nameof(MSBuildHandler)}' Method='{nameof(InitializeTestApplications)}'");
            }

            var testApp = new TestApplication(module, _args);
            _testApplications.Add(testApp);
        }
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

    private (IEnumerable<TestModule> Projects, bool Restored) GetProjectsProperties(string solutionOrProjectFilePath, bool isSolution, BuildOptions buildOptions)
    {
        (IEnumerable<TestModule> projects, bool isBuiltOrRestored) = isSolution ?
            MSBuildUtility.GetProjectsFromSolution(solutionOrProjectFilePath, buildOptions) :
            MSBuildUtility.GetProjectsFromProject(solutionOrProjectFilePath, buildOptions);

        LogProjectProperties(projects);

        return (projects, isBuiltOrRestored);
    }

    private void LogProjectProperties(IEnumerable<TestModule> modules)
    {
        if (!Logger.TraceEnabled)
        {
            return;
        }

        foreach (var module in modules)
        {
            Logger.LogTrace($"{ProjectProperties.ProjectFullPath}: {module.ProjectFullPath}");
            Logger.LogTrace($"{ProjectProperties.IsTestProject}: {module.IsTestProject}");
            Logger.LogTrace($"{ProjectProperties.IsTestingPlatformApplication}: {module.IsTestingPlatformApplication}");
            Logger.LogTrace($"{ProjectProperties.TargetFramework}: {module.TargetFramework}");
            Logger.LogTrace($"{ProjectProperties.TargetPath}: {module.TargetPath}");
            Logger.LogTrace($"{ProjectProperties.RunSettingsFilePath}: {module.RunSettingsFilePath}");
            Logger.LogTrace("");
        }
    }

    public void Dispose()
    {
        foreach (var testApplication in _testApplications)
        {
            testApplication.Dispose();
        }
    }
}
