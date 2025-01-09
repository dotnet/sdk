// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.CommandLine;
using Microsoft.DotNet.Tools.Test;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.DotNet.Cli
{
    internal partial class TestingPlatformCommand : CliCommand, ICustomHelp
    {
        private readonly ConcurrentBag<TestApplication> _testApplications = [];

        private MSBuildHandler _msBuildHandler;
        private TestModulesFilterHandler _testModulesFilterHandler;
        private TestApplicationActionQueue _actionQueue;
        private List<string> _args;

        public TestingPlatformCommand(string name, string description = null) : base(name, description)
        {
            TreatUnmatchedTokensAsErrors = false;
        }

        public async Task<int> Run(ParseResult parseResult)
        {
            bool hasFailed = false;
            try
            {
                // User can decide what the degree of parallelism should be
                // If not specified, we will default to the number of processors
                if (!int.TryParse(parseResult.GetValue(TestingPlatformOptions.MaxParallelTestModulesOption), out int degreeOfParallelism))
                    degreeOfParallelism = Environment.ProcessorCount;

                bool filterModeEnabled = parseResult.HasOption(TestingPlatformOptions.TestModulesFilterOption);

                if (filterModeEnabled && parseResult.HasOption(TestingPlatformOptions.ArchitectureOption))
                {
                    VSTestTrace.SafeWriteTrace(() => $"The --arch option is not supported yet.");
                }

                BuiltInOptions builtInOptions = new(
                    parseResult.HasOption(TestingPlatformOptions.NoRestoreOption),
                    parseResult.HasOption(TestingPlatformOptions.NoBuildOption),
                    parseResult.GetValue(TestingPlatformOptions.ConfigurationOption),
                    parseResult.GetValue(TestingPlatformOptions.ArchitectureOption));

                if (ContainsHelpOption(parseResult.GetArguments()))
                {
                    _actionQueue = new(degreeOfParallelism, async (TestApplication testApp) =>
                    {
                        testApp.HelpRequested += OnHelpRequested;
                        testApp.ErrorReceived += OnErrorReceived;
                        testApp.TestProcessExited += OnTestProcessExited;
                        testApp.Run += OnTestApplicationRun;
                        testApp.ExecutionIdReceived += OnExecutionIdReceived;

                        return await testApp.RunAsync(filterModeEnabled, enableHelp: true, builtInOptions);
                    });
                }
                else
                {
                    _actionQueue = new(degreeOfParallelism, async (TestApplication testApp) =>
                    {
                        testApp.HandshakeReceived += OnHandshakeReceived;
                        testApp.DiscoveredTestsReceived += OnDiscoveredTestsReceived;
                        testApp.TestResultsReceived += OnTestResultsReceived;
                        testApp.FileArtifactsReceived += OnFileArtifactsReceived;
                        testApp.SessionEventReceived += OnSessionEventReceived;
                        testApp.ErrorReceived += OnErrorReceived;
                        testApp.TestProcessExited += OnTestProcessExited;
                        testApp.Run += OnTestApplicationRun;
                        testApp.ExecutionIdReceived += OnExecutionIdReceived;

                        return await testApp.RunAsync(filterModeEnabled, enableHelp: false, builtInOptions);
                    });
                }

                _args = [.. parseResult.UnmatchedTokens];
                _msBuildHandler = new(_args, _actionQueue, degreeOfParallelism);
                _testModulesFilterHandler = new(_args, _actionQueue);

                if (parseResult.HasOption(TestingPlatformOptions.TestModulesFilterOption))
                {
                    if (!_testModulesFilterHandler.RunWithTestModulesFilter(parseResult))
                    {
                        return ExitCodes.GenericFailure;
                    }
                }
                else
                {
                    if (!await RunMSBuild(parseResult))
                    {
                        return ExitCodes.GenericFailure;
                    }

                    // If not all test projects have IsTestProject and IsTestingPlatformApplication properties set to true, we will simply return
                    if (!_msBuildHandler.EnqueueTestApplications())
                    {
                        VSTestTrace.SafeWriteTrace(() => LocalizableStrings.CmdUnsupportedVSTestTestApplicationsDescription);
                        return ExitCodes.GenericFailure;
                    }
                }

                _actionQueue.EnqueueCompleted();
                hasFailed = _actionQueue.WaitAllActions();
                // Above line will block till we have all connections and all GetTestsProject msbuild task complete.
            }
            finally
            {
                // Clean up everything
                CleanUp();
            }

            return hasFailed ? ExitCodes.GenericFailure : ExitCodes.Success;
        }

        private async Task<bool> RunMSBuild(ParseResult parseResult)
        {
            int msbuildExitCode;

            if (parseResult.HasOption(TestingPlatformOptions.ProjectOption))
            {
                string filePath = parseResult.GetValue(TestingPlatformOptions.ProjectOption);
                string[] extensions = [".proj", ".csproj", ".vbproj", ".fsproj"];

                if (!extensions.Contains(Path.GetExtension(filePath)))
                {
                    VSTestTrace.SafeWriteTrace(() => string.Format(LocalizableStrings.CmdInvalidProjectFileExtensionDescription, filePath));
                    return false;
                }

                if (!File.Exists(filePath))
                {
                    VSTestTrace.SafeWriteTrace(() => string.Format(LocalizableStrings.CmdNonExistentProjectFilePathDescription, filePath));
                    return false;
                }

                msbuildExitCode = await _msBuildHandler.RunWithMSBuild(filePath, isSolution: false);
            }
            else if (parseResult.HasOption(TestingPlatformOptions.SolutionOption))
            {
                string filePath = parseResult.GetValue(TestingPlatformOptions.SolutionOption);
                string[] extensions = [".sln", ".slnx"];

                if (!extensions.Contains(Path.GetExtension(filePath)))
                {
                    VSTestTrace.SafeWriteTrace(() => string.Format(LocalizableStrings.CmdInvalidSolutionFileExtensionDescription, filePath));
                    return false;
                }

                if (!File.Exists(filePath))
                {
                    VSTestTrace.SafeWriteTrace(() => string.Format(LocalizableStrings.CmdNonExistentSolutionFilePathDescription, filePath));
                    return false;
                }

                msbuildExitCode = await _msBuildHandler.RunWithMSBuild(filePath, isSolution: true);
            }
            else
            {
                // If no filter was provided neither the project using --project,
                // MSBuild will get the test project paths in the current directory
                msbuildExitCode = await _msBuildHandler.RunWithMSBuild();
            }

            if (msbuildExitCode != ExitCodes.Success)
            {
                VSTestTrace.SafeWriteTrace(() => string.Format(LocalizableStrings.CmdMSBuildProjectsPropertiesErrorMessage, msbuildExitCode));
                return false;
            }

            return true;
        }

        private void CleanUp()
        {
            _msBuildHandler.Dispose();
            foreach (var testApplication in _testApplications)
            {
                testApplication.Dispose();
            }
        }

        private void OnHandshakeReceived(object sender, HandshakeArgs args)
        {
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            var handshake = args.Handshake;

            foreach (var property in handshake.Properties)
            {
                VSTestTrace.SafeWriteTrace(() => $"{property.Key}: {property.Value}");
            }
        }

        private void OnDiscoveredTestsReceived(object sender, DiscoveredTestEventArgs args)
        {
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            var discoveredTestMessages = args.DiscoveredTests;

            VSTestTrace.SafeWriteTrace(() => $"DiscoveredTests Execution Id: {args.ExecutionId}");
            foreach (DiscoveredTest discoveredTestMessage in discoveredTestMessages)
            {
                VSTestTrace.SafeWriteTrace(() => $"DiscoveredTest: {discoveredTestMessage.Uid}, {discoveredTestMessage.DisplayName}");
            }
        }

        private void OnTestResultsReceived(object sender, TestResultEventArgs args)
        {
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            VSTestTrace.SafeWriteTrace(() => $"TestResults Execution Id: {args.ExecutionId}");

            foreach (SuccessfulTestResult successfulTestResult in args.SuccessfulTestResults)
            {
                VSTestTrace.SafeWriteTrace(() => $"SuccessfulTestResult: {successfulTestResult.Uid}, {successfulTestResult.DisplayName}, " +
                $"{successfulTestResult.State}, {successfulTestResult.Duration}, {successfulTestResult.Reason}, {successfulTestResult.StandardOutput}," +
                $"{successfulTestResult.ErrorOutput}, {successfulTestResult.SessionUid}");
            }

            foreach (FailedTestResult failedTestResult in args.FailedTestResults)
            {
                VSTestTrace.SafeWriteTrace(() => $"FailedTestResult: {failedTestResult.Uid}, {failedTestResult.DisplayName}, " +
                $"{failedTestResult.State}, {failedTestResult.Duration}, {failedTestResult.Reason}, {failedTestResult.ErrorMessage}," +
                $"{failedTestResult.ErrorStackTrace}, {failedTestResult.StandardOutput}, {failedTestResult.ErrorOutput}, {failedTestResult.SessionUid}");
            }
        }

        private void OnFileArtifactsReceived(object sender, FileArtifactEventArgs args)
        {
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            VSTestTrace.SafeWriteTrace(() => $"FileArtifactMessages Execution Id: {args.ExecutionId}");

            foreach (FileArtifact fileArtifactMessage in args.FileArtifacts)
            {
                VSTestTrace.SafeWriteTrace(() => $"FileArtifact: {fileArtifactMessage.FullPath}, {fileArtifactMessage.DisplayName}, " +
                $"{fileArtifactMessage.Description}, {fileArtifactMessage.TestUid}, {fileArtifactMessage.TestDisplayName}, " +
                $"{fileArtifactMessage.SessionUid}");
            }
        }

        private void OnSessionEventReceived(object sender, SessionEventArgs args)
        {
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            var sessionEvent = args.SessionEvent;
            VSTestTrace.SafeWriteTrace(() => $"TestSessionEvent: {sessionEvent.SessionType}, {sessionEvent.SessionUid}, {sessionEvent.ExecutionId}");
        }

        private void OnErrorReceived(object sender, ErrorEventArgs args)
        {
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            VSTestTrace.SafeWriteTrace(() => args.ErrorMessage);
        }

        private void OnTestProcessExited(object sender, TestProcessExitEventArgs args)
        {
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            if (args.ExitCode != ExitCodes.Success)
            {
                VSTestTrace.SafeWriteTrace(() => $"Test Process exited with non-zero exit code: {args.ExitCode}");
            }

            if (args.OutputData.Count > 0)
            {
                VSTestTrace.SafeWriteTrace(() => $"Output Data: {string.Join("\n", args.OutputData)}");
            }

            if (args.ErrorData.Count > 0)
            {
                VSTestTrace.SafeWriteTrace(() => $"Error Data: {string.Join("\n", args.ErrorData)}");
            }
        }

        private void OnTestApplicationRun(object sender, EventArgs args)
        {
            TestApplication testApp = sender as TestApplication;
            _testApplications.Add(testApp);
        }

        private void OnExecutionIdReceived(object sender, ExecutionEventArgs args)
        {
        }

        private static bool ContainsHelpOption(IEnumerable<string> args) => args.Contains(CliConstants.HelpOptionKey) || args.Contains(CliConstants.HelpOptionKey.Substring(0, 2));
    }
}
