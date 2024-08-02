// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.CommandLine;
using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.Testing.TestInfrastructure;

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
            DebuggerUtility.AttachCurrentProcessToVSProcessPID(12016);
            _args = [.. parseResult.UnmatchedTokens];

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

                    int runHelpResult = await testApp.RunHelpAsync();
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

                    int runResult = await testApp.RunAsync();
                });
            }

            VSTestTrace.SafeWriteTrace(() => $"Wait for connection(s) on pipe = {_pipeNameDescription.Name}");
            _namedPipeConnectionLoop = Task.Run(async () => await WaitConnectionAsync(_cancellationToken.Token));

            if (parseResult.HasOption(TestCommandParser.TestModules))
            {
                // If the module path pattern(s) was provided, we will use that to filter the test modules
                string testModules = parseResult.GetValue(TestCommandParser.TestModules);

                var testModulePaths = GetMatchedModulePaths(testModules);

                if (!testModulePaths.Any())
                {
                    VSTestTrace.SafeWriteTrace(() => $"No test modules found for the given test module pattern: {testModules}");
                    return 1;
                }

                foreach (string testModule in testModulePaths)
                {
                    _testApplications[testModule] = new TestApplication(testModule, _pipeNameDescription.Name, _args);
                    _actionQueue.Enqueue(_testApplications[testModule]);
                }
            }
            else
            {
                // If no filter was provided, MSBuild will get the test project paths
                var result = RunMSBuildTask(parseResult);
                if (result != 0)
                {
                    VSTestTrace.SafeWriteTrace(() => $"MSBuild task _GetTestsProject didn't execute properly.");
                    return result;
                }
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

        private IEnumerable<string> GetMatchedModulePaths(string testModules)
        {
            var testModulePatterns = testModules.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            Matcher matcher = new();
            matcher.AddIncludePatterns(testModulePatterns);

            string searchDirectory = Directory.GetCurrentDirectory();

            PatternMatchingResult result = matcher.Execute(
                new DirectoryInfoWrapper(
                    new DirectoryInfo(searchDirectory)));

            return result.Files.Select(file => $"{searchDirectory}\\{file.Path.Replace("/", "\\")}");
        }

        private int RunMSBuildTask(ParseResult parseResult)
        {
            bool containsNoBuild = parseResult.UnmatchedTokens.Any(token => token == CliConstants.NoBuildOptionKey);

            ForwardingAppImplementation msBuildForwardingApp = new(
                GetMSBuildExePath(),
                [$"-t:{(containsNoBuild ? string.Empty : "Build;")}_GetTestsProject",
                    $"-p:GetTestsProjectPipeName={_pipeNameDescription.Name}",
                    "-verbosity:q"]);
            return msBuildForwardingApp.Execute();
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
                if (TryGetModulePath(request, out string modulePath))
                {
                    _testApplications[modulePath] = new TestApplication(modulePath, _pipeNameDescription.Name, _args);
                    // Write the test application to the channel
                    _actionQueue.Enqueue(_testApplications[modulePath]);

                    return Task.FromResult((IResponse)VoidResponse.CachedInstance);
                }

                if (TryGetHandshakeInfo(request, out HandshakeInfo handshakeInfo))
                {
                    if (handshakeInfo.Properties.TryGetValue(HandshakeInfoPropertyNames.ModulePath, out string value))
                    {
                        var testApplication = _testApplications[value];
                        Debug.Assert(testApplication is not null);
                        testApplication.OnHandshakeInfo(handshakeInfo);

                        return Task.FromResult((IResponse)CreateHandshakeInfo());
                    }
                }

                if (TryGetHelpResponse(request, out CommandLineOptionMessages commandLineOptionMessages))
                {
                    var testApplication = _testApplications[commandLineOptionMessages.ModulePath];
                    Debug.Assert(testApplication is not null);
                    testApplication.OnCommandLineOptionMessages(commandLineOptionMessages);

                    return Task.FromResult((IResponse)VoidResponse.CachedInstance);
                }

                if (TryGetSuccessfulTestResultMessage(request, out SuccessfulTestResultMessage successfulTestResultMessage))
                {
                    var testApplication = _testApplications[successfulTestResultMessage.ModulePath];
                    Debug.Assert(testApplication is not null);

                    testApplication.OnSuccessfulTestResultMessage(successfulTestResultMessage);
                    return Task.FromResult((IResponse)VoidResponse.CachedInstance);
                }

                if (TryGetFailedTestResultMessage(request, out FailedTestResultMessage failedTestResultMessage))
                {
                    var testApplication = _testApplications[failedTestResultMessage.ModulePath];
                    Debug.Assert(testApplication is not null);

                    testApplication.OnFailedTestResultMessage(failedTestResultMessage);
                    return Task.FromResult((IResponse)VoidResponse.CachedInstance);
                }

                if (TryGetFileArtifactInfo(request, out FileArtifactInfo fileArtifactInfo))
                {
                    var testApplication = _testApplications[fileArtifactInfo.ModulePath];
                    Debug.Assert(testApplication is not null);
                    testApplication.OnFileArtifactInfo(fileArtifactInfo);
                    return Task.FromResult((IResponse)VoidResponse.CachedInstance);
                }

                if (TryGetSessionEvent(request, out TestSessionEvent sessionEvent))
                {
                    var testApplication = _testApplications[sessionEvent.ModulePath];
                    Debug.Assert(testApplication is not null);
                    testApplication.OnSessionEvent(sessionEvent);
                    return Task.FromResult((IResponse)VoidResponse.CachedInstance);
                }

                // If we don't recognize the message, log and skip it
                if (TryGetUnknownMessage(request, out UnknownMessage unknownMessage))
                {
                    if (VSTestTrace.TraceEnabled)
                    {
                        VSTestTrace.SafeWriteTrace(() => $"Request '{request.GetType()}' with Serializer ID = {unknownMessage.SerializerId} is unsupported.");
                    }
                    return Task.FromResult((IResponse)VoidResponse.CachedInstance);
                }

                // If it doesn't match any of the above, throw an exception
                throw new NotSupportedException($"Request '{request.GetType()}' is unsupported.");
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

        private static bool TryGetModulePath(IRequest request, out string modulePath)
        {
            if (request is Module module)
            {
                modulePath = module.DLLPath;
                return true;
            }

            modulePath = null;
            return false;
        }

        private static bool TryGetHandshakeInfo(IRequest request, out HandshakeInfo handshakeInfo)
        {
            if (request is HandshakeInfo result)
            {
                handshakeInfo = result;
                return true;
            }

            handshakeInfo = null;
            return false;
        }

        private static bool TryGetHelpResponse(IRequest request, out CommandLineOptionMessages commandLineOptionMessages)
        {
            if (request is CommandLineOptionMessages result)
            {
                commandLineOptionMessages = result;
                return true;
            }

            commandLineOptionMessages = null;
            return false;
        }

        public static bool TryGetSuccessfulTestResultMessage(IRequest response, out SuccessfulTestResultMessage testResultMessage)
        {
            if (response is SuccessfulTestResultMessage result)
            {
                testResultMessage = result;
                return true;
            }
            testResultMessage = null;
            return false;
        }

        public static bool TryGetFailedTestResultMessage(IRequest response, out FailedTestResultMessage testResultMessage)
        {
            if (response is FailedTestResultMessage result)
            {
                testResultMessage = result;
                return true;
            }
            testResultMessage = null;
            return false;
        }

        private bool TryGetFileArtifactInfo(IRequest request, out FileArtifactInfo fileArtifactInfo)
        {
            if (request is FileArtifactInfo result)
            {
                fileArtifactInfo = result;
                return true;
            }

            fileArtifactInfo = null;
            return false;
        }

        private bool TryGetSessionEvent(IRequest request, out TestSessionEvent sessionEvent)
        {
            if (request is TestSessionEvent result)
            {
                sessionEvent = result;
                return true;
            }

            sessionEvent = null;
            return false;
        }

        private static bool TryGetUnknownMessage(IRequest request, out UnknownMessage unknownMessage)
        {
            if (request is UnknownMessage result)
            {
                unknownMessage = result;
                return true;
            }

            unknownMessage = null;
            return false;
        }

        private void OnHandshakeInfoReceived(object sender, HandshakeInfoArgs args)
        {
            if (VSTestTrace.TraceEnabled)
            {
                var handshakeInfo = args.handshakeInfo;

                foreach (var property in handshakeInfo.Properties)
                {
                    VSTestTrace.SafeWriteTrace(() => $"{property.Key}: {property.Value}");
                }
            }
        }
        private void OnTestResultReceived(object sender, EventArgs args)
        {
            if (VSTestTrace.TraceEnabled)
            {
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
        }

        private void OnFileArtifactInfoReceived(object sender, FileArtifactInfoEventArgs args)
        {
            if (VSTestTrace.TraceEnabled)
            {
                var fileArtifactInfo = args.FileArtifactInfo;
                VSTestTrace.SafeWriteTrace(() => $"FileArtifactInfo: {fileArtifactInfo.FullPath}, {fileArtifactInfo.DisplayName}, " +
                    $"{fileArtifactInfo.Description}, {fileArtifactInfo.TestUid}, {fileArtifactInfo.TestDisplayName}, " +
                    $"{fileArtifactInfo.SessionUid}, {fileArtifactInfo.ModulePath}");
            }
        }

        private void OnSessionEventReceived(object sender, SessionEventArgs args)
        {
            if (VSTestTrace.TraceEnabled)
            {
                var sessionEvent = args.SessionEvent;
                VSTestTrace.SafeWriteTrace(() => $"TestSessionEvent: {sessionEvent.SessionType}, {sessionEvent.SessionUid}, {sessionEvent.ModulePath}");
            }
        }

        private void OnErrorReceived(object sender, ErrorEventArgs args)
        {
            if (VSTestTrace.TraceEnabled)
            {
                VSTestTrace.SafeWriteTrace(() => args.ErrorMessage);
            }
        }

        private void OnTestProcessExited(object sender, TestProcessExitEventArgs args)
        {
            if (VSTestTrace.TraceEnabled)
            {
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
