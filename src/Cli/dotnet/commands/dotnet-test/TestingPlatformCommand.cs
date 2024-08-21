// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.CommandLine;
using Microsoft.DotNet.Cli.commands.dotnet_test;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.DotNet.Cli
{
    internal partial class TestingPlatformCommand : CliCommand, ICustomHelp
    {
        private readonly ConcurrentDictionary<string, TestApplication> _testApplications = [];
        private readonly CancellationTokenSource _cancellationToken = new();

        private MSBuildConnectionHandler _msBuildHelper;
        private TestApplicationActionQueue _actionQueue;
        private Task _namedPipeConnectionLoop;
        private string[] _args;

        public TestingPlatformCommand(string name, string description = null) : base(name, description)
        {
            TreatUnmatchedTokensAsErrors = false;
        }

        public int Run(ParseResult parseResult)
        {
            // User can decide what the degree of parallelism should be
            // If not specified, we will default to the number of processors
            if (!int.TryParse(parseResult.GetValue(TestCommandParser.MaxParallelTestModules), out int degreeOfParallelism))
                degreeOfParallelism = Environment.ProcessorCount;

            if (ContainsHelpOption(parseResult.GetArguments()))
            {
                _actionQueue = new(degreeOfParallelism, async (TestApplication testApp) =>
                {
                    testApp.HelpRequested += OnHelpRequested;
                    testApp.ErrorReceived += OnErrorReceived;
                    testApp.TestProcessExited += OnTestProcessExited;
                    testApp.Created += OnTestApplicationCreated;
                    testApp.ExecutionIdReceived += OnExecutionIdReceived;

                    return await testApp.RunAsync(enableHelp: true);
                });
            }
            else
            {
                _actionQueue = new(degreeOfParallelism, async (TestApplication testApp) =>
                {
                    testApp.HandshakeInfoReceived += OnHandshakeInfoReceived;
                    testApp.SuccessfulTestResultReceived += OnTestResultReceived;
                    testApp.FailedTestResultReceived += OnTestResultReceived;
                    testApp.FileArtifactInfoReceived += OnFileArtifactInfoReceived;
                    testApp.SessionEventReceived += OnSessionEventReceived;
                    testApp.ErrorReceived += OnErrorReceived;
                    testApp.TestProcessExited += OnTestProcessExited;
                    testApp.Created += OnTestApplicationCreated;
                    testApp.ExecutionIdReceived += OnExecutionIdReceived;

                    return await testApp.RunAsync(enableHelp: false);
                });
            }

            _args = [.. parseResult.UnmatchedTokens];
            _msBuildHelper = new(_args, _actionQueue);
            _namedPipeConnectionLoop = Task.Run(async () => await _msBuildHelper.WaitConnectionAsync(_cancellationToken.Token));

            if (parseResult.HasOption(TestCommandParser.TestModules))
            {
                if (!RunWithTestModulesFilter(parseResult))
                {
                    return ExitCodes.GenericFailure;
                }
            }
            else
            {
                // If no filter was provided, MSBuild will get the test project paths
                var msbuildResult = _msBuildHelper.RunWithMSBuild(parseResult);
                if (msbuildResult != 0)
                {
                    VSTestTrace.SafeWriteTrace(() => $"MSBuild task _GetTestsProject didn't execute properly with exit code: {msbuildResult}.");
                    return ExitCodes.GenericFailure;
                }
            }

            _actionQueue.EnqueueCompleted();
            var hasFailed = _actionQueue.WaitAllActions();

            // Above line will block till we have all connections and all GetTestsProject msbuild task complete.
            _cancellationToken.Cancel();
            _namedPipeConnectionLoop.Wait();

            // Clean up everything
            CleanUp();

            return hasFailed ? ExitCodes.GenericFailure : ExitCodes.Success;
        }

        private void CleanUp()
        {
            _msBuildHelper.Dispose();
            foreach (var testApplication in _testApplications.Values)
            {
                testApplication.Dispose();
            }
        }

        private bool RunWithTestModulesFilter(ParseResult parseResult)
        {
            // If the module path pattern(s) was provided, we will use that to filter the test modules
            string testModules = parseResult.GetValue(TestCommandParser.TestModules);

            // If the root directory was provided, we will use that to search for the test modules
            // Otherwise, we will use the current directory
            string rootDirectory = Directory.GetCurrentDirectory();
            if (parseResult.HasOption(TestCommandParser.TestModulesRootDirectory))
            {
                rootDirectory = parseResult.GetValue(TestCommandParser.TestModulesRootDirectory);

                // If the root directory is not valid, we simply return
                if (string.IsNullOrEmpty(rootDirectory) || !Directory.Exists(rootDirectory))
                {
                    VSTestTrace.SafeWriteTrace(() => $"The provided root directory does not exist: {rootDirectory}");
                    return false;
                }
            }

            var testModulePaths = GetMatchedModulePaths(testModules, rootDirectory);

            // If no matches were found, we simply return
            if (!testModulePaths.Any())
            {
                VSTestTrace.SafeWriteTrace(() => $"No test modules found for the given test module pattern: {testModules} with root directory: {rootDirectory}");
                return false;
            }

            foreach (string testModule in testModulePaths)
            {
                var testApp = new TestApplication(testModule, _args);
                // Write the test application to the channel
                _actionQueue.Enqueue(testApp);
                testApp.OnCreated();
            }

            return true;
        }

        private static IEnumerable<string> GetMatchedModulePaths(string testModules, string rootDirectory)
        {
            var testModulePatterns = testModules.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            Matcher matcher = new();
            matcher.AddIncludePatterns(testModulePatterns);

            return MatcherExtensions.GetResultsInFullPath(matcher, rootDirectory);
        }

        private void OnHandshakeInfoReceived(object sender, HandshakeInfoArgs args)
        {
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            var handshakeInfo = args.handshakeInfo;

            foreach (var property in handshakeInfo.Properties)
            {
                VSTestTrace.SafeWriteTrace(() => $"{property.Key}: {property.Value}");
            }
        }

        private void OnTestResultReceived(object sender, EventArgs args)
        {
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            if (args is SuccessfulTestResultEventArgs successfulTestResultEventArgs)
            {
                var successfulTestResultMessage = successfulTestResultEventArgs.SuccessfulTestResultMessage;
                VSTestTrace.SafeWriteTrace(() => $"TestResultMessage: {successfulTestResultMessage.Uid}, {successfulTestResultMessage.DisplayName}, " +
                $"{successfulTestResultMessage.State}, {successfulTestResultMessage.Reason}, {successfulTestResultMessage.SessionUid}");
            }
            else if (args is FailedTestResultEventArgs failedTestResultEventArgs)
            {
                var failedTestResultMessage = failedTestResultEventArgs.FailedTestResultMessage;
                VSTestTrace.SafeWriteTrace(() => $"TestResultMessage: {failedTestResultMessage.Uid}, {failedTestResultMessage.DisplayName}, " +
                $"{failedTestResultMessage.State}, {failedTestResultMessage.Reason}, {failedTestResultMessage.ErrorMessage}," +
                $" {failedTestResultMessage.ErrorStackTrace}, {failedTestResultMessage.SessionUid}");
            }
        }

        private void OnFileArtifactInfoReceived(object sender, FileArtifactInfoEventArgs args)
        {
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            var fileArtifactInfo = args.FileArtifactInfo;
            VSTestTrace.SafeWriteTrace(() => $"FileArtifactInfo: {fileArtifactInfo.FullPath}, {fileArtifactInfo.DisplayName}, " +
                $"{fileArtifactInfo.Description}, {fileArtifactInfo.TestUid}, {fileArtifactInfo.TestDisplayName}, " +
                $"{fileArtifactInfo.SessionUid}");
        }

        private void OnSessionEventReceived(object sender, SessionEventArgs args)
        {
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            var sessionEvent = args.SessionEvent;
            VSTestTrace.SafeWriteTrace(() => $"TestSessionEvent: {sessionEvent.SessionType}, {sessionEvent.SessionUid}");
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

            if (args.ExitCode != 0)
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

        private void OnTestApplicationCreated(object sender, EventArgs args)
        {
            TestApplication testApp = sender as TestApplication;
            _testApplications[testApp.ModulePath] = testApp;
        }

        private void OnExecutionIdReceived(object sender, ExecutionEventArgs args)
        {
            if (_testApplications.TryGetValue(args.ModulePath, out var testApp))
            {
                testApp.AddExecutionId(args.ExecutionId);
            }
        }

        private static bool ContainsHelpOption(IEnumerable<string> args) => args.Contains(CliConstants.HelpOptionKey) || args.Contains(CliConstants.HelpOptionKey.Substring(0, 2));
    }
}
