// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using Microsoft.DotNet.Cli.Commands.Test.IPC;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed class TestApplication(
    TestModule module,
    BuildOptions buildOptions,
    TestOptions testOptions,
    TestResultsDirectoryResolver resultsDirectoryResolver,
    TerminalTestReporter output,
    Action<CommandLineOptionMessages> onHelpRequested,
    ArtifactPostProcessingManager? artifactPostProcessingManager = null,
    ArtifactPostProcessingInvocation? artifactPostProcessingInvocation = null,
    TestRunPolicy? testRunPolicy = null) : IDisposable
{
    private static readonly Version ProtocolVersion_1_1 = new(1, 1, 0);
    private static readonly TimeSpan DefaultArtifactPostProcessingTimeout = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Largest timeout <see cref="Task.WaitAsync(TimeSpan)"/> accepts, in seconds. Anything above
    /// <c>Timer.MaxSupportedTimeout</c> (0xFFFFFFFE milliseconds) throws instead of waiting.
    /// </summary>
    private const int MaximumArtifactPostProcessingTimeoutSeconds = (int)(0xFFFFFFFE / 1000);

    private static readonly TimeSpan ArtifactPostProcessingTimeout = GetArtifactPostProcessingTimeout();
    private const int LiveOutputTailLineCount = 200;

    private readonly Lock _requestLock = new();
    private readonly Lock _controlRequestLock = new();
    private readonly Lock _pipeConnectionsLock = new();
    private readonly BuildOptions _buildOptions = buildOptions;
    private readonly TestResultsDirectoryResolver _resultsDirectoryResolver = resultsDirectoryResolver;
    private readonly Action<CommandLineOptionMessages> _onHelpRequested = onHelpRequested;
    private readonly TestApplicationHandler _handler = new(
        output,
        module,
        testOptions,
        artifactPostProcessingManager,
        artifactPostProcessingInvocation,
        testRunPolicy);
    private readonly ArtifactPostProcessingInvocation? _artifactPostProcessingInvocation = artifactPostProcessingInvocation;
    private readonly TestRunPolicy? _testRunPolicy = testRunPolicy;
    private readonly CancellationTokenSource _pipeCancellationTokenSource = new();
    private HttpTestHostGateway? _httpGateway;
    private string? _httpResponseFilePath;

    private readonly string _pipeName = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
    private readonly string _controlPipeName = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));

    private readonly List<NamedPipeServer> _testAppPipeConnections = [];
    private readonly Dictionary<NamedPipeServer, HandshakeMessage> _handshakes = new();
    private readonly List<TaskCompletionSource<IResponse>> _pendingControlRequests = [];

    private int _hasRun;
    private int _protocolNegotiated;
    private int _sessionCancellationRequested;
    private Version? _negotiatedProtocolVersion;
    private ProcessOutputCollector? _standardOutputCollector;
    private ProcessOutputCollector? _standardErrorCollector;

    public TestModule Module { get; } = module;
    public TestOptions TestOptions { get; } = testOptions;

    public bool HasFailureDuringDispose { get; private set; }

    internal bool IsProtocol_1_1_OrHigher =>
        _negotiatedProtocolVersion is { } negotiatedProtocolVersion &&
        negotiatedProtocolVersion.CompareTo(ProtocolVersion_1_1) >= 0;

    public async Task<int> RunAsync(CtrlCCancellationManager ctrlC)
    {
        if (Interlocked.Exchange(ref _hasRun, 1) != 0)
        {
            throw new InvalidOperationException(CliCommandStrings.RunAsyncCalledMoreThanOnce);
        }

        var processStartInfo = CreateProcessStartInfo();

        var cancellationToken = _pipeCancellationTokenSource.Token;
        var testAppPipeConnectionLoop = _httpGateway is null
            ? Task.Run(async () => await WaitConnectionAsync(cancellationToken))
            : Task.CompletedTask;
        var controlPipeConnectionLoop = _httpGateway is null
            ? Task.Run(async () => await WaitControlConnectionAsync(cancellationToken))
            : Task.CompletedTask;

        Process? process = null;
        bool testApplicationStarted = false;
        try
        {
            Logger.LogTrace($"Starting test process with command '{processStartInfo.FileName}' and arguments '{GetArgumentsForLogging(processStartInfo.Arguments)}'.");

            process = Process.Start(processStartInfo)!;
            _testRunPolicy?.OnTestApplicationStarted();
            testApplicationStarted = true;

            // Register with the Ctrl+C manager so a force-exit (second Ctrl+C) kills this process
            // tree even if the child's own cooperative cancellation hangs.
            ctrlC.Register(process);

            // Reading from process stdout/stderr is done on separate threads to avoid blocking IO on the threadpool.
            // Note: even with 'process.StandardOutput.ReadToEndAsync()' or 'process.BeginOutputReadLine()', we ended up with
            // many TP threads just doing synchronous IO, slowing down the progress of the test run.
            // We want to read requests coming through the pipe and sending responses back to the test app as fast as possible.
            // The collector is thread-safe for the timeout case.
            // In the timeout case, we leave stdOutTask and stdErrTask running, just we stop observing them.
            var stdOutBuilder = new ProcessOutputCollector(LiveOutputTailLineCount, _handler.WriteMessage);
            var stdErrBuilder = new ProcessOutputCollector(LiveOutputTailLineCount, _handler.WriteMessage);
            Volatile.Write(ref _standardOutputCollector, stdOutBuilder);
            Volatile.Write(ref _standardErrorCollector, stdErrBuilder);

            var stdOutTask = Task.Factory.StartNew(() =>
            {
                var stdOut = process.StandardOutput;
                string? currentLine;
                while ((currentLine = stdOut.ReadLine()) is not null)
                {
                    stdOutBuilder.AddLine(currentLine, GetLiveOutputStreamingState());
                }
            }, TaskCreationOptions.LongRunning);

            var stdErrTask = Task.Factory.StartNew(() =>
            {
                var stdErr = process.StandardError;
                string? currentLine;
                while ((currentLine = stdErr.ReadLine()) is not null)
                {
                    stdErrBuilder.AddLine(currentLine, GetLiveOutputStreamingState());
                }
            }, TaskCreationOptions.LongRunning);

            // WaitForExitAsync only waits for process exit (and doesn't wait for output) for our usage here.
            // If we use BeginOutputReadLine/BeginErrorReadLine, it will also wait for output which can deadlock.
            bool artifactPostProcessingTimedOut = false;
            if (_artifactPostProcessingInvocation is null)
            {
                Task processExitTask = process.WaitForExitAsync();
                if (_testRunPolicy is { } testRunPolicy)
                {
                    Task firstCompletedTask = await Task.WhenAny(processExitTask, testRunPolicy.Cancellation);
                    if (firstCompletedTask != processExitTask)
                    {
                        RequestSessionCancellation();
                        try
                        {
                            await processExitTask.WaitAsync(testRunPolicy.CancellationGracePeriod);
                        }
                        catch (TimeoutException)
                        {
                            try
                            {
                                if (!process.HasExited)
                                {
                                    process.Kill(entireProcessTree: true);
                                }
                            }
                            catch (InvalidOperationException ex)
                            {
                                // The process exited between the HasExited check and Kill.
                                Logger.LogTrace($"Test process exited before it could be killed after cancellation:\n{ex}");
                            }

                            await processExitTask;
                        }
                    }
                    else
                    {
                        await processExitTask;
                    }
                }
                else
                {
                    await processExitTask;
                }
            }
            else
            {
                try
                {
                    await process.WaitForExitAsync().WaitAsync(ArtifactPostProcessingTimeout);
                }
                catch (TimeoutException)
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync();
                    }

                    artifactPostProcessingTimedOut = true;
                }
            }

            _testRunPolicy?.OnTestApplicationExited();
            testApplicationStarted = false;

            // At this point, process already exited. Allow for 5 seconds to consume stdout/stderr.
            // We might not be able to consume all the output if the test app has exited but left a child process alive.
            try
            {
                await Task.WhenAll(stdOutTask, stdErrTask).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
            }

            if (artifactPostProcessingTimedOut)
            {
                throw new TimeoutException();
            }

            var exitCode = process.ExitCode;
            string outputToReport = stdOutBuilder.GetOutputToReport();
            string errorToReport = stdErrBuilder.GetOutputToReport();

            // Output that was streamed live is not reported again - it is already on the terminal, and
            // replaying it printed everything twice (https://github.com/dotnet/sdk/issues/55549). The trace
            // file has no console sink and is collected on its own, though, so it would silently lose that
            // output. Record it here, where the full capture is still available, for the streams the
            // handler is not going to log. Guarded so nothing is materialized unless tracing is enabled,
            // which it is not by default.
            if (Logger.TraceEnabled)
            {
                LogStreamedProcessOutput("Output Data", outputToReport, stdOutBuilder);
                LogStreamedProcessOutput("Error Data", errorToReport, stdErrBuilder);
            }

            _handler.OnTestProcessExited(exitCode, outputToReport, errorToReport);

            // This condition is to prevent considering the test app as successful when we didn't receive test session end.
            // We don't produce the exception if the exit code is already non-zero to avoid surfacing this exception when there is already a known failure.
            // For example, if hangdump timeout is reached, the process will be killed and we will have mismatching count.
            // Or if there is a crash (e.g, Environment.FailFast), etc.
            // So this is only a safe guard to avoid passing the test run if Environment.Exit(0) is called in one of the tests for example.
            if (exitCode == 0 && _handler.HasMismatchingTestSessionEventCount())
            {
                throw new InvalidOperationException(CliCommandStrings.MissingTestSessionEnd);
            }

            return exitCode;
        }
        finally
        {
            if (testApplicationStarted)
            {
                _testRunPolicy?.OnTestApplicationExited();
            }

            if (process is not null)
            {
                ctrlC.Unregister(process);
                process.Dispose();
            }

            Volatile.Write(ref _standardOutputCollector, null);
            Volatile.Write(ref _standardErrorCollector, null);

            _pipeCancellationTokenSource.Cancel();
            await Task.WhenAll(testAppPipeConnectionLoop, controlPipeConnectionLoop);
        }
    }

    internal ProcessStartInfo CreateProcessStartInfo()
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

        if (Module.LaunchSettings is ProjectLaunchProfile)
        {
            foreach (var entry in Module.LaunchSettings.EnvironmentVariables)
            {
                processStartInfo.Environment[entry.Key] = entry.Value;
            }

            if (_artifactPostProcessingInvocation is null &&
                !_buildOptions.NoLaunchProfileArguments &&
                !string.IsNullOrEmpty(Module.LaunchSettings.CommandLineArgs))
            {
                processStartInfo.Arguments = $"{processStartInfo.Arguments} {Module.LaunchSettings.CommandLineArgs}";
            }
        }

        // Command-line variables (including changes made by opted-in MSBuild targets)
        // override variables specified in the launch profile.
        foreach (var (name, value) in Module.EnvironmentVariables)
        {
            processStartInfo.Environment[name] = value;
        }

        if (Module.DotnetRootArchVariableName is not null)
        {
            processStartInfo.Environment[Module.DotnetRootArchVariableName] = Path.GetDirectoryName(new Muxer().MuxerPath);
        }

        if (TestOptions.CollectTestMap)
        {
            processStartInfo.Environment[TestOptions.AffectedTestsModeEnvironmentVariable] = TestOptions.CollectTestMapMode;
        }
        else if (TestOptions.AffectedTests)
        {
            processStartInfo.Environment[TestOptions.AffectedTestsModeEnvironmentVariable] = TestOptions.RunAffectedTestsMode;
        }
        else
        {
            processStartInfo.Environment.Remove(TestOptions.AffectedTestsModeEnvironmentVariable);
        }

        processStartInfo.Environment["DOTNET_CLI_TEST_COMMAND_WORKING_DIRECTORY"] = Directory.GetCurrentDirectory();
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
        StringBuilder builder = new(
            _artifactPostProcessingInvocation is null
                ? Module.RunProperties.Arguments
                : GetArtifactPostProcessingLaunchArguments(Module));

        if (_artifactPostProcessingInvocation is not null)
        {
            return BuildArtifactPostProcessingArguments(
                builder,
                _buildOptions.PathOptions,
                _artifactPostProcessingInvocation.ManifestPath,
                AppendTestHostTransportArguments);
        }

        if (TestOptions.IsHelp)
        {
            builder.Append($" {CliConstants.HelpOptionKey}");
        }

        if (TestOptions.IsDiscovery)
        {
            builder.Append($" {TestCommandDefinition.MicrosoftTestingPlatform.ListTestsOptionName}");
        }

        if (TestOptions.CollectTestMap && !TestOptions.CollectTestMapForwarded)
        {
            builder.Append($" {TestCommandDefinition.MicrosoftTestingPlatform.CollectTestMapOptionName}");
        }

        if (TestOptions.AffectedTests && !TestOptions.AffectedTestsForwarded)
        {
            builder.Append($" {TestCommandDefinition.MicrosoftTestingPlatform.AffectedTestsOptionName}");
        }

        if (_resultsDirectoryResolver.Resolve(Module) is { } resultsDirectoryPath)
        {
            builder.Append($" {TestCommandDefinition.MicrosoftTestingPlatform.ResultsDirectoryOptionName} {ArgumentEscaper.EscapeSingleArg(resultsDirectoryPath)}");
        }

        if (_buildOptions.PathOptions.ConfigFilePath is { } configFilePath)
        {
            builder.Append($" {TestCommandDefinition.MicrosoftTestingPlatform.ConfigFileOptionName} {ArgumentEscaper.EscapeSingleArg(configFilePath)}");
        }

        if (_buildOptions.PathOptions.DiagnosticOutputDirectoryPath is { } diagnosticOutputDirectoryPath)
        {
            builder.Append($" {TestCommandDefinition.MicrosoftTestingPlatform.DiagnosticOutputDirectoryOptionName} {ArgumentEscaper.EscapeSingleArg(diagnosticOutputDirectoryPath)}");
        }

        foreach (var arg in _buildOptions.TestApplicationArguments)
        {
            builder.Append($" {ArgumentEscaper.EscapeSingleArg(arg)}");
        }

        AppendTestHostTransportArguments(builder);

        return builder.ToString();
    }

    private void AppendTestHostTransportArguments(StringBuilder builder)
    {
        if (RequiresHttpTransport(Module))
        {
            _httpGateway ??= new HttpTestHostGateway(
                OnHttpRequest,
                _pipeCancellationTokenSource.Token);
            _httpResponseFilePath ??= CreateHttpTransportResponseFile(_httpGateway);
            builder.Append($" {ArgumentEscaper.EscapeSingleArg("@" + _httpResponseFilePath)}");
        }
        else
        {
            builder.Append($" {CliConstants.ServerOptionKey} {CliConstants.ServerOptionValue}");
            builder.Append($" {CliConstants.DotNetTestPipeOptionKey} {ArgumentEscaper.EscapeSingleArg(_pipeName)}");
        }
    }

    internal static bool RequiresHttpTransport(TestModule module)
    {
        string runtimeIdentifier = module.RunProperties.RuntimeIdentifier;
        return runtimeIdentifier.StartsWith("browser-", StringComparison.OrdinalIgnoreCase) ||
            runtimeIdentifier.StartsWith("wasi-", StringComparison.OrdinalIgnoreCase);
    }

    internal string? HttpResponseFilePath => _httpResponseFilePath;

    private static string CreateHttpTransportResponseFile(HttpTestHostGateway gateway)
    {
        string path = Path.Combine(Path.GetTempPath(), $"dotnet-test-http-{Guid.NewGuid():N}.rsp");
        try
        {
            FileStream stream;
            if (OperatingSystem.IsWindows())
            {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                SecurityIdentifier currentUser = identity.User
                    ?? throw new InvalidOperationException("Unable to determine the current Windows user.");
                var security = new FileSecurity();
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                security.AddAccessRule(new FileSystemAccessRule(
                    currentUser,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));
                stream = FileSystemAclExtensions.Create(
                    new FileInfo(path),
                    FileMode.CreateNew,
                    FileSystemRights.FullControl,
                    FileShare.None,
                    bufferSize: 4096,
                    FileOptions.WriteThrough,
                    security);
            }
            else
            {
                stream = new FileStream(path, new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    BufferSize = 4096,
                    Options = FileOptions.WriteThrough,
                    UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
                });
            }

            using (stream)
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.WriteLine($"{CliConstants.ServerOptionKey} {CliConstants.ServerOptionValue}");
                writer.WriteLine($"{CliConstants.DotNetTestTransportOptionKey} {CliConstants.DotNetTestHttpTransportValue}");
                writer.WriteLine($"{CliConstants.DotNetTestHttpEndpointOptionKey} {gateway.Endpoint.AbsoluteUri}");
                writer.WriteLine($"{CliConstants.DotNetTestHttpTokenOptionKey} {gateway.Token}");
            }

            return path;
        }
        catch
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception cleanupException)
            {
                Logger.LogTrace($"Failed to clean up the dotnet test HTTP transport response file after creation failed: {cleanupException}");
            }

            throw;
        }
    }

    internal string GetArgumentsForLogging(string arguments)
    {
        if (_httpGateway is null)
        {
            return arguments;
        }

        string redactedEndpoint = $"{_httpGateway.Endpoint.GetLeftPart(UriPartial.Authority)}/[REDACTED]";
        return arguments
            .Replace(_httpGateway.Endpoint.AbsoluteUri, redactedEndpoint, StringComparison.Ordinal)
            .Replace(_httpGateway.Token, "[REDACTED]", StringComparison.Ordinal);
    }

    internal static string GetArtifactPostProcessingLaunchArguments(TestModule module)
        => string.Equals(
            Path.GetFileNameWithoutExtension(module.RunProperties.Command),
            "dotnet",
            StringComparison.OrdinalIgnoreCase)
            ? $"exec {ArgumentEscaper.EscapeSingleArg(module.TargetPath)}"
            : string.Empty;

    /// <summary>
    /// Appends the arguments that put a relaunched test application into merge-host mode. None of the
    /// options describing the test run itself are forwarded — the host discovers nothing and runs no
    /// tests — but the options deciding which extensions load, and where they write diagnostics, must
    /// match the run that produced the artifacts.
    /// </summary>
    internal static string BuildArtifactPostProcessingArguments(
        StringBuilder builder,
        PathOptions pathOptions,
        string manifestPath,
        string pipeName)
        => BuildArtifactPostProcessingArguments(
            builder,
            pathOptions,
            manifestPath,
            transportArgumentsBuilder => transportArgumentsBuilder.Append(
                $" {CliConstants.ServerOptionKey} {CliConstants.ServerOptionValue} {CliConstants.DotNetTestPipeOptionKey} {ArgumentEscaper.EscapeSingleArg(pipeName)}"));

    private static string BuildArtifactPostProcessingArguments(
        StringBuilder builder,
        PathOptions pathOptions,
        string manifestPath,
        Action<StringBuilder> appendTransportArguments)
    {
        builder.Append($" {CliConstants.ArtifactPostProcessingToolName}");
        builder.Append($" {CliConstants.ArtifactPostProcessingManifestOptionKey} {ArgumentEscaper.EscapeSingleArg(manifestPath)}");

        // A merge host resolves and enables its extensions exactly like a test host does, so a
        // configuration file that governs which extensions load has to reach it too. Without this
        // the merge runs with a different extension set than the run that produced the artifacts,
        // and a post-processor disabled by configuration silently comes back for the merge.
        if (pathOptions.ConfigFilePath is { } configFilePath)
        {
            builder.Append($" {TestCommandDefinition.MicrosoftTestingPlatform.ConfigFileOptionName} {ArgumentEscaper.EscapeSingleArg(configFilePath)}");
        }

        if (pathOptions.DiagnosticOutputDirectoryPath is { } diagnosticOutputDirectoryPath)
        {
            builder.Append($" {TestCommandDefinition.MicrosoftTestingPlatform.DiagnosticOutputDirectoryOptionName} {ArgumentEscaper.EscapeSingleArg(diagnosticOutputDirectoryPath)}");
        }

        // The results directory is deliberately not forwarded: the merged output location travels in
        // the manifest instead, so the SDK keeps control of it even when it has to be derived.
        appendTransportArguments(builder);
        return builder.ToString();
    }

    /// <summary>
    /// Resolves how long a relaunched merge host may run before it is killed. The bound exists so a
    /// hung merge host cannot hold an already-finished test run open indefinitely, but no single
    /// value fits every repository — merging the coverage of a large solution legitimately takes far
    /// longer than merging two TRX reports. The override also accepts '0' to remove the bound, which
    /// is what makes it possible to attach a debugger to a merge host.
    /// </summary>
    private static TimeSpan GetArtifactPostProcessingTimeout()
        => ParseArtifactPostProcessingTimeout(
            Environment.GetEnvironmentVariable(CliConstants.TestArtifactPostProcessingTimeoutEnvVar));

    internal static TimeSpan ParseArtifactPostProcessingTimeout(string? configuredTimeout)
    {
        // Parsed as long rather than int so that a value chosen to mean 'a very long time' lands in
        // the 'effectively never' branch below instead of overflowing the parse and silently falling
        // back to the default. Anything above long.MaxValue seconds is not a duration anyone means,
        // so it keeps the default along with the other unusable values.
        if (!long.TryParse(configuredTimeout, NumberStyles.Integer, CultureInfo.InvariantCulture, out long seconds) || seconds < 0)
        {
            return DefaultArtifactPostProcessingTimeout;
        }

        // Task.WaitAsync rejects a timeout above Timer.MaxSupportedTimeout (~49.7 days) with an
        // ArgumentOutOfRangeException, which would escape the TimeoutException-only catch around the
        // wait and fail every merge. A caller asking for a timeout that long means "effectively
        // never", so give them that instead of a broken run.
        return seconds == 0 || seconds > MaximumArtifactPostProcessingTimeoutSeconds
            ? Timeout.InfiniteTimeSpan
            : TimeSpan.FromSeconds(seconds);
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
                AddPipeConnection(pipeConnection);
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

    private async Task WaitControlConnectionAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServer? pipeConnection = new(
                    _controlPipeName,
                    OnControlRequest,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    token,
                    skipUnknownMessages: false);
                try
                {
                    pipeConnection.RegisterAllSerializers();

                    await pipeConnection.WaitConnectionAsync(token);
                    AddPipeConnection(pipeConnection);
                    pipeConnection = null;
                }
                finally
                {
                    pipeConnection?.Dispose();
                }
            }
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == token)
        {
            Logger.LogTrace("WaitControlConnectionAsync() was cancelled.");
        }
        catch (Exception ex)
        {
            // The reverse channel is an optional capability. A failure here must not replace
            // the test application's result; policy cancellation can still use the kill fallback.
            Logger.LogTrace($"The server-control pipe stopped accepting connections:\n{ex}");
        }
    }

    private Task<IResponse> OnControlRequest(NamedPipeServer _, IRequest request)
    {
        if (request is not WaitForServerControlRequest)
        {
            throw new NotSupportedException(string.Format(CliCommandStrings.CmdUnsupportedMessageRequestTypeException, request.GetType()));
        }

        lock (_controlRequestLock)
        {
            if (Volatile.Read(ref _sessionCancellationRequested) != 0)
            {
                return Task.FromResult<IResponse>(new ServerControlMessage(ServerControlKinds.CancelSession));
            }

            var completion = new TaskCompletionSource<IResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingControlRequests.Add(completion);
            _pipeCancellationTokenSource.Token.Register(
                () => completion.TrySetCanceled(_pipeCancellationTokenSource.Token));
            return completion.Task;
        }
    }

    private void AddPipeConnection(NamedPipeServer pipeConnection)
    {
        lock (_pipeConnectionsLock)
        {
            _testAppPipeConnections.Add(pipeConnection);
        }
    }

    private void RequestSessionCancellation()
    {
        if (Interlocked.Exchange(ref _sessionCancellationRequested, 1) != 0)
        {
            return;
        }

        lock (_controlRequestLock)
        {
            var message = new ServerControlMessage(ServerControlKinds.CancelSession);
            foreach (TaskCompletionSource<IResponse> request in _pendingControlRequests)
            {
                request.TrySetResult(message);
            }
        }
    }

    private Task<IResponse> OnHttpRequest(IRequest request)
    {
        if (request is WaitForServerControlRequest)
        {
            Logger.LogTrace("The dotnet test HTTP transport does not support the reverse server-control channel.");
            return Task.FromResult<IResponse>(VoidResponse.CachedInstance);
        }

        return OnRequest(server: null, request);
    }

    private Task<IResponse> OnRequest(NamedPipeServer? server, IRequest request)
    {
        // We need to lock as we might be called concurrently when test app child processes all communicate with us.
        // For example, in a case of a sharding extension, we could get test result messages concurrently.
        // To be the most safe, we lock the whole OnRequest.
        lock (_requestLock)
        {
            try
            {
                switch (request)
                {
                    case HandshakeMessage handshakeMessage:
                        if (server is not null && !_handshakes.TryAdd(server, handshakeMessage))
                        {
                            throw new InvalidOperationException(CliCommandStrings.DotnetTestDuplicateHandshakeOnConnection);
                        }
                        string negotiatedVersion = GetSupportedProtocolVersion(handshakeMessage);
                        // If the handler rejects the handshake (unsupported version, missing required
                        // properties, mismatching info, ...) respond with an empty negotiated version so
                        // Microsoft.Testing.Platform stops sending further messages on this connection.
                        bool handshakeAccepted = OnHandshakeMessage(handshakeMessage, negotiatedVersion.Length > 0);
                        SetNegotiatedProtocolVersion(handshakeAccepted ? negotiatedVersion : string.Empty);
                        return Task.FromResult((IResponse)CreateHandshakeMessage(
                            handshakeAccepted ? negotiatedVersion : string.Empty,
                            includeControlPipe: _httpGateway is null));

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

                    case TestInProgressMessages testInProgressMessages:
                        OnTestInProgressMessages(testInProgressMessages);
                        break;

                    case TestSessionEvent sessionEvent:
                        OnSessionEvent(sessionEvent);
                        break;

                    case AzureDevOpsLogMessage azureDevOpsLogMessage:
                        OnAzureDevOpsLogMessage(azureDevOpsLogMessage);
                        break;

                    case DisplayMessage displayMessage:
                        OnDisplayMessage(displayMessage);
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
    }

    internal static string GetSupportedProtocolVersion(HandshakeMessage handshakeMessage)
    {
        if (!handshakeMessage.Properties.TryGetValue(HandshakeMessagePropertyNames.SupportedProtocolVersions, out string? protocolVersions) ||
            string.IsNullOrWhiteSpace(protocolVersions))
        {
            // The handshake didn't advertise any supported protocol versions. Return empty so the
            // handler can surface a dedicated "missing protocol versions" failure to the user via
            // 'HandshakeFailure' (rather than throwing here, which would route to 'FailFast').
            return string.Empty;
        }

        List<(Version Version, string Text)> sdkSupportedVersions = [];
        foreach (string supportedVersion in ProtocolConstants.SupportedVersions.Split(';'))
        {
            string trimmedSupportedVersion = supportedVersion.Trim();
            if (Version.TryParse(trimmedSupportedVersion, out Version? parsedSupportedVersion))
            {
                sdkSupportedVersions.Add((parsedSupportedVersion, trimmedSupportedVersion));
            }
        }

        Version? highestCommonVersion = null;
        string highestCommonVersionText = string.Empty;
        foreach (string advertisedVersion in protocolVersions.Split(';'))
        {
            if (!Version.TryParse(advertisedVersion.Trim(), out Version? parsedAdvertisedVersion))
            {
                continue;
            }

            foreach ((Version sdkSupportedVersion, string sdkSupportedVersionText) in sdkSupportedVersions)
            {
                if (parsedAdvertisedVersion.Equals(sdkSupportedVersion) &&
                    (highestCommonVersion is null || sdkSupportedVersion.CompareTo(highestCommonVersion) > 0))
                {
                    highestCommonVersion = sdkSupportedVersion;
                    highestCommonVersionText = sdkSupportedVersionText;
                }
            }
        }

        return highestCommonVersionText;
    }

    private HandshakeMessage CreateHandshakeMessage(string version, bool includeControlPipe = true)
    {
        var properties = new Dictionary<byte, string>(capacity: 6)
        {
            { HandshakeMessagePropertyNames.PID, Environment.ProcessId.ToString(CultureInfo.InvariantCulture) },
            { HandshakeMessagePropertyNames.Architecture, RuntimeInformation.ProcessArchitecture.ToString() },
            { HandshakeMessagePropertyNames.Framework, RuntimeInformation.FrameworkDescription },
            { HandshakeMessagePropertyNames.OS, RuntimeInformation.OSDescription },
            { HandshakeMessagePropertyNames.SupportedProtocolVersions, version }
        };

        if (version.Length > 0 && includeControlPipe)
        {
            properties.Add(HandshakeMessagePropertyNames.ServerControlPipeName, _controlPipeName);
        }

        return new HandshakeMessage(properties);
    }

    private void SetNegotiatedProtocolVersion(string negotiatedVersion)
    {
        if (Version.TryParse(negotiatedVersion, out Version? parsedNegotiatedVersion) &&
            (_negotiatedProtocolVersion is null || parsedNegotiatedVersion.CompareTo(_negotiatedProtocolVersion) > 0))
        {
            _negotiatedProtocolVersion = parsedNegotiatedVersion;
        }

        Volatile.Write(ref _protocolNegotiated, 1);
        FlushBufferedOutputIfLiveStreamingEnabled();
    }

    private bool? GetLiveOutputStreamingState() =>
        Volatile.Read(ref _protocolNegotiated) == 0 ? null : IsProtocol_1_1_OrHigher;

    /// <summary>
    /// Records process output that is not being reported to the user because it already reached the
    /// terminal as live output, so that an enabled trace file still contains it.
    /// </summary>
    private static void LogStreamedProcessOutput(string label, string reportedOutput, ProcessOutputCollector collector)
    {
        if (reportedOutput.Length > 0)
        {
            // The output is being reported, so the handler traces it as usual.
            return;
        }

        string capturedOutput = collector.GetCapturedOutput();
        if (capturedOutput.Length > 0)
        {
            Logger.LogTrace($"{label} (already streamed live): {capturedOutput}");
        }
    }

    private void FlushBufferedOutputIfLiveStreamingEnabled()
    {
        bool? liveOutputStreamingState = GetLiveOutputStreamingState();
        Volatile.Read(ref _standardOutputCollector)?.FlushBufferedOutputIfLiveStreamingEnabled(liveOutputStreamingState);
        Volatile.Read(ref _standardErrorCollector)?.FlushBufferedOutputIfLiveStreamingEnabled(liveOutputStreamingState);
    }

    public bool OnHandshakeMessage(HandshakeMessage handshakeMessage, bool gotSupportedVersion)
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

    private void OnTestInProgressMessages(TestInProgressMessages testInProgressMessages)
        => _handler.OnTestInProgressReceived(testInProgressMessages);

    private void OnSessionEvent(TestSessionEvent sessionEvent)
        => _handler.OnSessionEventReceived(sessionEvent);

    private void OnAzureDevOpsLogMessage(AzureDevOpsLogMessage azureDevOpsLogMessage)
        => _handler.OnAzureDevOpsLogReceived(azureDevOpsLogMessage);

    private void OnDisplayMessage(DisplayMessage displayMessage)
        => _handler.OnDisplayMessageReceived(displayMessage);

    internal sealed class ProcessOutputCollector(int liveOutputTailLineCount, Action<string> writeOutput)
    {
        // Serializes "decide what to write, then write it" so live output reaches the terminal in the
        // order the test process produced it. Without it the protocol-negotiation flush and a reader
        // thread can interleave and put a later line ahead of the buffered lines that precede it, and
        // live output is the only copy the user gets. This is deliberately separate from _lock so a
        // capture read never waits on terminal IO - RunAsync reads the capture on a timeout path that
        // exists precisely because a reader may be stuck. Always taken before _lock, never after.
        private readonly object _writeLock = new();
        private readonly object _lock = new();
        private readonly Queue<string> _lines = [];
        private bool _liveStreamingEnabled;

        public void AddLine(string line, bool? liveOutputStreamingState)
        {
            lock (_writeLock)
            {
                string outputToWrite;
                lock (_lock)
                {
                    _lines.Enqueue(line);

                    if (_liveStreamingEnabled)
                    {
                        // The caller sampled the streaming state before taking this lock, so it can be
                        // stale: negotiation may have enabled streaming in between. Once streaming is on
                        // it never turns back off, and the capture is reported as already shown, so this
                        // line has to be written even when the stale sample says otherwise - skipping it
                        // would drop it from the terminal entirely.
                        outputToWrite = line + Environment.NewLine;
                    }
                    else
                    {
                        if (liveOutputStreamingState != true)
                        {
                            return;
                        }

                        _liveStreamingEnabled = true;
                        outputToWrite = JoinLinesWithTrailingNewLine(_lines);
                    }

                    TrimToBoundedTail();
                }

                writeOutput(outputToWrite);
            }
        }

        public void FlushBufferedOutputIfLiveStreamingEnabled(bool? liveOutputStreamingState)
        {
            lock (_writeLock)
            {
                string outputToWrite;
                lock (_lock)
                {
                    if (liveOutputStreamingState != true || _liveStreamingEnabled)
                    {
                        return;
                    }

                    _liveStreamingEnabled = true;
                    outputToWrite = JoinLinesWithTrailingNewLine(_lines);
                    TrimToBoundedTail();
                }

                if (outputToWrite.Length > 0)
                {
                    writeOutput(outputToWrite);
                }
            }
        }

        /// <summary>
        /// Returns the captured tail of the stream for the caller to report, or an empty string when
        /// that content already reached the terminal as live output.
        /// </summary>
        /// <remarks>
        /// Live streaming engages only once for a stream, and everything buffered up to that point is
        /// flushed in the same step, so from then on the whole capture - including lines later evicted
        /// by <see cref="TrimToBoundedTail"/> - has been shown. Reporting it again would print it twice
        /// (https://github.com/dotnet/sdk/issues/55549). Until streaming engages the capture is the only
        /// copy there is, so it is returned in full: older Microsoft.Testing.Platform versions that
        /// don't negotiate protocol 1.1.0, and processes that fail before the handshake, depend on it.
        /// </remarks>
        public string GetOutputToReport()
        {
            lock (_lock)
            {
                return _liveStreamingEnabled ? string.Empty : string.Join(Environment.NewLine, _lines);
            }
        }

        /// <summary>
        /// Returns the captured tail of the stream whether or not it was already streamed live. Only for
        /// diagnostics that record the output without showing it to the user - anything rendered on the
        /// terminal must go through <see cref="GetOutputToReport"/> instead, or it will be printed twice.
        /// </summary>
        public string GetCapturedOutput()
        {
            lock (_lock)
            {
                return string.Join(Environment.NewLine, _lines);
            }
        }

        private void TrimToBoundedTail()
        {
            while (_lines.Count > liveOutputTailLineCount)
            {
                _lines.Dequeue();
            }
        }

        private static string JoinLinesWithTrailingNewLine(IEnumerable<string> lines)
        {
            StringBuilder builder = new();
            foreach (string line in lines)
            {
                builder.AppendLine(line);
            }

            return builder.ToString();
        }
    }

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

        _httpGateway?.Dispose();
        if (_httpResponseFilePath is not null)
        {
            try
            {
                File.Delete(_httpResponseFilePath);
            }
            catch (Exception ex)
            {
                Logger.LogTrace($"Failed to delete the dotnet test HTTP transport response file: {ex}");
            }
        }

        _pipeCancellationTokenSource.Dispose();
    }
}
