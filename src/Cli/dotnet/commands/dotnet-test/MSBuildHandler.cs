// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.DotNet.Cli
{
    internal sealed class MSBuildHandler : IDisposable
    {
        private readonly List<string> _args;
        private readonly TestApplicationActionQueue _actionQueue;
        private TerminalTestReporter _output;

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

            if (!string.IsNullOrEmpty(buildOptions.ProjectPath))
            {
                path = PathUtility.GetFullPath(buildOptions.ProjectPath);
                msBuildExitCode = RunBuild(path, isSolution: false, buildOptions);
            }
            else if (!string.IsNullOrEmpty(buildOptions.SolutionPath))
            {
                path = PathUtility.GetFullPath(buildOptions.SolutionPath);
                msBuildExitCode = RunBuild(path, isSolution: true, buildOptions);
            }
            else
            {
                path = PathUtility.GetFullPath(buildOptions.DirectoryPath ?? Directory.GetCurrentDirectory());
                msBuildExitCode = RunBuild(path, buildOptions);
            }

            if (msBuildExitCode != ExitCodes.Success)
            {
                _output.WriteMessage(string.Format(LocalizableStrings.CmdMSBuildProjectsPropertiesErrorDescription, msBuildExitCode));
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
                return ExitCodes.GenericFailure;
            }

            (IEnumerable<Module> projects, bool restored) = GetProjectsProperties(projectOrSolutionFilePath, isSolution, buildOptions);

            InitializeTestApplications(projects);

            return restored ? ExitCodes.Success : ExitCodes.GenericFailure;
        }

        private int RunBuild(string filePath, bool isSolution, BuildOptions buildOptions)
        {
            (IEnumerable<Module> projects, bool restored) = GetProjectsProperties(filePath, isSolution, buildOptions);

            InitializeTestApplications(projects);

            return restored ? ExitCodes.Success : ExitCodes.GenericFailure;
        }

        private void InitializeTestApplications(IEnumerable<Module> modules)
        {
            foreach (Module module in modules)
            {
                if (!module.IsTestProject)
                {
                    // Non test projects, like the projects that include production code are skipped over, we won't run them.
                    return;
                }

                if (!module.IsTestingPlatformApplication)
                {
                    // If one test app has IsTestingPlatformApplication set to false, then we will not run any of the test apps
                    _areTestingPlatformApplications = false;
                    return;
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

        private (IEnumerable<Module> Projects, bool Restored) GetProjectsProperties(string solutionOrProjectFilePath, bool isSolution, BuildOptions buildOptions)
        {
            (IEnumerable<Module> projects, bool isBuiltOrRestored) = isSolution ?
                MSBuildUtility.GetProjectsFromSolution(solutionOrProjectFilePath, buildOptions) :
                MSBuildUtility.GetProjectsFromProject(solutionOrProjectFilePath, buildOptions);

            LogProjectProperties(projects);

            return (projects, isBuiltOrRestored);
        }

        private void LogProjectProperties(IEnumerable<Module> modules)
        {
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            foreach (var module in modules)
            {
                Console.WriteLine();

                VSTestTrace.SafeWriteTrace(() => $"{ProjectProperties.ProjectFullPath}: {module.ProjectFullPath}");
                VSTestTrace.SafeWriteTrace(() => $"{ProjectProperties.IsTestProject}: {module.IsTestProject}");
                VSTestTrace.SafeWriteTrace(() => $"{ProjectProperties.IsTestingPlatformApplication}: {module.IsTestingPlatformApplication}");
                VSTestTrace.SafeWriteTrace(() => $"{ProjectProperties.TargetFramework}: {module.TargetFramework}");
                VSTestTrace.SafeWriteTrace(() => $"{ProjectProperties.TargetPath}: {module.TargetPath}");
                VSTestTrace.SafeWriteTrace(() => $"{ProjectProperties.RunSettingsFilePath}: {module.RunSettingsFilePath}");

                Console.WriteLine();
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
}
