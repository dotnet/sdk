// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Concurrent;
using System.CommandLine;
using System.IO.Pipes;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Build;
using Microsoft.DotNet.Tools.Test;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.DotNet.Cli
{
    internal partial class TestingPlatformCommand : CliCommand, ICustomHelp
    {
        private readonly List<NamedPipeServer> _namedPipeServers = new();
        private readonly List<Task> _taskModuleName = [];
        private readonly ConcurrentBag<Task> _testsRun = [];
        private readonly ConcurrentDictionary<string, (CommandLineOptionMessage, string[])> _commandLineOptionNameToModuleNames = [];
        private readonly ConcurrentDictionary<string, TestApplication> _testApplications = [];
        private readonly PipeNameDescription _pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
        private readonly CancellationTokenSource _cancellationToken = new();

        private Task _namedPipeConnectionLoop;
        private string[] _args;

        private const string MSBuildExeName = "MSBuild.dll";

        public int Run(ParseResult parseResult)
        {
            _args = parseResult.GetArguments();

            VSTestTrace.SafeWriteTrace(() => $"Wait for connection(s) on pipe = {_pipeNameDescription.Name}");
            _namedPipeConnectionLoop = Task.Run(async () => await WaitConnectionAsync(_cancellationToken.Token));

            bool containsNoBuild = parseResult.UnmatchedTokens.Any(x => x == "--no-build");

            if (containsNoBuild)
            {
                ForwardingAppImplementation mSBuildForwardingApp = new(GetMSBuildExePath(), ["-t:_GetTestsProject", $"-p:GetTestsProjectPipeName={_pipeNameDescription.Name}", "-verbosity:q"]);
                int getTestsProjectResult = mSBuildForwardingApp.Execute();
            }
            else
            {
                BuildCommand buildCommand = BuildCommand.FromArgs(["-t:_BuildTestsProject;_GetTestsProject", "-bl", $"-p:GetTestsProjectPipeName={_pipeNameDescription.Name}", "-verbosity:q"]);
                int buildResult = buildCommand.Execute();
            }

            // Above line will block till we have all connections and all GetTestsProject msbuild task complete.
            Task.WaitAll([.. _taskModuleName]);
            Task.WaitAll([.. _testsRun]);
            _cancellationToken.Cancel();
            _namedPipeConnectionLoop.Wait();

            return 0;
        }

        private async Task WaitConnectionAsync(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    NamedPipeServer namedPipeServer = new(_pipeNameDescription, Callback, NamedPipeServerStream.MaxAllowedServerInstances, token);
                    namedPipeServer.RegisterSerializer<Module>(new ModuleSerializer());
                    namedPipeServer.RegisterSerializer<CommandLineOptionMessages>(new CommandLineOptionMessagesSerializer());
                    namedPipeServer.RegisterSerializer<VoidResponse>(new VoidResponseSerializer());

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

        private Task<IResponse> Callback(IRequest request)
        {
            if (request is Module module)
            {
                var testApplication = new TestApplication(module.Name, _pipeNameDescription.Name, _args);
                _testApplications[Path.GetFileName(module.Name)] = testApplication;
                testApplication.HelpOptionsEvent += OnHelpOptionsEvent;
                testApplication.ErrorEvent += OnErrorEvent;

                _testsRun.Add(Task.Run(async () => await testApplication.Run()));
            }
            else if (request is CommandLineOptionMessages commandLineOptionMessages)
            {
                var testApplication = _testApplications[commandLineOptionMessages.ModuleName];
                testApplication?.RunHelp(commandLineOptionMessages);
            }
            else
            {
                throw new NotSupportedException($"Request '{request.GetType()}' is unsupported.");
            }

            return Task.FromResult((IResponse)VoidResponse.CachedInstance);
        }

        private void OnHelpOptionsEvent(object sender, CommandLineOptionMessages commandLineOptionMessages)
        {
            string moduleName = commandLineOptionMessages.ModuleName;

            foreach (CommandLineOptionMessage commandLineOptionMessage in commandLineOptionMessages.CommandLineOptionMessageList)
            {
                if (commandLineOptionMessage.IsHidden) continue;

                _commandLineOptionNameToModuleNames.AddOrUpdate(
                    commandLineOptionMessage.Name,
                    (key) => (commandLineOptionMessage, new[] { moduleName }), (optionName, value) => (value.Item1, value.Item2.Concat([moduleName]).ToArray()));
            }
        }

        private void OnErrorEvent(object sender, string moduleName)
        {
            VSTestTrace.SafeWriteTrace(() => $"Test module '{moduleName}' not found. Build the test application before or run 'dotnet test'.");
        }

        private static string GetMSBuildExePath()
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                MSBuildExeName);
        }

        public TestingPlatformCommand(string name, string description = null) : base(name, description)
        {
            TreatUnmatchedTokensAsErrors = false;
        }
    }
}
