// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.CommandLine;
using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.DotNet.Cli
{
    internal partial class TestingPlatformCommand : CliCommand, ICustomHelp
    {
        private readonly List<NamedPipeServer> _namedPipeServers = new();
        private readonly List<Task> _taskModuleName = [];
        private readonly ConcurrentBag<Task> _testsRun = [];
        private readonly ConcurrentDictionary<string, CommandLineOptionMessage> _commandLineOptionNameToModuleNames = [];
        private readonly ConcurrentDictionary<bool, List<(string, string[])>> _moduleNamesToCommandLineOptions = [];
        private readonly ConcurrentDictionary<string, TestApplication> _testApplications = [];
        private readonly PipeNameDescription _pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
        private readonly CancellationTokenSource _cancellationToken = new();
        private TestApplicationActionQueue _actionQueue;

        private Task _namedPipeConnectionLoop;
        private string[] _args;

        public TestingPlatformCommand(string name, string description = null) : base(name, description)
        {
            TreatUnmatchedTokensAsErrors = false;
        }

        public int Run(ParseResult parseResult)
        {
            _args = parseResult.GetArguments();

            // User can decide what the degree of parallelism should be
            // If not specified, we will default to the number of processors
            if (!int.TryParse(parseResult.GetValue(TestCommandParser.MaxParallelTestModules), out int degreeOfParallelism))
                degreeOfParallelism = Environment.ProcessorCount;

            if (ContainsHelpOption(_args))
            {
                _actionQueue = new(degreeOfParallelism, async (TestApplication testApp) =>
                {
                    testApp.HelpRequested += OnHelpRequested;
                    testApp.ErrorReceived += OnErrorReceived;
                    testApp.TestProcessExited += OnTestProcessExited;

                    int runHelpResult = await testApp.RunAsync(enableHelp: true);
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

                    int runResult = await testApp.RunAsync(enableHelp: false);
                });
            }

            VSTestTrace.SafeWriteTrace(() => $"Wait for connection(s) on pipe = {_pipeNameDescription.Name}");
            _namedPipeConnectionLoop = Task.Run(async () => await WaitConnectionAsync(_cancellationToken.Token));

            bool containsNoBuild = parseResult.UnmatchedTokens.Any(token => token == CliConstants.NoBuildOptionKey);
            List<string> msbuildCommandlineArgs = [$"-t:{(containsNoBuild ? string.Empty : "Build;")}_GetTestsProject",
                 $"-p:GetTestsProjectPipeName={_pipeNameDescription.Name}",
                    "-verbosity:q"];

            AddAdditionalMSBuildParameters(parseResult, msbuildCommandlineArgs);

            if (VSTestTrace.TraceEnabled)
            {
                VSTestTrace.SafeWriteTrace(() => $"MSBuild command line arguments: {string.Join(" ", msbuildCommandlineArgs)}");
            }

            ForwardingAppImplementation msBuildForwardingApp = new(GetMSBuildExePath(), msbuildCommandlineArgs);
            int testsProjectResult = msBuildForwardingApp.Execute();

            if (testsProjectResult != 0)
            {
                VSTestTrace.SafeWriteTrace(() => $"MSBuild task _GetTestsProject didn't execute properly.");
                return testsProjectResult;
            }

            _actionQueue.EnqueueCompleted();

            _actionQueue.WaitAllActions();

            // Above line will block till we have all connections and all GetTestsProject msbuild task complete.
            _cancellationToken.Cancel();
            _namedPipeConnectionLoop.Wait();

            return 0;
        }

        private static void AddAdditionalMSBuildParameters(ParseResult parseResult, List<string> parameters)
        {
            string msBuildParameters = parseResult.GetValue(TestCommandParser.AdditionalMSBuildParameters);
            parameters.AddRange(!string.IsNullOrEmpty(msBuildParameters) ? msBuildParameters.Split(" ", StringSplitOptions.RemoveEmptyEntries) : []);
        }

        private async Task WaitConnectionAsync(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    NamedPipeServer namedPipeServer = new(_pipeNameDescription, OnRequest, NamedPipeServerStream.MaxAllowedServerInstances, token, skipUnknownMessages: true);
                    namedPipeServer.RegisterAllSerializers();

                    await namedPipeServer.WaitConnectionAsync(token);

                    _namedPipeServers.Add(namedPipeServer);
                }
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == token)
            {
                // We are exiting
            }
            catch (Exception ex)
            {
                if (VSTestTrace.TraceEnabled)
                {
                    VSTestTrace.SafeWriteTrace(() => ex.ToString());
                }

                Environment.FailFast(ex.ToString());
            }
        }

        private Task<IResponse> OnRequest(IRequest request)
        {
            try
            {
                switch (request)
                {
                    case Module module:
                        string modulePath = module.DLLPath;
                        _testApplications[modulePath] = new TestApplication(modulePath, _pipeNameDescription.Name, _args);
                        // Write the test application to the channel
                        _actionQueue.Enqueue(_testApplications[modulePath]);
                        break;

                    case HandshakeInfo handshakeInfo:
                        if (handshakeInfo.Properties.TryGetValue(HandshakeInfoPropertyNames.ModulePath, out string value))
                        {
                            var testApp = _testApplications[value];
                            Debug.Assert(testApp is not null);
                            testApp.OnHandshakeInfo(handshakeInfo);

                            return Task.FromResult((IResponse)CreateHandshakeInfo());
                        }
                        break;

                    case CommandLineOptionMessages commandLineOptionMessages:
                        var testApplication = _testApplications[commandLineOptionMessages.ModulePath];
                        Debug.Assert(testApplication is not null);
                        testApplication.OnCommandLineOptionMessages(commandLineOptionMessages);
                        break;

                    case SuccessfulTestResultMessage successfulTestResultMessage:
                        testApplication = _testApplications[successfulTestResultMessage.ModulePath];
                        Debug.Assert(testApplication is not null);

                        testApplication.OnSuccessfulTestResultMessage(successfulTestResultMessage);
                        break;

                    case FailedTestResultMessage failedTestResultMessage:
                        testApplication = _testApplications[failedTestResultMessage.ModulePath];
                        Debug.Assert(testApplication is not null);

                        testApplication.OnFailedTestResultMessage(failedTestResultMessage);
                        break;

                    case FileArtifactInfo fileArtifactInfo:
                        testApplication = _testApplications[fileArtifactInfo.ModulePath];
                        Debug.Assert(testApplication is not null);
                        testApplication.OnFileArtifactInfo(fileArtifactInfo);
                        break;

                    case TestSessionEvent sessionEvent:
                        testApplication = _testApplications[sessionEvent.ModulePath];
                        Debug.Assert(testApplication is not null);
                        testApplication.OnSessionEvent(sessionEvent);
                        break;

                    // If we don't recognize the message, log and skip it
                    case UnknownMessage unknownMessage:
                        if (VSTestTrace.TraceEnabled)
                        {
                            VSTestTrace.SafeWriteTrace(() => $"Request '{request.GetType()}' with Serializer ID = {unknownMessage.SerializerId} is unsupported.");
                        }
                        return Task.FromResult((IResponse)VoidResponse.CachedInstance);
                    default:
                        // If it doesn't match any of the above, throw an exception
                        throw new NotSupportedException($"Request '{request.GetType()}' is unsupported.");
                }
            }
            catch (Exception ex)
            {
                if (VSTestTrace.TraceEnabled)
                {
                    VSTestTrace.SafeWriteTrace(() => ex.ToString());
                }

                Environment.FailFast(ex.ToString());
            }

            return Task.FromResult((IResponse)VoidResponse.CachedInstance);
        }

        private static HandshakeInfo CreateHandshakeInfo() =>
            new(new Dictionary<string, string>
            {
                { HandshakeInfoPropertyNames.PID, Process.GetCurrentProcess().Id.ToString() },
                { HandshakeInfoPropertyNames.Architecture, RuntimeInformation.OSArchitecture.ToString() },
                { HandshakeInfoPropertyNames.Framework, RuntimeInformation.FrameworkDescription },
                { HandshakeInfoPropertyNames.OS, RuntimeInformation.OSDescription },
                { HandshakeInfoPropertyNames.ProtocolVersion, ProtocolConstants.Version }
            });

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
                $"{successfulTestResultMessage.State}, {successfulTestResultMessage.Reason}, {successfulTestResultMessage.SessionUid}, {successfulTestResultMessage.ModulePath}");
            }
            else if (args is FailedTestResultEventArgs failedTestResultEventArgs)
            {
                var failedTestResultMessage = failedTestResultEventArgs.FailedTestResultMessage;
                VSTestTrace.SafeWriteTrace(() => $"TestResultMessage: {failedTestResultMessage.Uid}, {failedTestResultMessage.DisplayName}, " +
                $"{failedTestResultMessage.State}, {failedTestResultMessage.Reason}, {failedTestResultMessage.ErrorMessage}," +
                $" {failedTestResultMessage.ErrorStackTrace}, {failedTestResultMessage.SessionUid}, {failedTestResultMessage.ModulePath}");
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
                $"{fileArtifactInfo.SessionUid}, {fileArtifactInfo.ModulePath}");
        }

        private void OnSessionEventReceived(object sender, SessionEventArgs args)
        {
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            var sessionEvent = args.SessionEvent;
            VSTestTrace.SafeWriteTrace(() => $"TestSessionEvent: {sessionEvent.SessionType}, {sessionEvent.SessionUid}, {sessionEvent.ModulePath}");
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

        private static bool ContainsHelpOption(IEnumerable<string> args) => args.Contains(CliConstants.HelpOptionKey) || args.Contains(CliConstants.HelpOptionKey.Substring(0, 2));

        private static string GetMSBuildExePath()
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                CliConstants.MSBuildExeName);
        }
    }
}
