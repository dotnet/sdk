// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Testing.Platform.OutputDevice.Terminal;


namespace Microsoft.DotNet.Cli
{
    internal sealed class MSBuildHandler : IDisposable
    {
        private readonly List<string> _args;
        private readonly TestApplicationActionQueue _actionQueue;
        private readonly int _degreeOfParallelism;
        private TerminalTestReporter _output;

        private readonly ConcurrentBag<TestApplication> _testApplications = new();
        private bool _areTestingPlatformApplications = true;

        public MSBuildHandler(List<string> args, TestApplicationActionQueue actionQueue, int degreeOfParallelism, TerminalTestReporter output)
        {
            _args = args;
            _actionQueue = actionQueue;
            _degreeOfParallelism = degreeOfParallelism;
            _output = output;
        }

        public async Task<bool> RunMSBuild(BuildPathsOptions buildPathOptions)
        {
            if (!ValidateBuildPathOptions(buildPathOptions))
            {
                return false;
            }

            int msBuildExitCode;
            string path;

            if (!string.IsNullOrEmpty(buildPathOptions.ProjectPath))
            {
                path = PathUtility.GetFullPath(buildPathOptions.ProjectPath);
                msBuildExitCode = await RunBuild(path, isSolution: false, buildPathOptions);
            }
            else if (!string.IsNullOrEmpty(buildPathOptions.SolutionPath))
            {
                path = PathUtility.GetFullPath(buildPathOptions.SolutionPath);
                msBuildExitCode = await RunBuild(path, isSolution: true, buildPathOptions);
            }
            else
            {
                path = PathUtility.GetFullPath(buildPathOptions.DirectoryPath ?? Directory.GetCurrentDirectory());
                msBuildExitCode = await RunBuild(path, buildPathOptions);
            }

            if (msBuildExitCode != ExitCodes.Success)
            {
                _output.WriteMessage(string.Format(LocalizableStrings.CmdMSBuildProjectsPropertiesErrorDescription, msBuildExitCode));
                return false;
            }

            return true;
        }

        private bool ValidateBuildPathOptions(BuildPathsOptions buildPathOptions)
        {
            if ((!string.IsNullOrEmpty(buildPathOptions.ProjectPath) && !string.IsNullOrEmpty(buildPathOptions.SolutionPath)) ||
                (!string.IsNullOrEmpty(buildPathOptions.ProjectPath) && !string.IsNullOrEmpty(buildPathOptions.DirectoryPath)) ||
                (!string.IsNullOrEmpty(buildPathOptions.SolutionPath) && !string.IsNullOrEmpty(buildPathOptions.DirectoryPath)))
            {
                _output.WriteMessage(LocalizableStrings.CmdMultipleBuildPathOptionsErrorDescription);
                return false;
            }

            if (!string.IsNullOrEmpty(buildPathOptions.ProjectPath))
            {
                return ValidateFilePath(buildPathOptions.ProjectPath, CliConstants.ProjectExtensions, LocalizableStrings.CmdInvalidProjectFileExtensionErrorDescription);
            }

            if (!string.IsNullOrEmpty(buildPathOptions.SolutionPath))
            {
                return ValidateFilePath(buildPathOptions.SolutionPath, CliConstants.SolutionExtensions, LocalizableStrings.CmdInvalidSolutionFileExtensionErrorDescription);
            }

            if (!string.IsNullOrEmpty(buildPathOptions.DirectoryPath) && !Directory.Exists(buildPathOptions.DirectoryPath))
            {
                _output.WriteMessage(string.Format(LocalizableStrings.CmdNonExistentDirectoryErrorDescription, buildPathOptions.DirectoryPath));
                return false;
            }

            return true;
        }

        private bool ValidateFilePath(string filePath, string[] validExtensions, string errorMessage)
        {
            if (!validExtensions.Contains(Path.GetExtension(filePath)))
            {
                _output.WriteMessage(string.Format(errorMessage, filePath));
                return false;
            }

            if (!File.Exists(filePath))
            {
                _output.WriteMessage(string.Format(LocalizableStrings.CmdNonExistentFileErrorDescription, Path.GetFullPath(filePath)));
                return false;
            }

            return true;
        }

        private async Task<int> RunBuild(string directoryPath, BuildPathsOptions buildPathOptions)
        {
            (bool solutionOrProjectFileFound, string message) = SolutionAndProjectUtility.TryGetProjectOrSolutionFilePath(directoryPath, out string projectOrSolutionFilePath, out bool isSolution);

            if (!solutionOrProjectFileFound)
            {
                _output.WriteMessage(message);
                return ExitCodes.GenericFailure;
            }

            (IEnumerable<Module> modules, bool restored) = await GetProjectsProperties(projectOrSolutionFilePath, isSolution, buildPathOptions);

            InitializeTestApplications(modules);

            return restored ? ExitCodes.Success : ExitCodes.GenericFailure;
        }

        private async Task<int> RunBuild(string filePath, bool isSolution, BuildPathsOptions buildPathOptions)
        {
            (IEnumerable<Module> modules, bool restored) = await GetProjectsProperties(filePath, isSolution, buildPathOptions);

            InitializeTestApplications(modules);

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

        private async Task<(IEnumerable<Module>, bool Restored)> GetProjectsProperties(string solutionOrProjectFilePath, bool isSolution, BuildPathsOptions buildPathOptions)
        {
            Debugger.Launch();
            bool isBuiltOrRestored = true;
            var allProjects = new ConcurrentBag<Module>();
            var projectCollection = new ProjectCollection();
            bool allowBinLog = IsBinaryLoggerEnabled(_args, out string binLogFileName);

            if (isSolution)
            {
                string fileDirectory = Path.GetDirectoryName(solutionOrProjectFilePath);
                string rootDirectory = string.IsNullOrEmpty(fileDirectory)
                    ? Directory.GetCurrentDirectory()
                    : fileDirectory;

                var projects = await SolutionAndProjectUtility.ParseSolution(solutionOrProjectFilePath, rootDirectory);

                MSBuildBuildAndRestoreSettings msBuildBuildAndRestoreSettings = new(GetCommands(buildPathOptions.HasNoRestore, buildPathOptions.HasNoBuild), buildPathOptions.Configuration, buildPathOptions.RuntimeIdentifier, allowBinLog, binLogFileName);
                CreateSlnfFile(projects.ToList(), "Projects.slnf", solutionOrProjectFilePath);

                isBuiltOrRestored = BuildOrRestoreProjectOrSolution("Projects.slnf", projectCollection, msBuildBuildAndRestoreSettings);

                ProcessProjectsInParallel(projectCollection, projects, allProjects);
            }
            else
            {
                if (!buildPathOptions.HasNoRestore)
                {
                    MSBuildBuildAndRestoreSettings msBuildRestoreSettings = new([CliConstants.RestoreCommand], buildPathOptions.Configuration, buildPathOptions.RuntimeIdentifier, allowBinLog, binLogFileName);
                    isBuiltOrRestored = BuildOrRestoreProjectOrSolution(solutionOrProjectFilePath, projectCollection, msBuildRestoreSettings);
                }

                if (!buildPathOptions.HasNoBuild)
                {
                    MSBuildBuildAndRestoreSettings msBuildBuildSettings = new([CliConstants.BuildCommand], buildPathOptions.Configuration, buildPathOptions.RuntimeIdentifier, allowBinLog, binLogFileName);
                    isBuiltOrRestored = isBuiltOrRestored && BuildOrRestoreProjectOrSolution(solutionOrProjectFilePath, projectCollection, msBuildBuildSettings);
                }

                IEnumerable<Module> relatedProjects = GetProjectPropertiesInternal(solutionOrProjectFilePath, projectCollection);
                foreach (var relatedProject in relatedProjects)
                {
                    allProjects.Add(relatedProject);
                }
            }

            LogProjectProperties(allProjects);

            return (allProjects, isBuiltOrRestored);
        }

        public static string[] GetCommands(bool hasNoRestore, bool hasNoBuild)
        {
            var commands = new List<string>();

            if (!hasNoRestore)
            {
                commands.Add(CliConstants.RestoreCommand);
            }

            if (!hasNoBuild)
            {
                commands.Add(CliConstants.BuildCommand);
            }

            return commands.ToArray();
        }

        private void ProcessProjectsInParallel(ProjectCollection projectCollection, IEnumerable<string> projects, ConcurrentBag<Module> allProjects)
        {
            Parallel.ForEach(
                projects,
                new ParallelOptions { MaxDegreeOfParallelism = _degreeOfParallelism },
                (project, state) =>
                {
                    IEnumerable<Module> relatedProjects = GetProjectPropertiesInternal(project, projectCollection);
                    foreach (var relatedProject in relatedProjects)
                    {
                        allProjects.Add(relatedProject);
                    }
                });
        }

        private static IEnumerable<Module> GetProjectPropertiesInternal(string projectFilePath, ProjectCollection projectCollection)
        {
            var project = projectCollection.LoadProject(projectFilePath);
            return ExtractModulesFromProject(project);
        }

        private static bool BuildOrRestoreProjectOrSolution(string filePath, ProjectCollection projectCollection, MSBuildBuildAndRestoreSettings msBuildBuildAndRestoreSettings)
        {
            var parameters = GetBuildParameters(projectCollection, msBuildBuildAndRestoreSettings);
            var globalProperties = GetGlobalProperties(msBuildBuildAndRestoreSettings);

            var buildRequestData = new BuildRequestData(filePath, globalProperties, null, msBuildBuildAndRestoreSettings.Commands, null);

            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(parameters, buildRequestData);

            return buildResult.OverallResult == BuildResultCode.Success;
        }

        private static BuildParameters GetBuildParameters(ProjectCollection projectCollection, MSBuildBuildAndRestoreSettings msBuildBuildAndRestoreSettings)
        {
            BuildParameters parameters = new(projectCollection)
            {
                Loggers = [new ConsoleLogger(LoggerVerbosity.Quiet)]
            };

            if (!msBuildBuildAndRestoreSettings.AllowBinLog)
                return parameters;

            parameters.Loggers =
            [
                .. parameters.Loggers,
                    .. new[]
                    {
                    new BinaryLogger
                    {
                        Parameters = msBuildBuildAndRestoreSettings.BinLogFileName
                    }
                    },
                ];

            return parameters;
        }

        private static Dictionary<string, string> GetGlobalProperties(MSBuildBuildAndRestoreSettings msBuildBuildAndRestoreSettings)
        {
            var globalProperties = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(msBuildBuildAndRestoreSettings.Configuration))
            {
                globalProperties[CliConstants.Configuration] = msBuildBuildAndRestoreSettings.Configuration;
            }

            if (!string.IsNullOrEmpty(msBuildBuildAndRestoreSettings.RuntimeIdentifier))
            {
                globalProperties[CliConstants.RuntimeIdentifier] = msBuildBuildAndRestoreSettings.RuntimeIdentifier;
            }

            return globalProperties;
        }

        private static IEnumerable<Module> ExtractModulesFromProject(Project project)
        {
            _ = bool.TryParse(project.GetPropertyValue(ProjectProperties.IsTestProject), out bool isTestProject);

            if (!isTestProject)
            {
                return [];
            }

            _ = bool.TryParse(project.GetPropertyValue(ProjectProperties.IsTestingPlatformApplication), out bool isTestingPlatformApplication);

            string targetFramework = project.GetPropertyValue(ProjectProperties.TargetFramework);
            string targetFrameworks = project.GetPropertyValue(ProjectProperties.TargetFrameworks);
            string targetPath = project.GetPropertyValue(ProjectProperties.TargetPath);
            string projectFullPath = project.GetPropertyValue(ProjectProperties.ProjectFullPath);
            string runSettingsFilePath = project.GetPropertyValue(ProjectProperties.RunSettingsFilePath);

            var projects = new List<Module>();

            if (string.IsNullOrEmpty(targetFrameworks))
            {
                projects.Add(new Module(targetPath, PathUtility.FixFilePath(projectFullPath), targetFramework, runSettingsFilePath, isTestingPlatformApplication, isTestProject));
            }
            else
            {
                var frameworks = targetFrameworks.Split(CliConstants.SemiColon, StringSplitOptions.RemoveEmptyEntries);
                foreach (var framework in frameworks)
                {
                    project.SetProperty(ProjectProperties.TargetFramework, framework);
                    project.ReevaluateIfNecessary();

                    projects.Add(new Module(project.GetPropertyValue(ProjectProperties.TargetPath),
                        PathUtility.FixFilePath(projectFullPath),
                        framework,
                        runSettingsFilePath,
                        isTestingPlatformApplication,
                        isTestProject));
                }
            }

            return projects;
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

                VSTestTrace.SafeWriteTrace(() => $"{ProjectProperties.ProjectFullPath}: {module.ProjectPath}");
                VSTestTrace.SafeWriteTrace(() => $"{ProjectProperties.IsTestProject}: {module.IsTestProject}");
                VSTestTrace.SafeWriteTrace(() => $"{ProjectProperties.IsTestingPlatformApplication}: {module.IsTestingPlatformApplication}");
                VSTestTrace.SafeWriteTrace(() => $"{ProjectProperties.TargetFramework}: {module.TargetFramework}");
                VSTestTrace.SafeWriteTrace(() => $"{ProjectProperties.TargetPath}: {module.DllOrExePath}");
                VSTestTrace.SafeWriteTrace(() => $"{ProjectProperties.RunSettingsFilePath}: {module.RunSettingsFilePath}");

                Console.WriteLine();
            }
        }

        internal static bool IsBinaryLoggerEnabled(List<string> args, out string binLogFileName)
        {
            binLogFileName = string.Empty;
            var binLogArgs = new List<string>();

            foreach (var arg in args)
            {
                if (arg.StartsWith("/bl:") || arg.Equals("/bl")
                    || arg.StartsWith("--binaryLogger:") || arg.Equals("--binaryLogger")
                    || arg.StartsWith("-bl:") || arg.Equals("-bl"))
                {
                    binLogArgs.Add(arg);

                }
            }

            if (binLogArgs.Count > 0)
            {
                // Remove all BinLog args from the list of args
                args.RemoveAll(arg => binLogArgs.Contains(arg));

                // Get BinLog filename
                var binLogArg = binLogArgs.LastOrDefault();

                if (binLogArg.Contains(CliConstants.Colon))
                {
                    var parts = binLogArg.Split(CliConstants.Colon, 2);
                    binLogFileName = !string.IsNullOrEmpty(parts[1]) ? parts[1] : CliConstants.BinLogFileName;
                }
                else
                {
                    binLogFileName = CliConstants.BinLogFileName;
                }

                return true;
            }

            return false;
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
