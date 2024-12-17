// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.CommandLine;
using System.IO.Pipes;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli
{
    internal sealed class MSBuildConnectionHandler : IDisposable
    {
        private List<string> _args;
        private readonly TestApplicationActionQueue _actionQueue;

        private readonly PipeNameDescription _pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
        private readonly List<NamedPipeServer> _namedPipeConnections = new();
        private readonly ConcurrentBag<TestApplication> _testApplications = new();
        private bool _areTestingPlatformApplications = true;

        public MSBuildConnectionHandler(List<string> args, TestApplicationActionQueue actionQueue)
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
                if (request is not ModuleMessage module)
                {
                    throw new NotSupportedException($"Request '{request.GetType()}' is unsupported.");
                }

                // Check if the test app has IsTestingPlatformApplication property set to true
                if (bool.TryParse(module.IsTestingPlatformApplication, out bool isTestingPlatformApplication) && isTestingPlatformApplication)
                {
                    var testApp = new TestApplication(new Module(module.DllOrExePath, module.ProjectPath, module.TargetFramework, module.RunSettingsFilePath), _args);
                    _testApplications.Add(testApp);
                }
                else // If one test app has IsTestingPlatformApplication set to false, then we will not run any of the test apps
                {
                    _areTestingPlatformApplications = false;
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

        public int RunWithMSBuild(ParseResult parseResult)
        {
            List<string> msbuildCommandLineArgs =
            [
                    parseResult.GetValue(TestingPlatformOptions.ProjectOption) ?? string.Empty,
                    "-t:Restore;_GetTestsProject",
                    $"-p:GetTestsProjectPipeName={_pipeNameDescription.Name}",
                    "-verbosity:q",
                    "-nologo",
            ];

            AddBinLogParameterIfExists(msbuildCommandLineArgs, _args);
            AddAdditionalMSBuildParametersIfExist(parseResult, msbuildCommandLineArgs);

            if (VSTestTrace.TraceEnabled)
            {
                VSTestTrace.SafeWriteTrace(() => $"MSBuild command line arguments: {string.Join(" ", msbuildCommandLineArgs)}");
            }

            ForwardingAppImplementation msBuildForwardingApp = new(GetMSBuildExePath(), msbuildCommandLineArgs);
            return msBuildForwardingApp.Execute();
        }

        private static void AddBinLogParameterIfExists(List<string> msbuildCommandLineArgs, List<string> args)
        {
            var binLog = args.FirstOrDefault(arg => arg.StartsWith("-bl", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(binLog))
            {
                msbuildCommandLineArgs.Add(binLog);

                // We remove it from the args list so that it is not passed to the test application
                args.Remove(binLog);
            }
        }

        private static void AddAdditionalMSBuildParametersIfExist(ParseResult parseResult, List<string> parameters)
        {
            string msBuildParameters = parseResult.GetValue(TestingPlatformOptions.AdditionalMSBuildParametersOption);

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

            foreach (var testApplication in _testApplications)
            {
                testApplication.Dispose();
            }
        }
    }
}
