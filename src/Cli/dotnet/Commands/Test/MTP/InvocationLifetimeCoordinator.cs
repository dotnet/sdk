// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal enum InvocationLifetimeSiblingLaunchKind
{
    /// <summary>Coordination was not established before any setup started; the module self-hosts its handlers (Phase 1).</summary>
    Independent,

    /// <summary>The owner completed invocation setup; the module receives its outputs and the coordinated handler UIDs.</summary>
    Coordinated,

    /// <summary>Coordination failed after setup might have started; the module must not be launched.</summary>
    Skip,
}

/// <param name="Outputs">Values may be secrets and must never be logged.</param>
/// <param name="CoordinatedHandlers">Compact JSON string array of the handler UIDs hosted by the owner.</param>
internal sealed record InvocationLifetimeSiblingLaunch(
    InvocationLifetimeSiblingLaunchKind Kind,
    IReadOnlyList<InvocationLifetimeOutput> Outputs,
    string? CoordinatedHandlers)
{
    internal static readonly InvocationLifetimeSiblingLaunch Independent = new(InvocationLifetimeSiblingLaunchKind.Independent, [], null);
    internal static readonly InvocationLifetimeSiblingLaunch Skip = new(InvocationLifetimeSiblingLaunchKind.Skip, [], null);
}

/// <summary>
/// Coordinates one invocation lifetime owner with the other modules of a <c>dotnet test</c> invocation
/// (microsoft/testfx RFC 024, sections 8.1-8.4).
/// </summary>
/// <remarks>
/// Before the owner accepts coordination, every failure falls back to Phase 1 because no setup can have started.
/// Once the SDK has acknowledged the owner's handshake, setup might have started, so every failure fails the run
/// and modules that were not launched yet are skipped.
/// </remarks>
internal sealed class InvocationLifetimeCoordinator : IDisposable
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(15);
    private const int MaximumTimeoutSeconds = (int)(0xFFFFFFFE / 1000);

    private enum State
    {
        Pending,
        FallenBack,
        Accepted,
        SetupSucceeded,
        TestsCompleted,
        CleanupRequested,
        Failed,
    }

    private readonly Lock _lock = new();
    private readonly TimeProvider _timeProvider;
    private readonly Action<string> _reportWarning;
    private readonly Action<string> _reportError;
    private readonly string _ownerName;
    private readonly TaskCompletionSource<InvocationLifetimeSiblingLaunch> _siblingLaunch = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<int> _moduleTestsCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<InvocationCleanupCompleted> _cleanupCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _ownerAbort = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _siblingAbort = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _ownerRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private State _state = State.Pending;
    private bool _setupSucceeded;
    private bool _ownerControlLost;
    private string? _coordinatedHandlers;
    private ITimer? _readinessTimer;
    private TestApplication? _detachedOwner;
    private Task<int>? _detachedOwnerRun;

    public InvocationLifetimeCoordinator(
        TestModule owner,
        TimeSpan timeout,
        TimeProvider? timeProvider = null,
        Action<string>? reportWarning = null,
        Action<string>? reportError = null)
    {
        Owner = owner;
        Timeout = timeout;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _reportWarning = reportWarning ?? (static (string message) => Reporter.Output.WriteLine(message.Yellow()));
        _reportError = reportError ?? (static (string message) => Reporter.Error.WriteLine(message.Red()));
        _ownerName = Path.GetFileNameWithoutExtension(owner.TargetPath);
    }

    public TestModule Owner { get; }

    public TimeSpan Timeout { get; }

    /// <summary>Completes with the owner's module exit code once its tests are done and it waits for cleanup.</summary>
    public Task<int> ModuleTestsCompletedTask => _moduleTestsCompleted.Task;

    /// <summary>Completes when the owner process tree must be terminated.</summary>
    public Task OwnerAbort => _ownerAbort.Task;

    /// <summary>Completes when running sibling modules must be canceled because the owner can no longer clean up.</summary>
    public Task SiblingAbort => _siblingAbort.Task;

    /// <summary>
    /// Whether the owner committed to coordination. A coordinated owner must still be able to receive cleanup
    /// after it handled a session cancellation.
    /// </summary>
    public bool IsOwnerCoordinated
    {
        get
        {
            lock (_lock)
            {
                return _state is State.Accepted or State.SetupSucceeded or State.TestsCompleted or State.CleanupRequested;
            }
        }
    }

    /// <summary>
    /// Whether coordination failed after the owner accepted it. Modules that were not launched yet must not be
    /// launched, even if invocation setup had already succeeded.
    /// </summary>
    public bool HasFailed
    {
        get
        {
            lock (_lock)
            {
                return _state == State.Failed;
            }
        }
    }

    internal static TimeSpan GetTimeout()
        => ParseTimeout(Environment.GetEnvironmentVariable(InvocationLifetimeEnvironmentVariables.TimeoutSeconds));

    internal static TimeSpan ParseTimeout(string? configuredTimeout)
    {
        if (!long.TryParse(configuredTimeout, NumberStyles.Integer, CultureInfo.InvariantCulture, out long seconds) || seconds < 0)
        {
            return DefaultTimeout;
        }

        return seconds == 0 || seconds > MaximumTimeoutSeconds
            ? System.Threading.Timeout.InfiniteTimeSpan
            : TimeSpan.FromSeconds(seconds);
    }

    public Task<InvocationLifetimeSiblingLaunch> WaitForSiblingLaunchAsync(CancellationToken cancellationToken)
        => _siblingLaunch.Task.WaitAsync(cancellationToken);

    /// <summary>Starts the readiness deadline. Called right before the owner is launched.</summary>
    public void OnOwnerLaunching()
    {
        if (Timeout == System.Threading.Timeout.InfiniteTimeSpan)
        {
            return;
        }

        lock (_lock)
        {
            if (_state is State.Pending or State.Accepted && _readinessTimer is null)
            {
                _readinessTimer = _timeProvider.CreateTimer(
                    static state => ((InvocationLifetimeCoordinator)state!).OnReadinessTimeout(),
                    this,
                    Timeout,
                    System.Threading.Timeout.InfiniteTimeSpan);
            }
        }
    }

    /// <summary>
    /// Decides whether the owner's handshake commits to coordination. Returns <see langword="true"/> only for the
    /// one handshake whose reply must carry <see cref="HandshakeMessagePropertyNames.InvocationLifetimeCoordinationAccepted"/>.
    /// </summary>
    /// <param name="negotiatedVersion">Empty when the handshake was rejected.</param>
    public bool OnOwnerHandshake(HandshakeMessage handshake, string negotiatedVersion, bool controlPipeAvailable)
    {
        lock (_lock)
        {
            if (_state != State.Pending)
            {
                if (handshake.Properties.ContainsKey(HandshakeMessagePropertyNames.InvocationLifetimeHandlers))
                {
                    Logger.LogTrace($"Ignoring an invocation lifetime handshake from '{_ownerName}' in state '{_state}'.");
                }

                return false;
            }

            if (!Version.TryParse(negotiatedVersion, out Version? version) || version < ProtocolConstants.InvocationLifetimeVersion)
            {
                FallBack($"negotiated protocol version '{negotiatedVersion}' does not support coordination");
                return false;
            }

            if (!controlPipeAvailable)
            {
                FallBack("the server-control channel is unavailable");
                return false;
            }

            if (!handshake.Properties.TryGetValue(HandshakeMessagePropertyNames.InvocationLifetimeHandlers, out string? handlersJson))
            {
                handshake.Properties.TryGetValue(HandshakeMessagePropertyNames.HostType, out string? hostType);
                if (hostType == HandshakeMessageHostTypes.TestHost)
                {
                    FallBack("the test host enabled no invocation lifetime handler");
                }

                // Controllers such as test host orchestrators do not host handlers themselves.
                return false;
            }

            if (!TryParseHandlers(handlersJson, out IReadOnlyList<string>? handlers))
            {
                FallBack("the advertised invocation lifetime handlers are invalid");
                return false;
            }

            if (Owner.InvocationLifetimeHandlers is { } declaredHandlers
                && !new HashSet<string>(handlers, StringComparer.Ordinal).IsSupersetOf(declaredHandlers))
            {
                FallBack("the test host did not enable every declared invocation lifetime handler");
                return false;
            }

            _coordinatedHandlers = SerializeHandlers(handlers);
            _state = State.Accepted;
            Logger.LogTrace($"Accepted invocation lifetime coordination from '{_ownerName}' for handlers {_coordinatedHandlers}.");
            return true;
        }
    }

    public void OnSetupCompleted(InvocationSetupCompleted message)
    {
        lock (_lock)
        {
            if (_state != State.Accepted)
            {
                FailUnexpectedMessage(nameof(InvocationSetupCompleted));
                return;
            }

            StopReadinessTimer();
            if (!message.Succeeded)
            {
                Fail(AppendDetails(string.Format(CliCommandStrings.InvocationLifetimeSetupFailed, _ownerName), message.ErrorMessage), abortOwner: false);
                return;
            }

            _state = State.SetupSucceeded;
            _setupSucceeded = true;

            // Output values are intentionally not traced.
            Logger.LogTrace($"Invocation setup completed in '{_ownerName}' with {message.Outputs.Length} output(s).");
            _siblingLaunch.TrySetResult(new InvocationLifetimeSiblingLaunch(
                InvocationLifetimeSiblingLaunchKind.Coordinated,
                message.Outputs,
                _coordinatedHandlers));
        }
    }

    /// <returns>
    /// A task that completes when the owner may receive the reply to <see cref="ModuleTestsCompleted"/>: once every
    /// other module completed. The owner cannot clean up while it waits for this reply, even if its server-control
    /// connection is lost, so it never releases the resources that running modules still use.
    /// </returns>
    public Task OnModuleTestsCompleted(ModuleTestsCompleted message)
    {
        lock (_lock)
        {
            if (_state == State.SetupSucceeded)
            {
                _state = State.TestsCompleted;
            }
            else if (!(_state == State.Failed && _ownerControlLost && _setupSucceeded))
            {
                FailUnexpectedMessage(nameof(ModuleTestsCompleted));
                return Task.CompletedTask;
            }

            // After a lost control connection, the coordination already failed, but the owner is still held until the
            // modules that run with its setup outputs complete.
            _moduleTestsCompleted.TrySetResult(message.ExitCode);
            return _ownerRelease.Task;
        }
    }

    /// <summary>
    /// Called when the owner's dedicated server-control connection closes while the owner is alive. The owner can no
    /// longer receive the invocation cleanup request, so no other module is launched any more. Running modules are
    /// not canceled: the owner is held at <see cref="ModuleTestsCompleted"/> until they complete.
    /// </summary>
    public void OnOwnerControlLost()
    {
        lock (_lock)
        {
            if (_state is not (State.Accepted or State.SetupSucceeded or State.TestsCompleted))
            {
                Logger.LogTrace($"The invocation lifetime control connection of '{_ownerName}' closed in state '{_state}'.");
                return;
            }

            _ownerControlLost = true;
            Fail(string.Format(CliCommandStrings.InvocationLifetimeOwnerControlLost, _ownerName), abortOwner: false, abortSiblings: false);
        }
    }

    public void OnCleanupCompleted(InvocationCleanupCompleted message)
    {
        lock (_lock)
        {
            if (_state != State.CleanupRequested)
            {
                FailUnexpectedMessage(nameof(InvocationCleanupCompleted));
                return;
            }

            _cleanupCompleted.TrySetResult(message);
        }
    }

    /// <summary>Called when the owner's <see cref="TestApplication.RunAsync"/> completes, whatever its outcome.</summary>
    public void OnOwnerExited()
    {
        lock (_lock)
        {
            StopReadinessTimer();
            switch (_state)
            {
                case State.Pending:
                    FallBack("the test host exited before requesting coordination");
                    break;

                case State.Accepted:
                    Fail(string.Format(CliCommandStrings.InvocationLifetimeOwnerExitedBeforeSetup, _ownerName), abortOwner: false);
                    break;

                case State.SetupSucceeded:
                case State.TestsCompleted:
                    // An owner that exits before cleanup can no longer release what the other modules use.
                    Fail(string.Format(CliCommandStrings.InvocationLifetimeOwnerExitedBeforeCleanup, _ownerName), abortOwner: false);
                    break;

                case State.Failed when _ownerControlLost && !_ownerRelease.Task.IsCompleted:
                    // The owner was held until the other modules complete, but it is gone: so might be its resources.
                    _siblingAbort.TrySetResult();
                    break;
            }
        }
    }

    /// <summary>Takes ownership of an owner that is still running and waits for invocation cleanup.</summary>
    public void AttachDetachedOwner(TestApplication owner, Task<int> run)
    {
        lock (_lock)
        {
            _detachedOwner = owner;
            _detachedOwnerRun = run;
        }
    }

    /// <summary>
    /// Runs invocation cleanup after every module completed and folds coordination failures into the aggregate exit code.
    /// </summary>
    public async Task<int> CompleteAsync(int aggregateExitCode, bool isCanceled)
    {
        TestApplication? owner;
        Task<int>? ownerRun;
        bool requestCleanup;
        lock (_lock)
        {
            StopReadinessTimer();
            owner = _detachedOwner;
            ownerRun = _detachedOwnerRun;
            requestCleanup = _state == State.TestsCompleted && owner is not null;
            if (requestCleanup)
            {
                _state = State.CleanupRequested;
            }
            else if (_state == State.Pending)
            {
                // No owner handshake can be accepted any more.
                _state = State.FallenBack;
            }

            // Release modules that could still be waiting, for example after cancellation.
            _siblingLaunch.TrySetResult(_state == State.FallenBack ? InvocationLifetimeSiblingLaunch.Independent : InvocationLifetimeSiblingLaunch.Skip);
        }

        bool cleanupFailed = false;
        if (owner is not null && ownerRun is not null)
        {
            try
            {
                if (requestCleanup)
                {
                    owner.RequestInvocationCleanup(aggregateExitCode, isCanceled);
                }

                // Every other module completed: the owner may now proceed to its cleanup.
                _ownerRelease.TrySetResult();
                if (requestCleanup)
                {
                    cleanupFailed = !await WaitForCleanupAsync(ownerRun);
                }

                if (!await WaitForOwnerExitAsync(ownerRun))
                {
                    cleanupFailed = true;
                }
            }
            finally
            {
                owner.Dispose();
                cleanupFailed |= owner.HasFailureDuringDispose;
            }
        }

        // Nothing waits for the other modules any more.
        _ownerRelease.TrySetResult();

        bool failed;
        lock (_lock)
        {
            failed = cleanupFailed || _state == State.Failed;
            if (failed)
            {
                _state = State.Failed;
            }
        }

        return failed && aggregateExitCode is ExitCode.Success or ExitCode.ZeroTests
            ? ExitCode.GenericFailure
            : aggregateExitCode;
    }

    private async Task<bool> WaitForCleanupAsync(Task<int> ownerRun)
    {
        Task cleanup = _cleanupCompleted.Task;
        Task completed = await WhenAnyWithTimeout(cleanup, ownerRun);
        if (completed == cleanup)
        {
            InvocationCleanupCompleted message = await _cleanupCompleted.Task;
            if (message.Succeeded)
            {
                return true;
            }

            _reportError(AppendDetails(string.Format(CliCommandStrings.InvocationLifetimeCleanupFailed, _ownerName), message.ErrorMessage));
            return false;
        }

        if (completed == ownerRun)
        {
            _reportError(string.Format(CliCommandStrings.InvocationLifetimeOwnerExitedBeforeCleanup, _ownerName));
            return false;
        }

        _reportError(string.Format(
            CliCommandStrings.InvocationLifetimeCleanupTimedOut,
            _ownerName,
            (int)Timeout.TotalSeconds,
            InvocationLifetimeEnvironmentVariables.TimeoutSeconds));
        _ownerAbort.TrySetResult();
        return false;
    }

    private async Task<bool> WaitForOwnerExitAsync(Task<int> ownerRun)
    {
        if (await WhenAnyWithTimeout(ownerRun) != ownerRun)
        {
            _reportError(string.Format(
                CliCommandStrings.InvocationLifetimeCleanupTimedOut,
                _ownerName,
                (int)Timeout.TotalSeconds,
                InvocationLifetimeEnvironmentVariables.TimeoutSeconds));
            _ownerAbort.TrySetResult();
        }

        try
        {
            await ownerRun;
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogTrace($"The invocation lifetime owner '{_ownerName}' failed after its tests completed: {ex}");
            _reportError(string.Format(CliCommandStrings.InvocationLifetimeOwnerExitedBeforeCleanup, _ownerName));
            return false;
        }
    }

    private async Task<Task> WhenAnyWithTimeout(params Task[] tasks)
    {
        if (Timeout == System.Threading.Timeout.InfiniteTimeSpan)
        {
            return await Task.WhenAny(tasks);
        }

        using var timeoutCancellation = new CancellationTokenSource();
        Task delay = Task.Delay(Timeout, _timeProvider, timeoutCancellation.Token);
        Task completed = await Task.WhenAny([.. tasks, delay]);
        timeoutCancellation.Cancel();
        return completed;
    }

    private void OnReadinessTimeout()
    {
        lock (_lock)
        {
            _readinessTimer?.Dispose();
            _readinessTimer = null;
            if (_state == State.Pending)
            {
                FallBack("the test host did not request coordination in time");
                _reportWarning(string.Format(
                    CliCommandStrings.InvocationLifetimeOwnerHandshakeTimedOut,
                    _ownerName,
                    (int)Timeout.TotalSeconds,
                    InvocationLifetimeEnvironmentVariables.TimeoutSeconds));
            }
            else if (_state == State.Accepted)
            {
                Fail(string.Format(
                    CliCommandStrings.InvocationLifetimeSetupTimedOut,
                    _ownerName,
                    (int)Timeout.TotalSeconds,
                    InvocationLifetimeEnvironmentVariables.TimeoutSeconds), abortOwner: true);
            }
        }
    }

    private void FallBack(string reason)
    {
        Logger.LogTrace($"Invocation lifetime coordination with '{_ownerName}' is not used: {reason}.");
        StopReadinessTimer();
        _state = State.FallenBack;
        _siblingLaunch.TrySetResult(InvocationLifetimeSiblingLaunch.Independent);
    }

    private void FailUnexpectedMessage(string messageName)
    {
        if (_state is State.Pending or State.FallenBack or State.Failed)
        {
            // Not coordinated by this SDK, or already failed; nothing to protect.
            Logger.LogTrace($"Ignoring '{messageName}' from '{_ownerName}' in state '{_state}'.");
            return;
        }

        Fail(string.Format(CliCommandStrings.InvocationLifetimeUnexpectedMessage, _ownerName, messageName), abortOwner: true);
    }

    private void Fail(string message, bool abortOwner, bool abortSiblings = true)
    {
        if (_state == State.Failed)
        {
            return;
        }

        StopReadinessTimer();
        _state = State.Failed;
        _reportError(message);
        _siblingLaunch.TrySetResult(InvocationLifetimeSiblingLaunch.Skip);
        if (abortSiblings)
        {
            _siblingAbort.TrySetResult();
        }

        if (abortOwner)
        {
            _ownerAbort.TrySetResult();
        }
    }

    private void StopReadinessTimer()
    {
        _readinessTimer?.Dispose();
        _readinessTimer = null;
    }

    private static string AppendDetails(string message, string? details)
        => string.IsNullOrWhiteSpace(details) ? message : $"{message}{Environment.NewLine}{details}";

    internal static bool TryParseHandlers(string? json, [NotNullWhen(true)] out IReadOnlyList<string>? handlers)
    {
        handlers = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            List<string> uids = [];
            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.String || element.GetString() is not { } uid || string.IsNullOrWhiteSpace(uid))
                {
                    return false;
                }

                uids.Add(uid);
            }

            handlers = InvocationLifetimeOwnerSelector.NormalizeHandlers(uids);
            return handlers is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static string SerializeHandlers(IReadOnlyList<string> handlers)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (string handler in handlers)
            {
                writer.WriteStringValue(handler);
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public void Dispose()
    {
        lock (_lock)
        {
            StopReadinessTimer();
        }

        _ownerRelease.TrySetResult();
    }
}
