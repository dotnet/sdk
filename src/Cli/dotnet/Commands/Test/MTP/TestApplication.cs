// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using Microsoft.DotNet.Cli.Commands.Test.IPC;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed class TestApplication(
    TestModule module,
    BuildOptions buildOptions,
    TestOptions testOptions,
    TerminalTestReporter output) : IDisposable
{
    private readonly BuildOptions _buildOptions = buildOptions;
    private readonly TestApplicationHandler _handler = new(output, module, testOptions);

    private readonly List<string> _outputData = [];
    private readonly List<string> _errorData = [];
    private readonly PipeNameDescription _pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
    private readonly CancellationTokenSource _cancellationToken = new();

    private Task _testAppPipeConnectionLoop;
    private readonly List<NamedPipeServer> _testAppPipeConnections = [];
    private readonly Dictionary<NamedPipeServer, HandshakeMessage> _handshakes = new();

    public event EventHandler<HelpEventArgs> HelpRequested;

    public TestModule Module { get; } = module;
    public TestOptions TestOptions { get; } = testOptions;

    public bool HasFailureDuringDispose { get; private set; }

    public async Task<int> RunAsync()
    {
        // TODO: RunAsync is probably expected to be executed exactly once on each TestApplication instance.
        // Consider throwing an exception if it's called more than once.
        if (TestOptions.HasFilterMode && !ModulePathExists())
        {
            return ExitCode.GenericFailure;
        }

        var processStartInfo = CreateProcessStartInfo();

        _testAppPipeConnectionLoop = Task.Run(async () => await WaitConnectionAsync(_cancellationToken.Token), _cancellationToken.Token);
        var testProcessResult = await StartProcess(processStartInfo);

        WaitOnTestApplicationPipeConnectionLoop();

        return testProcessResult;
    }

    private ProcessStartInfo CreateProcessStartInfo()
    {
        var processStartInfo = new ProcessStartInfo
        {
            // We should get correct RunProperties right away.
            // For the case of dotnet test --test-modules path/to/dll, the TestModulesFilterHandler is responsible
            // for providing the dotnet muxer as RunCommand, and `exec "path/to/dll"` as RunArguments.
            FileName = Module.RunProperties.Command,
            Arguments = GetArguments(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        if (!string.IsNullOrEmpty(Module.RunProperties.WorkingDirectory))
        {
            processStartInfo.WorkingDirectory = Module.RunProperties.WorkingDirectory;
        }

        if (Module.LaunchSettings is not null)
        {
            foreach (var entry in Module.LaunchSettings.EnvironmentVariables)
            {
                string value = Environment.ExpandEnvironmentVariables(entry.Value);
                processStartInfo.Environment[entry.Key] = value;
            }

            if (!_buildOptions.NoLaunchProfileArguments &&
                !string.IsNullOrEmpty(Module.LaunchSettings.CommandLineArgs))
            {
                processStartInfo.Arguments = $"{processStartInfo.Arguments} {Module.LaunchSettings.CommandLineArgs}";
            }
        }

        if (Module.DotnetRootArchVariableName is not null)
        {
            processStartInfo.Environment[Module.DotnetRootArchVariableName] = Path.GetDirectoryName(new Muxer().MuxerPath);
        }

        return processStartInfo;
    }

    private string GetArguments()
    {
        // Keep RunArguments first.
        // In the case of UseAppHost=false, RunArguments is set to `exec $(TargetPath)`:
        // https://github.com/dotnet/sdk/blob/333388c31d811701e3b6be74b5434359151424dc/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.targets#L1411
        // So, we keep that first always.
        // RunArguments is intentionally not escaped. It can contain multiple arguments and spaces there shouldn't cause the whole
        // value to be wrapped in double quotes. This matches dotnet run behavior.
        // In short, it's expected to already be escaped properly.
        StringBuilder builder = new(Module.RunProperties.Arguments);

        if (TestOptions.IsHelp)
        {
            builder.Append($" {CliConstants.HelpOptionKey}");
        }

        if (TestOptions.IsDiscovery)
        {
            builder.Append($" {MicrosoftTestingPlatformOptions.ListTestsOption.Name}");
        }

        if (_buildOptions.PathOptions.ResultsDirectoryPath is { } resultsDirectoryPath)
        {
            builder.Append($" {MicrosoftTestingPlatformOptions.ResultsDirectoryOption.Name} {ArgumentEscaper.EscapeSingleArg(resultsDirectoryPath)}");
        }

        if (_buildOptions.PathOptions.ConfigFilePath is { } configFilePath)
        {
            builder.Append($" {MicrosoftTestingPlatformOptions.ConfigFileOption.Name} {ArgumentEscaper.EscapeSingleArg(configFilePath)}");
        }

        if (_buildOptions.PathOptions.DiagnosticOutputDirectoryPath is { } diagnosticOutputDirectoryPath)
        {
            builder.Append($" {MicrosoftTestingPlatformOptions.DiagnosticOutputDirectoryOption.Name} {ArgumentEscaper.EscapeSingleArg(diagnosticOutputDirectoryPath)}");
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
                var pipeConnection = new NamedPipeServer(_pipeNameDescription, OnRequest, NamedPipeServerStream.MaxAllowedServerInstances, token, skipUnknownMessages: true);
                pipeConnection.RegisterAllSerializers();

                await pipeConnection.WaitConnectionAsync(token);
                _testAppPipeConnections.Add(pipeConnection);
            }
        }
        catch (OperationCanceledException ex)
        {
            // We are exiting
            Logger.LogTrace($"WaitConnectionAsync() throws OperationCanceledException with {(ex.CancellationToken == token ? "internal token" : "external token")}");
        }
        catch (Exception ex)
        {
            var exAsString = ex.ToString();
            Logger.LogTrace(exAsString);
            Environment.FailFast(exAsString);
        }
    }

    private Task<IResponse> OnRequest(NamedPipeServer server, IRequest request)
    {
        try
        {
            switch (request)
            {
                case HandshakeMessage handshakeMessage:
                    _handshakes.Add(server, handshakeMessage);
                    string negotiatedVersion = GetSupportedProtocolVersion(handshakeMessage);
                    OnHandshakeMessage(handshakeMessage, negotiatedVersion.Length > 0);
                    return Task.FromResult((IResponse)CreateHandshakeMessage(negotiatedVersion));

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
                    Logger.LogTrace($"Request '{request.GetType()}' with Serializer ID = {unknownMessage.SerializerId} is unsupported.");
                    return Task.FromResult((IResponse)VoidResponse.CachedInstance);

                default:
                    // If it doesn't match any of the above, throw an exception
                    throw new NotSupportedException(string.Format(CliCommandStrings.CmdUnsupportedMessageRequestTypeException, request.GetType()));
            }
        }
        catch (Exception ex)
        {
            string exAsString = ex.ToString();
            Logger.LogTrace(exAsString);
            Environment.FailFast(exAsString);
        }

        return Task.FromResult((IResponse)VoidResponse.CachedInstance);
    }

    private static string GetSupportedProtocolVersion(HandshakeMessage handshakeMessage)
    {
        if (!handshakeMessage.Properties.TryGetValue(HandshakeMessagePropertyNames.SupportedProtocolVersions, out string protocolVersions) ||
            protocolVersions is null)
        {
            // It's not expected we hit this.
            // TODO: Maybe we should fail more hard?
            return string.Empty;
        }

        // NOTE: Today, ProtocolConstants.Version is only 1.0.0 (i.e, SDK supports only a single version).
        // Whenever we support multiple versions in SDK, we should do intersection
        // between protocolVersions given by MTP, and the versions supported by SDK.
        // Then we return the "highest" version from the intersection.
        // The current logic **assumes** that ProtocolConstants.SupportedVersions is a single version.
        if (protocolVersions.Split(";").Contains(ProtocolConstants.SupportedVersions))
        {
            return ProtocolConstants.SupportedVersions;
        }

        // The version given by MTP is not supported by SDK.
        return string.Empty;
    }

    private static HandshakeMessage CreateHandshakeMessage(string version) =>
        new(new Dictionary<byte, string>(capacity: 5)
        {
            { HandshakeMessagePropertyNames.PID, Environment.ProcessId.ToString(CultureInfo.InvariantCulture) },
            { HandshakeMessagePropertyNames.Architecture, RuntimeInformation.ProcessArchitecture.ToString() },
            { HandshakeMessagePropertyNames.Framework, RuntimeInformation.FrameworkDescription },
            { HandshakeMessagePropertyNames.OS, RuntimeInformation.OSDescription },
            { HandshakeMessagePropertyNames.SupportedProtocolVersions, version }
        });

    private async Task<int> StartProcess(ProcessStartInfo processStartInfo)
    {
        Logger.LogTrace($"Test application arguments: {processStartInfo.Arguments}");

        using var process = Process.Start(processStartInfo);
        StoreOutputAndErrorData(process);
        await process.WaitForExitAsync();

        _handler.OnTestProcessExited(process.ExitCode, _outputData, _errorData);

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
        if (!File.Exists(Module.RunProperties.Command))
        {
            // TODO: The error should be shown to the user, not just logged to trace.
            Logger.LogTrace($"Test module '{Module.RunProperties.Command}' not found. Build the test application before or run 'dotnet test'.");

            return false;
        }
        return true;
    }

    public void OnHandshakeMessage(HandshakeMessage handshakeMessage, bool gotSupportedVersion)
        => _handler.OnHandshakeReceived(handshakeMessage, gotSupportedVersion);

    private void OnCommandLineOptionMessages(CommandLineOptionMessages commandLineOptionMessages)
    {
        HelpRequested?.Invoke(this, new HelpEventArgs { ModulePath = commandLineOptionMessages.ModulePath, CommandLineOptions = [.. commandLineOptionMessages.CommandLineOptionMessageList.Select(message => new CommandLineOption(message.Name, message.Description, message.IsHidden, message.IsBuiltIn))] });
    }

    private void OnDiscoveredTestMessages(DiscoveredTestMessages discoveredTestMessages)
        => _handler.OnDiscoveredTestsReceived(discoveredTestMessages);

    private void OnTestResultMessages(TestResultMessages testResultMessage)
        => _handler.OnTestResultsReceived(testResultMessage);

    internal void OnFileArtifactMessages(FileArtifactMessages fileArtifactMessages)
        => _handler.OnFileArtifactsReceived(fileArtifactMessages);

    private void OnSessionEvent(TestSessionEvent sessionEvent)
        => _handler.OnSessionEventReceived(sessionEvent);

    public override string ToString()
    {
        StringBuilder builder = new();

        if (!string.IsNullOrEmpty(Module.RunProperties.Command))
        {
            builder.Append($"{ProjectProperties.RunCommand}: {Module.RunProperties.Command}");
        }

        if (!string.IsNullOrEmpty(Module.RunProperties.Arguments))
        {
            builder.Append($"{ProjectProperties.RunArguments}: {Module.RunProperties.Arguments}");
        }

        if (!string.IsNullOrEmpty(Module.RunProperties.WorkingDirectory))
        {
            builder.Append($"{ProjectProperties.RunWorkingDirectory}: {Module.RunProperties.WorkingDirectory}");
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

    public void Dispose()
    {
        foreach (var namedPipeServer in _testAppPipeConnections)
        {
            try
            {
                namedPipeServer.Dispose();
            }
            catch (Exception ex)
            {
                StringBuilder messageBuilder;
                if (_handshakes.TryGetValue(namedPipeServer, out var handshake))
                {
                    messageBuilder = new StringBuilder(CliCommandStrings.DotnetTestPipeFailureHasHandshake);
                    messageBuilder.AppendLine();
                    foreach (var kvp in handshake.Properties)
                    {
                        messageBuilder.AppendLine($"{kvp.Key}: {kvp.Value}");
                    }
                }
                else
                {
                    messageBuilder = new StringBuilder(CliCommandStrings.DotnetTestPipeFailureWithoutHandshake);
                    messageBuilder.AppendLine();
                }

                messageBuilder.AppendLine($"RunCommand: {Module.RunProperties.Command}");
                messageBuilder.AppendLine($"RunArguments: {Module.RunProperties.Arguments}");
                messageBuilder.AppendLine(ex.ToString());

                HasFailureDuringDispose = true;
                Reporter.Error.WriteLine(messageBuilder.ToString());
            }
        }

        WaitOnTestApplicationPipeConnectionLoop();
    }
}
