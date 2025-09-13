// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Threading;
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
    TerminalTestReporter output,
    Action<CommandLineOptionMessages> onHelpRequested) : IDisposable
{
    private readonly BuildOptions _buildOptions = buildOptions;
    private readonly Action<CommandLineOptionMessages> _onHelpRequested = onHelpRequested;
    private readonly TestApplicationHandler _handler = new(output, module, testOptions);

    private readonly string _pipeName = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));

    private readonly List<NamedPipeServer> _testAppPipeConnections = [];
    private readonly Dictionary<NamedPipeServer, HandshakeMessage> _handshakes = new();

    public TestModule Module { get; } = module;
    public TestOptions TestOptions { get; } = testOptions;

    public bool HasFailureDuringDispose { get; private set; }

    public async Task<int> RunAsync()
    {
        // TODO: RunAsync is probably expected to be executed exactly once on each TestApplication instance.
        // Consider throwing an exception if it's called more than once.
        var processStartInfo = CreateProcessStartInfo();

        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var testAppPipeConnectionLoop = Task.Run(async () => await WaitConnectionAsync(cancellationToken));

        try
        {
            Logger.LogTrace($"Starting test process with command '{processStartInfo.FileName}' and arguments '{processStartInfo.Arguments}'.");

            using var process = Process.Start(processStartInfo)!;
            var standardOutput = process.StandardOutput;
            var standardError = process.StandardError;

            // Reading from process stdout/stderr is done on separate threads to avoid blocking IO on the threadpool.
            // Note: even with 'process.StandardOutput.ReadToEndAsync()' or 'process.BeginOutputReadLine()', we ended up with
            // many TP threads just doing synchronous IO, slowing down the progress of the test run.
            // We want to read requests coming through the pipe and sending responses back to the test app as fast as possible.
            var stdOutTask = Task.Factory.StartNew(static standardOutput => ((StreamReader)standardOutput!).ReadToEnd(), standardOutput, TaskCreationOptions.LongRunning);
            var stdErrTask = Task.Factory.StartNew(static standardError => ((StreamReader)standardError!).ReadToEnd(), standardError, TaskCreationOptions.LongRunning);

            var outputAndError = await Task.WhenAll(stdOutTask, stdErrTask);
            await process.WaitForExitAsync();

            _handler.OnTestProcessExited(process.ExitCode, outputAndError[0], outputAndError[1]);

            if (_handler.HasMismatchingTestSessionEventCount())
            {
                throw new InvalidOperationException(CliCommandStrings.MissingTestSessionEnd);
            }

            return process.ExitCode;
        }
        finally
        {
            cancellationTokenSource.Cancel();
            await testAppPipeConnectionLoop;
        }
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
            // False is already the default on .NET Core, but prefer to be explicit.
            UseShellExecute = false,
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

        builder.Append($" {CliConstants.ServerOptionKey} {CliConstants.ServerOptionValue} {CliConstants.DotNetTestPipeOptionKey} {ArgumentEscaper.EscapeSingleArg(_pipeName)}");

        return builder.ToString();
    }

    private async Task WaitConnectionAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var pipeConnection = new NamedPipeServer(_pipeName, OnRequest, NamedPipeServerStream.MaxAllowedServerInstances, token, skipUnknownMessages: true);
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
            // BE CAREFUL:
            // When handling some of the messages, we may throw an exception in unexpected state.
            // (e.g, OnSessionEvent may throw if we receive TestSessionEnd without TestSessionStart).
            // (or if we receive help-related messages when not in help mode)
            // In that case, we FailFast.
            // The lack of FailFast *might* have unintended consequences, such as breaking the internal loop of pipe server.
            // In that case, maybe MTP app will continue waiting for response, but we don't send the response and are waiting for
            // MTP app process exit (which doesn't happen).
            // So, we explicitly FailFast here.
            string exAsString = ex.ToString();
            Logger.LogTrace(exAsString);
            Environment.FailFast(exAsString);
        }

        return Task.FromResult((IResponse)VoidResponse.CachedInstance);
    }

    private static string GetSupportedProtocolVersion(HandshakeMessage handshakeMessage)
    {
        if (!handshakeMessage.Properties.TryGetValue(HandshakeMessagePropertyNames.SupportedProtocolVersions, out string? protocolVersions) ||
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
        new HandshakeMessage(new Dictionary<byte, string>(capacity: 5)
        {
            { HandshakeMessagePropertyNames.PID, Environment.ProcessId.ToString(CultureInfo.InvariantCulture) },
            { HandshakeMessagePropertyNames.Architecture, RuntimeInformation.ProcessArchitecture.ToString() },
            { HandshakeMessagePropertyNames.Framework, RuntimeInformation.FrameworkDescription },
            { HandshakeMessagePropertyNames.OS, RuntimeInformation.OSDescription },
            { HandshakeMessagePropertyNames.SupportedProtocolVersions, version }
        });

    public void OnHandshakeMessage(HandshakeMessage handshakeMessage, bool gotSupportedVersion)
        => _handler.OnHandshakeReceived(handshakeMessage, gotSupportedVersion);

    private void OnCommandLineOptionMessages(CommandLineOptionMessages commandLineOptionMessages)
    {
        if (!TestOptions.IsHelp)
        {
            throw new InvalidOperationException(CliCommandStrings.UnexpectedHelpMessage);
        }

        _onHelpRequested(commandLineOptionMessages);
    }

    private void OnDiscoveredTestMessages(DiscoveredTestMessages discoveredTestMessages)
        => _handler.OnDiscoveredTestsReceived(discoveredTestMessages);

    private void OnTestResultMessages(TestResultMessages testResultMessage)
        => _handler.OnTestResultsReceived(testResultMessage);

    private void OnFileArtifactMessages(FileArtifactMessages fileArtifactMessages)
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
    }
}
