// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Commands.Test.IPC;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed class TestApplication(TestModule module, BuildOptions buildOptions) : IDisposable
{
    private readonly BuildOptions _buildOptions = buildOptions;

    private readonly List<string> _outputData = [];
    private readonly List<string> _errorData = [];
    private readonly PipeNameDescription _pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
    private readonly CancellationTokenSource _cancellationToken = new();

    private Task _testAppPipeConnectionLoop;
    private readonly List<NamedPipeServer> _testAppPipeConnections = [];

    public event EventHandler<HandshakeArgs> HandshakeReceived;
    public event EventHandler<HelpEventArgs> HelpRequested;
    public event EventHandler<DiscoveredTestEventArgs> DiscoveredTestsReceived;
    public event EventHandler<TestResultEventArgs> TestResultsReceived;
    public event EventHandler<FileArtifactEventArgs> FileArtifactsReceived;
    public event EventHandler<SessionEventArgs> SessionEventReceived;
    public event EventHandler<ErrorEventArgs> ErrorReceived;
    public event EventHandler<TestProcessExitEventArgs> TestProcessExited;

    public TestModule Module { get; } = module;

    public async Task<int> RunAsync(TestOptions testOptions)
    {
        if (testOptions.HasFilterMode && !ModulePathExists())
        {
            return ExitCode.GenericFailure;
        }

        var processStartInfo = CreateProcessStartInfo(testOptions);

        _testAppPipeConnectionLoop = Task.Run(async () => await WaitConnectionAsync(_cancellationToken.Token), _cancellationToken.Token);
        var testProcessResult = await StartProcess(processStartInfo);

        WaitOnTestApplicationPipeConnectionLoop();

        return testProcessResult;
    }

    private ProcessStartInfo CreateProcessStartInfo(TestOptions testOptions)
    {
        // Check for architecture mismatch when UseAppHost=false
        if (ShouldValidateArchitectureForUseAppHostFalse())
        {
            var archMismatchError = ValidateArchitectureCompatibility();
            if (archMismatchError != null)
            {
                throw new GracefulException(archMismatchError);
            }
        }

        var processStartInfo = new ProcessStartInfo
        {
            // We should get correct RunProperties right away.
            // For the case of dotnet test --test-modules path/to/dll, the TestModulesFilterHandler is responsible
            // for providing the dotnet muxer as RunCommand, and `exec "path/to/dll"` as RunArguments.
            FileName = Module.RunProperties.RunCommand,
            Arguments = GetArguments(testOptions),
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (!string.IsNullOrEmpty(Module.RunProperties.RunWorkingDirectory))
        {
            processStartInfo.WorkingDirectory = Module.RunProperties.RunWorkingDirectory;
        }

        if (Module.LaunchSettings is not null)
        {
            foreach (var entry in Module.LaunchSettings.EnvironmentVariables)
            {
                string value = Environment.ExpandEnvironmentVariables(entry.Value);
                processStartInfo.EnvironmentVariables[entry.Key] = value;
            }

            if (!_buildOptions.NoLaunchProfileArguments &&
                !string.IsNullOrEmpty(Module.LaunchSettings.CommandLineArgs))
            {
                processStartInfo.Arguments = $"{processStartInfo.Arguments} {Module.LaunchSettings.CommandLineArgs}";
            }
        }

        return processStartInfo;
    }

    private string GetArguments(TestOptions testOptions)
    {
        // Keep RunArguments first.
        // In the case of UseAppHost=false, RunArguments is set to `exec $(TargetPath)`:
        // https://github.com/dotnet/sdk/blob/333388c31d811701e3b6be74b5434359151424dc/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.targets#L1411
        // So, we keep that first always.
        // RunArguments is intentionally not escaped. It can contain multiple arguments and spaces there shouldn't cause the whole
        // value to be wrapped in double quotes. This matches dotnet run behavior.
        // In short, it's expected to already be escaped properly.
        StringBuilder builder = new(Module.RunProperties.RunArguments);

        if (testOptions.IsHelp)
        {
            builder.Append($" {TestingPlatformOptions.HelpOption.Name}");
        }

        if (_buildOptions.PathOptions.ResultsDirectoryPath is { } resultsDirectoryPath)
        {
            builder.Append($" {TestingPlatformOptions.ResultsDirectoryOption.Name} {ArgumentEscaper.EscapeSingleArg(resultsDirectoryPath)}");
        }

        foreach (var arg in _buildOptions.UnmatchedTokens)
        {
            builder.Append($" {ArgumentEscaper.EscapeSingleArg(arg)}");
        }

        builder.Append($" {CliConstants.ServerOptionKey} {CliConstants.ServerOptionValue} {CliConstants.DotNetTestPipeOptionKey} {ArgumentEscaper.EscapeSingleArg(_pipeNameDescription.Name)}");

        return builder.ToString();
    }

    private void WaitOnTestApplicationPipeConnectionLoop()
    {
        _cancellationToken.Cancel();
        _testAppPipeConnectionLoop?.Wait((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
    }

    private async Task WaitConnectionAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServer pipeConnection = new(_pipeNameDescription, OnRequest, NamedPipeServerStream.MaxAllowedServerInstances, token, skipUnknownMessages: true);
                pipeConnection.RegisterAllSerializers();

                await pipeConnection.WaitConnectionAsync(token);
                _testAppPipeConnections.Add(pipeConnection);
            }
        }
        catch (OperationCanceledException ex)
        {
            // We are exiting
            if (Logger.TraceEnabled)
            {
                string tokenType = ex.CancellationToken == token ? "internal token" : "external token";
                Logger.LogTrace(() => $"WaitConnectionAsync() throws OperationCanceledException with {tokenType}");
            }
        }
        catch (Exception ex)
        {
            if (Logger.TraceEnabled)
            {
                Logger.LogTrace(() => ex.ToString());
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
                case HandshakeMessage handshakeMessage:
                    if (handshakeMessage.Properties.TryGetValue(HandshakeMessagePropertyNames.ModulePath, out string value))
                    {
                        OnHandshakeMessage(handshakeMessage);

                        return Task.FromResult((IResponse)CreateHandshakeMessage(GetSupportedProtocolVersion(handshakeMessage)));
                    }
                    break;

                case CommandLineOptionMessages commandLineOptionMessages:
                    OnCommandLineOptionMessages(commandLineOptionMessages);
                    break;

                case DiscoveredTestMessages discoveredTestMessages:
                    OnDiscoveredTestMessages(discoveredTestMessages);
                    break;

                case TestResultMessages testResultMessages:
                    OnTestResultMessages(testResultMessages);
                    break;

                case FileArtifactMessages fileArtifactMessages:
                    OnFileArtifactMessages(fileArtifactMessages);
                    break;

                case TestSessionEvent sessionEvent:
                    OnSessionEvent(sessionEvent);
                    break;

                // If we don't recognize the message, log and skip it
                case UnknownMessage unknownMessage:
                    if (Logger.TraceEnabled)
                    {
                        Logger.LogTrace(() => $"Request '{request.GetType()}' with Serializer ID = {unknownMessage.SerializerId} is unsupported.");
                    }
                    return Task.FromResult((IResponse)VoidResponse.CachedInstance);

                default:
                    // If it doesn't match any of the above, throw an exception
                    throw new NotSupportedException(string.Format(CliCommandStrings.CmdUnsupportedMessageRequestTypeException, request.GetType()));
            }
        }
        catch (Exception ex)
        {
            if (Logger.TraceEnabled)
            {
                Logger.LogTrace(() => ex.ToString());
            }

            Environment.FailFast(ex.ToString());
        }

        return Task.FromResult((IResponse)VoidResponse.CachedInstance);
    }

    private static string GetSupportedProtocolVersion(HandshakeMessage handshakeMessage)
    {
        handshakeMessage.Properties.TryGetValue(HandshakeMessagePropertyNames.SupportedProtocolVersions, out string protocolVersions);

        string version = string.Empty;
        if (protocolVersions is not null && protocolVersions.Split(";").Contains(ProtocolConstants.Version))
        {
            version = ProtocolConstants.Version;
        }

        return version;
    }

    private static HandshakeMessage CreateHandshakeMessage(string version) =>
        new(new Dictionary<byte, string>
        {
            { HandshakeMessagePropertyNames.PID, Process.GetCurrentProcess().Id.ToString() },
            { HandshakeMessagePropertyNames.Architecture, RuntimeInformation.ProcessArchitecture.ToString() },
            { HandshakeMessagePropertyNames.Framework, RuntimeInformation.FrameworkDescription },
            { HandshakeMessagePropertyNames.OS, RuntimeInformation.OSDescription },
            { HandshakeMessagePropertyNames.SupportedProtocolVersions, version }
        });

    private async Task<int> StartProcess(ProcessStartInfo processStartInfo)
    {
        if (Logger.TraceEnabled)
        {
            Logger.LogTrace(() => $"Test application arguments: {processStartInfo.Arguments}");
        }

        var process = Process.Start(processStartInfo);
        StoreOutputAndErrorData(process);
        await process.WaitForExitAsync();

        TestProcessExited?.Invoke(this, new TestProcessExitEventArgs { OutputData = _outputData, ErrorData = _errorData, ExitCode = process.ExitCode });

        return process.ExitCode;
    }

    private void StoreOutputAndErrorData(Process process)
    {
        process.EnableRaisingEvents = true;

        process.OutputDataReceived += (sender, e) =>
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            _outputData.Add(e.Data);
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            _errorData.Add(e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    private bool ModulePathExists()
    {
        if (!File.Exists(Module.RunProperties.RunCommand))
        {
            ErrorReceived.Invoke(this, new ErrorEventArgs { ErrorMessage = $"Test module '{Module.RunProperties.RunCommand}' not found. Build the test application before or run 'dotnet test'." });
            return false;
        }
        return true;
    }

    public void OnHandshakeMessage(HandshakeMessage handshakeMessage)
    {
        HandshakeReceived?.Invoke(this, new HandshakeArgs { Handshake = new Handshake(handshakeMessage.Properties) });
    }

    public void OnCommandLineOptionMessages(CommandLineOptionMessages commandLineOptionMessages)
    {
        HelpRequested?.Invoke(this, new HelpEventArgs { ModulePath = commandLineOptionMessages.ModulePath, CommandLineOptions = [.. commandLineOptionMessages.CommandLineOptionMessageList.Select(message => new CommandLineOption(message.Name, message.Description, message.IsHidden, message.IsBuiltIn))] });
    }

    internal void OnDiscoveredTestMessages(DiscoveredTestMessages discoveredTestMessages)
    {
        DiscoveredTestsReceived?.Invoke(this, new DiscoveredTestEventArgs
        {
            ExecutionId = discoveredTestMessages.ExecutionId,
            InstanceId = discoveredTestMessages.InstanceId,
            DiscoveredTests = [.. discoveredTestMessages.DiscoveredMessages.Select(message => new DiscoveredTest(message.Uid, message.DisplayName))]
        });
    }

    internal void OnTestResultMessages(TestResultMessages testResultMessage)
    {
        TestResultsReceived?.Invoke(this, new TestResultEventArgs
        {
            ExecutionId = testResultMessage.ExecutionId,
            InstanceId = testResultMessage.InstanceId,
            SuccessfulTestResults = [.. testResultMessage.SuccessfulTestMessages.Select(message => new SuccessfulTestResult(message.Uid, message.DisplayName, message.State, message.Duration, message.Reason, message.StandardOutput, message.ErrorOutput, message.SessionUid))],
            FailedTestResults = [.. testResultMessage.FailedTestMessages.Select(message => new FailedTestResult(message.Uid, message.DisplayName, message.State, message.Duration, message.Reason, [.. message.Exceptions.Select(e => new FlatException(e.ErrorMessage, e.ErrorType, e.StackTrace))], message.StandardOutput, message.ErrorOutput, message.SessionUid))]
        });
    }

    internal void OnFileArtifactMessages(FileArtifactMessages fileArtifactMessages)
    {
        FileArtifactsReceived?.Invoke(this, new FileArtifactEventArgs
        {
            ExecutionId = fileArtifactMessages.ExecutionId,
            InstanceId = fileArtifactMessages.InstanceId,
            FileArtifacts = [.. fileArtifactMessages.FileArtifacts.Select(message => new FileArtifact(message.FullPath, message.DisplayName, message.Description, message.TestUid, message.TestDisplayName, message.SessionUid))]
        });
    }

    internal void OnSessionEvent(TestSessionEvent sessionEvent)
    {
        SessionEventReceived?.Invoke(this, new SessionEventArgs { SessionEvent = new TestSession(sessionEvent.SessionType, sessionEvent.SessionUid, sessionEvent.ExecutionId) });
    }

    public override string ToString()
    {
        StringBuilder builder = new();

        if (!string.IsNullOrEmpty(Module.RunProperties.RunCommand))
        {
            builder.Append($"{ProjectProperties.RunCommand}: {Module.RunProperties.RunCommand}");
        }

        if (!string.IsNullOrEmpty(Module.RunProperties.RunArguments))
        {
            builder.Append($"{ProjectProperties.RunArguments}: {Module.RunProperties.RunArguments}");
        }

        if (!string.IsNullOrEmpty(Module.RunProperties.RunWorkingDirectory))
        {
            builder.Append($"{ProjectProperties.RunWorkingDirectory}: {Module.RunProperties.RunWorkingDirectory}");
        }

        if (!string.IsNullOrEmpty(Module.ProjectFullPath))
        {
            builder.Append($"{ProjectProperties.ProjectFullPath}: {Module.ProjectFullPath}");
        }

        if (!string.IsNullOrEmpty(Module.TargetFramework))
        {
            builder.Append($"{ProjectProperties.TargetFramework} : {Module.TargetFramework}");
        }

        return builder.ToString();
    }

    private bool ShouldValidateArchitectureForUseAppHostFalse()
    {
        // Check if UseAppHost=false (RunArguments starts with "exec")
        return !string.IsNullOrEmpty(Module.RunProperties.RunArguments) && 
               Module.RunProperties.RunArguments.TrimStart().StartsWith("exec ", StringComparison.OrdinalIgnoreCase);
    }

    private string ValidateArchitectureCompatibility()
    {
        // Extract the requested architecture from MSBuild args
        string requestedArch = GetRequestedArchitecture();
        if (string.IsNullOrEmpty(requestedArch))
        {
            // No architecture specified, no validation needed
            return null;
        }

        // Get current muxer architecture 
        string currentArch = GetCurrentMuxerArchitecture();
        
        // Normalize architecture names for comparison
        string normalizedRequested = NormalizeArchitectureName(requestedArch);
        string normalizedCurrent = NormalizeArchitectureName(currentArch);

        if (!string.Equals(normalizedRequested, normalizedCurrent, StringComparison.OrdinalIgnoreCase))
        {
            return $"The current .NET host does not support the requested target architecture '{requestedArch}'. " +
                   $"The current host is running '{currentArch}' architecture. " +
                   $"When UseAppHost is false, the target architecture must match the current .NET host architecture.";
        }

        return null;
    }

    private string GetRequestedArchitecture()
    {
        // Look for architecture in MSBuild args
        foreach (var arg in _buildOptions.MSBuildArgs)
        {
            if (arg.StartsWith("--property:RuntimeIdentifier=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-property:RuntimeIdentifier=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("/property:RuntimeIdentifier=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-p:RuntimeIdentifier=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("/p:RuntimeIdentifier=", StringComparison.OrdinalIgnoreCase))
            {
                var rid = arg.Split('=', 2)[1];
                return ExtractArchitectureFromRid(rid);
            }
        }

        // Also check UnmatchedTokens for --arch parameter
        for (int i = 0; i < _buildOptions.UnmatchedTokens.Count - 1; i++)
        {
            if (_buildOptions.UnmatchedTokens[i] == "--arch" || _buildOptions.UnmatchedTokens[i] == "-a")
            {
                return _buildOptions.UnmatchedTokens[i + 1];
            }
        }

        return null;
    }

    private static string ExtractArchitectureFromRid(string rid)
    {
        // RID format is typically os-arch (e.g., linux-x64, win-x86, osx-arm64)
        var parts = rid.Split('-');
        if (parts.Length >= 2)
        {
            return parts[^1]; // Last part is the architecture
        }
        return rid; // Fallback to the entire string
    }

    private static string GetCurrentMuxerArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            Architecture.Arm64 => "arm64",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
        };
    }

    private static string NormalizeArchitectureName(string arch)
    {
        return arch.ToLowerInvariant() switch
        {
            "amd64" => "x64",
            "x86_64" => "x64",
            "arm64" => "arm64",
            "aarch64" => "arm64",
            _ => arch.ToLowerInvariant()
        };
    }

    public void Dispose()
    {
        foreach (var namedPipeServer in _testAppPipeConnections)
        {
            namedPipeServer.Dispose();
        }

        WaitOnTestApplicationPipeConnectionLoop();
    }
}
