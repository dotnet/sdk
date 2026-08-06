// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed class MSBuildHandler(BuildOptions buildOptions)
{
    private readonly BuildOptions _buildOptions = buildOptions;

    private readonly ConcurrentBag<ParallelizableTestModuleGroupWithSequentialInnerModules> _testApplications = [];
    private bool _areTestingPlatformApplications = true;

    public bool RunMSBuild()
    {
        if (!ValidationUtility.ValidateBuildPathOptions(_buildOptions))
        {
            return false;
        }

        int msBuildExitCode;
        string path;
        PathOptions pathOptions = _buildOptions.PathOptions;

        if (!string.IsNullOrEmpty(pathOptions.ProjectPath))
        {
            path = PathUtility.GetFullPath(pathOptions.ProjectPath);

            msBuildExitCode = Directory.Exists(path)
                ? RunBuild(path, expectProject: true)
                : RunBuild(path, isSolution: false);
        }
        else if (!string.IsNullOrEmpty(pathOptions.SolutionPath))
        {
            path = PathUtility.GetFullPath(pathOptions.SolutionPath);

            msBuildExitCode = Directory.Exists(path)
                ? RunBuild(path, expectSolution: true)
                : RunBuild(path, isSolution: true);
        }
        else
        {
            path = PathUtility.GetFullPath(Directory.GetCurrentDirectory());
            msBuildExitCode = RunBuild(path);
        }

        if (msBuildExitCode != ExitCode.Success)
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.CmdMSBuildProjectsPropertiesErrorDescription, msBuildExitCode));
            return false;
        }

        return true;
    }

    private int RunBuild(string directoryPath, bool expectProject = false, bool expectSolution = false)
    {
        bool solutionOrProjectFileFound;
        string message;
        string projectOrSolutionFilePath;
        bool isSolution;

        if (expectProject)
        {
            (solutionOrProjectFileFound, message) = SolutionAndProjectUtility.TryGetProjectFilePath(directoryPath, out projectOrSolutionFilePath);
            isSolution = false;
        }
        else if (expectSolution)
        {
            (solutionOrProjectFileFound, message) = SolutionAndProjectUtility.TryGetSolutionFilePath(directoryPath, out projectOrSolutionFilePath);
            isSolution = true;
        }
        else
        {
            (solutionOrProjectFileFound, message) = SolutionAndProjectUtility.TryGetProjectOrSolutionFilePath(directoryPath, out projectOrSolutionFilePath, out isSolution);
        }

        if (!solutionOrProjectFileFound)
        {
            Reporter.Error.WriteLine(message);
            return ExitCode.GenericFailure;
        }

        (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> projects, bool restored) = GetProjectsProperties(projectOrSolutionFilePath, isSolution);

        InitializeTestApplications(projects);

        return restored && !_testApplications.IsEmpty ? ExitCode.Success : ExitCode.GenericFailure;
    }

    private int RunBuild(string filePath, bool isSolution)
    {
        (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> projects, bool restored) = GetProjectsProperties(filePath, isSolution);

        InitializeTestApplications(projects);

        return restored && !_testApplications.IsEmpty ? ExitCode.Success : ExitCode.GenericFailure;
    }

    private void InitializeTestApplications(IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> moduleGroups)
    {
        // If one test app has IsTestingPlatformApplication set to false (VSTest and not MTP), then we will not run any of the test apps
        IEnumerable<TestModule> vsTestTestProjects = moduleGroups.SelectMany(group => group.GetVSTestAndNotMTPModules());

        if (vsTestTestProjects.Any())
        {
            _areTestingPlatformApplications = false;

            Reporter.Error.WriteLine(
                string.Format(
                    CliCommandStrings.CmdUnsupportedVSTestTestApplicationsDescription,
                    string.Join(Environment.NewLine, vsTestTestProjects.Select(module => Path.GetFileName(module.ProjectFullPath))).Red().Bold()));

            return;
        }

        foreach (ParallelizableTestModuleGroupWithSequentialInnerModules moduleGroup in moduleGroups)
        {
            _testApplications.Add(moduleGroup);
        }
    }

    public bool EnqueueTestApplications(TestApplicationActionQueue queue)
    {
        if (!_areTestingPlatformApplications)
        {
            return false;
        }

        foreach (var testApp in _testApplications)
        {
            queue.Enqueue(testApp);
        }
        return true;
    }

    private (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> Projects, bool Restored) GetProjectsProperties(string solutionOrProjectFilePath, bool isSolution)
    {
        (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> projects, bool isBuiltOrRestored) = isSolution ?
            MSBuildUtility.GetProjectsFromSolution(solutionOrProjectFilePath, _buildOptions) :
            MSBuildUtility.GetProjectsFromProject(solutionOrProjectFilePath, _buildOptions);

        LogProjectProperties(projects);

        return (projects, isBuiltOrRestored);
    }

    private static void LogProjectProperties(IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> moduleGroups)
    {
        if (!Logger.TraceEnabled)
        {
            return;
        }

        var logMessageBuilder = new StringBuilder();

        foreach (var moduleGroup in moduleGroups)
        {
            foreach (var module in moduleGroup)
            {
                logMessageBuilder.AppendLine($"{ProjectProperties.ProjectFullPath}: {module.ProjectFullPath}");
                logMessageBuilder.AppendLine($"{ProjectProperties.IsTestingPlatformApplication}: {module.IsTestingPlatformApplication}");
                logMessageBuilder.AppendLine($"{ProjectProperties.TargetFramework}: {module.TargetFramework}");
                logMessageBuilder.AppendLine($"{ProjectProperties.RunCommand}: {module.RunProperties.Command}");
                logMessageBuilder.AppendLine($"{ProjectProperties.RunArguments}: {module.RunProperties.Arguments}");
                logMessageBuilder.AppendLine($"{ProjectProperties.RunWorkingDirectory}: {module.RunProperties.WorkingDirectory}");
                logMessageBuilder.AppendLine();
            }
        }

        Logger.LogTrace(logMessageBuilder, static logMessageBuilder => logMessageBuilder.ToString());
    }
}
