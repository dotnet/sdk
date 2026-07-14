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

    public bool RunMSBuild()
    {
        PathOptions pathOptions = _buildOptions.PathOptions;

        if (!ValidationUtility.ValidateBuildPathOptions(pathOptions, out string? projectOrSolutionFilePath, out bool isSolution))
        {
            return false;
        }

        (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> projects, int buildExitCode) = isSolution ?
            MSBuildUtility.GetProjectsFromSolution(projectOrSolutionFilePath, _buildOptions) :
            MSBuildUtility.GetProjectsFromProject(projectOrSolutionFilePath, _buildOptions);

        LogProjectProperties(projects);

        if (buildExitCode != 0)
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.CmdMSBuildProjectsPropertiesErrorDescription, buildExitCode));
            return false;
        }

        return InitializeTestApplications(projects);
    }

    private bool InitializeTestApplications(IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> moduleGroups)
    {
        // If one test app has IsTestingPlatformApplication set to false (VSTest and not MTP), then we will not run any of the test apps
        IEnumerable<TestModule> vsTestTestProjects = moduleGroups.SelectMany(group => group.GetVSTestAndNotMTPModules());

        if (vsTestTestProjects.Any())
        {
            Reporter.Error.WriteLine(
                string.Format(
                    CliCommandStrings.CmdUnsupportedVSTestTestApplicationsDescription,
                    string.Join(Environment.NewLine, vsTestTestProjects.Select(module => Path.GetFileName(module.ProjectFullPath))).Red().Bold()));

            return false;
        }

        foreach (ParallelizableTestModuleGroupWithSequentialInnerModules moduleGroup in moduleGroups)
        {
            _testApplications.Add(moduleGroup);
        }

        if (_testApplications.IsEmpty)
        {
            Reporter.Error.WriteLine(CliCommandStrings.CmdTestNoTestProjectsFound);
            return false;
        }

        return true;
    }

    public void EnqueueTestApplications(TestApplicationActionQueue queue)
    {
        foreach (var testApp in _testApplications)
        {
            queue.Enqueue(testApp);
        }
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
