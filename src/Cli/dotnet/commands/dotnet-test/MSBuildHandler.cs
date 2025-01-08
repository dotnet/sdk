// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Microsoft.DotNet.Cli
{
    internal sealed class MSBuildHandler : IDisposable
    {
        private readonly List<string> _args;
        private readonly TestApplicationActionQueue _actionQueue;
        private readonly int _degreeOfParallelism;

        private readonly ConcurrentBag<TestApplication> _testApplications = new();
        private bool _areTestingPlatformApplications = true;

        private const string BinLogFileName = "msbuild.binlog";
        private const string Separator = ";";
        private static readonly Lock buildLock = new();

        public MSBuildHandler(List<string> args, TestApplicationActionQueue actionQueue, int degreeOfParallelism)
        {
            _args = args;
            _actionQueue = actionQueue;
            _degreeOfParallelism = degreeOfParallelism;
        }

        public async Task<int> RunWithMSBuild()
        {
            bool solutionOrProjectFileFound = SolutionAndProjectUtility.TryGetProjectOrSolutionFilePath(Directory.GetCurrentDirectory(), out string projectOrSolutionFilePath, out bool isSolution);

            if (!solutionOrProjectFileFound)
            {
                return ExitCodes.GenericFailure;
            }

            (IEnumerable<Module> modules, bool restored) = await GetProjectsProperties(projectOrSolutionFilePath, isSolution);

            InitializeTestApplications(modules);

            return restored ? ExitCodes.Success : ExitCodes.GenericFailure;
        }

        public async Task<int> RunWithMSBuild(string filePath)
        {
            (IEnumerable<Module> modules, bool restored) = await GetProjectsProperties(filePath, false);

            InitializeTestApplications(modules);

            return restored ? ExitCodes.Success : ExitCodes.GenericFailure;
        }

        private void InitializeTestApplications(IEnumerable<Module> modules)
        {
            foreach (Module module in modules)
            {
                if (module.IsTestProject && module.IsTestingPlatformApplication)
                {
                    var testApp = new TestApplication(module, _args);
                    _testApplications.Add(testApp);
                }
                else // If one test app has IsTestingPlatformApplication set to false, then we will not run any of the test apps
                {
                    _areTestingPlatformApplications = false;
                    return;
                }
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

        private async Task<(IEnumerable<Module>, bool Restored)> GetProjectsProperties(string solutionOrProjectFilePath, bool isSolution)
        {
            var allProjects = new ConcurrentBag<Module>();
            bool restored = true;

            if (isSolution)
            {
                var projects = await SolutionAndProjectUtility.ParseSolution(solutionOrProjectFilePath);
                ProcessProjectsInParallel(projects, allProjects, ref restored);
            }
            else
            {
                bool allowBinLog = IsBinaryLoggerEnabled(_args, out string binLogFileName);

                var (relatedProjects, isProjectBuilt) = GetProjectPropertiesInternal(solutionOrProjectFilePath, allowBinLog, binLogFileName);
                foreach (var relatedProject in relatedProjects)
                {
                    allProjects.Add(relatedProject);
                }

                if (!isProjectBuilt)
                {
                    restored = false;
                }
            }
            return (allProjects, restored);
        }

        private void ProcessProjectsInParallel(IEnumerable<string> projects, ConcurrentBag<Module> allProjects, ref bool restored)
        {
            bool allProjectsRestored = true;
            bool allowBinLog = IsBinaryLoggerEnabled(_args, out string binLogFileName);

            Parallel.ForEach(
                projects,
                new ParallelOptions { MaxDegreeOfParallelism = _degreeOfParallelism },
                () => true,
                (project, state, localRestored) =>
                {
                    var (relatedProjects, isRestored) = GetProjectPropertiesInternal(project, allowBinLog, binLogFileName);
                    foreach (var relatedProject in relatedProjects)
                    {
                        allProjects.Add(relatedProject);
                    }

                    return localRestored && isRestored;
                },
                localRestored =>
                {
                    if (!localRestored)
                    {
                        allProjectsRestored = false;
                    }
                });

            restored = allProjectsRestored;
        }

        private static (IEnumerable<Module> Modules, bool Restored) GetProjectPropertiesInternal(string projectFilePath, bool allowBinLog, string binLogFileName)
        {
            var projectCollection = new ProjectCollection();
            var project = projectCollection.LoadProject(projectFilePath);
            var buildResult = RestoreProject(projectFilePath, projectCollection, allowBinLog, binLogFileName);

            bool restored = buildResult.OverallResult == BuildResultCode.Success;

            if (!restored)
            {
                return (Array.Empty<Module>(), restored);
            }

            return (ExtractModulesFromProject(project), restored);
        }

        private static IEnumerable<Module> ExtractModulesFromProject(Project project)
        {
            _ = bool.TryParse(project.GetPropertyValue(ProjectProperties.IsTestingPlatformApplication), out bool isTestingPlatformApplication);
            _ = bool.TryParse(project.GetPropertyValue(ProjectProperties.IsTestProject), out bool isTestProject);

            string targetFramework = project.GetPropertyValue(ProjectProperties.TargetFramework);
            string targetFrameworks = project.GetPropertyValue(ProjectProperties.TargetFrameworks);
            string targetPath = project.GetPropertyValue(ProjectProperties.TargetPath);
            string projectFullPath = project.GetPropertyValue(ProjectProperties.ProjectFullPath);
            string runSettingsFilePath = project.GetPropertyValue(ProjectProperties.RunSettingsFilePath);

            var projects = new List<Module>();

            if (string.IsNullOrEmpty(targetFrameworks))
            {
                projects.Add(new Module(targetPath, projectFullPath, targetFramework, runSettingsFilePath, isTestingPlatformApplication, isTestProject));
            }
            else
            {
                var frameworks = targetFrameworks.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
                foreach (var framework in frameworks)
                {
                    project.SetProperty(ProjectProperties.TargetFramework, framework);
                    project.ReevaluateIfNecessary();

                    projects.Add(new Module(project.GetPropertyValue(ProjectProperties.TargetPath),
                        projectFullPath,
                        framework,
                        runSettingsFilePath,
                        isTestingPlatformApplication,
                        isTestProject));
                }
            }

            return projects;
        }

        private static BuildResult RestoreProject(string projectFilePath, ProjectCollection projectCollection, bool allowBinLog, string binLogFileName)
        {
            BuildParameters parameters = new(projectCollection)
            {
                Loggers = [new ConsoleLogger(LoggerVerbosity.Quiet)]
            };

            if (allowBinLog)
            {
                parameters.Loggers = parameters.Loggers.Concat([
                    new BinaryLogger
                    {
                        Parameters = binLogFileName
                    }
                ]);
            }

            var buildRequestData = new BuildRequestData(projectFilePath, new Dictionary<string, string>(), null, [CliConstants.RestoreCommand], null);
            BuildResult buildResult;
            lock (buildLock)
            {
                buildResult = BuildManager.DefaultBuildManager.Build(parameters, buildRequestData);
            }

            return buildResult;
        }

        private static bool IsBinaryLoggerEnabled(List<string> args, out string binLogFileName)
        {
            binLogFileName = BinLogFileName;

            var binLogArgs = new List<string>();
            var otherArgs = new List<string>();

            foreach (var arg in args)
            {
                if (arg.StartsWith("/bl:") || arg.Equals("/bl")
                    || arg.StartsWith("--binaryLogger:") || arg.Equals("--binaryLogger")
                    || arg.StartsWith("-bl:") || arg.Equals("-bl"))
                {
                    binLogArgs.Add(arg);

                }
                else
                {
                    otherArgs.Add(arg);
                }
            }

            if (binLogArgs.Count > 0)
            {
                // Remove all BinLog args from the list of args
                args.RemoveAll(arg => binLogArgs.Contains(arg));

                // Get BinLog filename
                var binLogArg = binLogArgs.LastOrDefault();

                if (binLogArg.Contains(':'))
                {
                    binLogFileName = binLogArg.Split(':')[1];
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
