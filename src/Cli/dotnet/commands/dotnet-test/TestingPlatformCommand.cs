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
            if (!int.TryParse(parseResult.GetValue(TestCommandParser.DegreeOfParallelism), out int degreeOfParallelism))
                degreeOfParallelism = Environment.ProcessorCount;

            if (ContainsHelpOption(_args))
            {
                _actionQueue = new(degreeOfParallelism, async (TestApplication testApp) =>
                {
                    testApp.HelpRequested += OnHelpRequested;
                    testApp.ErrorReceived += OnErrorReceived;

                    await testApp.RunHelpAsync();
                });
            }
            else
            {
                _actionQueue = new(degreeOfParallelism, async (TestApplication testApp) =>
                {
                    testApp.ErrorReceived += OnErrorReceived;

                    await testApp.RunAsync();
                });
            }

            VSTestTrace.SafeWriteTrace(() => $"Wait for connection(s) on pipe = {_pipeNameDescription.Name}");
            _namedPipeConnectionLoop = Task.Run(async () => await WaitConnectionAsync(_cancellationToken.Token));

            bool containsNoBuild = parseResult.UnmatchedTokens.Any(token => token == CliConstants.NoBuildOptionKey);

            List<string> parameters = [$"-t:{(containsNoBuild ? string.Empty : "Build;")}_GetTestsProject",
                    $"-p:GetTestsProjectPipeName={_pipeNameDescription.Name}",
                    "-verbosity:q"];

            AddAdditionalMSBuildParameters(parseResult, parameters);

            ForwardingAppImplementation msBuildForwardingApp = new(GetMSBuildExePath(), parameters);
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
                    NamedPipeServer namedPipeServer = new(_pipeNameDescription, OnRequest, NamedPipeServerStream.MaxAllowedServerInstances, token);
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
                VSTestTrace.SafeWriteTrace(() => ex.ToString());
                throw;
            }
        }

        private Task<IResponse> OnRequest(IRequest request)
        {
            if (TryGetModulePath(request, out string modulePath))
            {
                _testApplications[modulePath] = new TestApplication(modulePath, _pipeNameDescription.Name, _args);
                // Write the test application to the channel
                _actionQueue.Enqueue(_testApplications[modulePath]);

                return Task.FromResult((IResponse)VoidResponse.CachedInstance);
            }

            if (TryGetHelpResponse(request, out CommandLineOptionMessages commandLineOptionMessages))
            {
                var testApplication = _testApplications[commandLineOptionMessages.ModulePath];
                Debug.Assert(testApplication is not null);
                testApplication.OnCommandLineOptionMessages(commandLineOptionMessages);

                return Task.FromResult((IResponse)VoidResponse.CachedInstance);
            }

            throw new NotSupportedException($"Request '{request.GetType()}' is unsupported.");
        }

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

        private void OnErrorReceived(object sender, ErrorEventArgs args)
        {
            VSTestTrace.SafeWriteTrace(() => args.ErrorMessage);
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
