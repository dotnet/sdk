// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.IO.Pipes;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli.commands.dotnet_test
{
    internal sealed class MSBuildConnectionHandler : IDisposable
    {
        private readonly PipeNameDescription _pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
        private readonly List<NamedPipeServer> _namedPipeConnections = new();
        private readonly string[] _args;

        private TestApplicationActionQueue _actionQueue;

        public MSBuildConnectionHandler(string[] args, TestApplicationActionQueue actionQueue)
        {
            _args = args;
            _actionQueue = actionQueue;
        }

        public async Task WaitConnectionAsync(CancellationToken token)
        {
            VSTestTrace.SafeWriteTrace(() => $"Waiting for connection(s) on pipe = {_pipeNameDescription.Name}");

            try
            {
                while (!token.IsCancellationRequested)
                {
                    NamedPipeServer pipeConnection = new(_pipeNameDescription, OnRequest, NamedPipeServerStream.MaxAllowedServerInstances, token, skipUnknownMessages: true);
                    pipeConnection.RegisterAllSerializers();

                    await pipeConnection.WaitConnectionAsync(token);

                    _namedPipeConnections.Add(pipeConnection);
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
                if (request is not Module module)
                {
                    throw new NotSupportedException($"Request '{request.GetType()}' is unsupported.");
                }

                var testApp = new TestApplication(module.DLLPath, _args);
                // Write the test application to the channel
                _actionQueue.Enqueue(testApp);
                testApp.OnCreated();
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

        public int RunWithMSBuild(ParseResult parseResult)
        {
            bool containsNoBuild = parseResult.UnmatchedTokens.Any(token => token == CliConstants.NoBuildOptionKey);
            List<string> msbuildCommandLineArgs =
            [
                    $"-t:{(containsNoBuild ? string.Empty : "Build;")}_GetTestsProject",
                    $"-p:GetTestsProjectPipeName={_pipeNameDescription.Name}",
                    "-verbosity:q"
            ];

            AddAdditionalMSBuildParameters(parseResult, msbuildCommandLineArgs);

            if (VSTestTrace.TraceEnabled)
            {
                VSTestTrace.SafeWriteTrace(() => $"MSBuild command line arguments: {string.Join(" ", msbuildCommandLineArgs)}");
            }

            ForwardingAppImplementation msBuildForwardingApp = new(GetMSBuildExePath(), msbuildCommandLineArgs);
            return msBuildForwardingApp.Execute();
        }

        private static void AddAdditionalMSBuildParameters(ParseResult parseResult, List<string> parameters)
        {
            string msBuildParameters = parseResult.GetValue(TestCommandParser.AdditionalMSBuildParameters);
            if (!string.IsNullOrEmpty(msBuildParameters))
            {
                parameters.AddRange(msBuildParameters.Split(" ", StringSplitOptions.RemoveEmptyEntries));
            }
        }

        private static string GetMSBuildExePath()
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                CliConstants.MSBuildExeName);
        }

        public void Dispose()
        {
            foreach (var namedPipeServer in _namedPipeConnections)
            {
                namedPipeServer.Dispose();
            }
        }
    }
}
