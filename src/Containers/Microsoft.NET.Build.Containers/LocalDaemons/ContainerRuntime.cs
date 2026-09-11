// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Selects a local container runtime and delegates local image-store operations to it.
/// </summary>
internal sealed class ContainerRuntime : ILocalRegistry
{
    public const string DockerCommand = "docker";

    public const string PodmanCommand = "podman";

    public const string WslcCommand = "wslc";

    public const string MacOSContainerCommand = "container";

    private const string FallbackCommands = $"{DockerCommand}/{PodmanCommand}";
    private static readonly TimeSpan s_defaultProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger _logger;
    private readonly ContainerRuntimeOperations _operations;
    private readonly Func<bool> _isPodmanAlias;
    private readonly bool _isWindows;
    private readonly bool _isMacOS;
    private readonly TimeSpan _probeTimeout;
    private IContainerRuntime? _runtime;

    public ContainerRuntime(string? command, ILoggerFactory loggerFactory)
        : this(
            command,
            loggerFactory,
            ContainerRuntimeOperations.TryRunCommandAsync,
            DockerContainerRuntime.IsPodmanAlias,
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX),
            s_defaultProbeTimeout)
    {
    }

    internal ContainerRuntime(ILoggerFactory loggerFactory, bool probePlatformNativeCli)
        : this(
            command: null,
            loggerFactory,
            ContainerRuntimeOperations.TryRunCommandAsync,
            DockerContainerRuntime.IsPodmanAlias,
            probePlatformNativeCli && RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            probePlatformNativeCli && RuntimeInformation.IsOSPlatform(OSPlatform.OSX),
            s_defaultProbeTimeout)
    {
    }

    internal ContainerRuntime(
        string? command,
        ILoggerFactory loggerFactory,
        Func<string, string, CancellationToken, Task<bool>> tryRunCommand,
        Func<bool> isPodmanAlias,
        bool isWindows,
        bool isMacOS,
        TimeSpan? probeTimeout = null)
    {
        _logger = loggerFactory.CreateLogger<ContainerRuntime>();
        _operations = new ContainerRuntimeOperations(_logger, tryRunCommand);
        _isPodmanAlias = isPodmanAlias;
        _isWindows = isWindows;
        _isMacOS = isMacOS;
        _probeTimeout = probeTimeout ?? s_defaultProbeTimeout;
        _runtime = command is null ? null : CreateRuntime(command);
    }

    public ContainerRuntime(ILoggerFactory loggerFactory)
        : this(null, loggerFactory)
    {
    }

    /// <inheritdoc />
    public async Task LoadAsync(
        BuiltImage image,
        SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        CancellationToken cancellationToken)
    {
        IContainerRuntime runtime = await GetRuntimeAsync(cancellationToken).ConfigureAwait(false)
            ?? throw CreateRuntimeNotFoundException();
        await runtime.LoadAsync(image, sourceReference, destinationReference, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task LoadAsync(
        MultiArchImage multiArchImage,
        SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        CancellationToken cancellationToken)
    {
        IContainerRuntime runtime = await GetRuntimeAsync(cancellationToken).ConfigureAwait(false)
            ?? throw CreateRuntimeNotFoundException();
        await runtime.LoadAsync(multiArchImage, sourceReference, destinationReference, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        IContainerRuntime? runtime = await GetRuntimeAsync(cancellationToken);
        if (runtime is null)
        {
            _logger.LogError($"Cannot find {GetCommandsForCurrentPlatform()} executable.");
            return false;
        }

        try
        {
            return await runtime.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(Strings.LocalDocker_FailedToGetConfig, ex.Message);
            _logger.LogTrace("Full information: {0}", ex);
            return false;
        }
    }

    /// <inheritdoc />
    public bool IsAvailable()
        => IsAvailableAsync(default).GetAwaiter().GetResult();

    internal ContainerRuntimeKind GetTelemetryValue()
        => GetRuntimeAsync(default).GetAwaiter().GetResult()?.GetTelemetryValue() ?? ContainerRuntimeKind.Unknown;

    internal string GetManifestMediaType(string defaultManifestMediaType, KnownImageFormats? imageFormat)
        => GetRuntimeAsync(default).GetAwaiter().GetResult()?.GetManifestMediaType(defaultManifestMediaType, imageFormat)
            ?? ContainerRuntimeBase.GetDefaultManifestMediaType(defaultManifestMediaType, imageFormat);

    public override string ToString()
        => string.Format(Strings.DockerCli_PushInfo, _runtime?.GetTelemetryValue().ToString());

    private IContainerRuntime CreateRuntime(string command)
        => command switch
        {
            DockerCommand => new DockerContainerRuntime(_operations, _logger),
            PodmanCommand => new PodmanContainerRuntime(_operations, _logger),
            WslcCommand => new WslcContainerRuntime(_operations, _logger),
            MacOSContainerCommand => new MacOSContainerRuntime(_operations, _logger),
            _ => throw new ArgumentException($"{command} is an unknown command.")
        };

    private async ValueTask<IContainerRuntime?> GetRuntimeAsync(CancellationToken cancellationToken)
    {
        if (_runtime is not null)
        {
            return _runtime;
        }

        IContainerRuntime? platformNativeRuntime = _isWindows
            ? CreateRuntime(WslcCommand)
            : _isMacOS
                ? CreateRuntime(MacOSContainerCommand)
                : null;
        if (platformNativeRuntime is not null &&
            await ProbeAsync(platformNativeRuntime, cancellationToken).ConfigureAwait(false))
        {
            return _runtime = platformNativeRuntime;
        }

        IContainerRuntime podmanRuntime = CreateRuntime(PodmanCommand);
        IContainerRuntime dockerRuntime = CreateRuntime(DockerCommand);
        Task<bool> podmanAvailable = ProbeAsync(podmanRuntime, cancellationToken);
        Task<bool> dockerAvailable = ProbeAsync(dockerRuntime, cancellationToken);

        await Task.WhenAll(podmanAvailable, dockerAvailable).ConfigureAwait(false);

        if (dockerAvailable.Result && podmanAvailable.Result && _isPodmanAlias())
        {
            _runtime = podmanRuntime;
        }
        else if (dockerAvailable.Result)
        {
            _runtime = dockerRuntime;
        }
        else if (podmanAvailable.Result)
        {
            _runtime = podmanRuntime;
        }

        return _runtime;
    }

    private async Task<bool> ProbeAsync(IContainerRuntime runtime, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_probeTimeout);
        try
        {
            return await runtime.ProbeAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private string GetCommandsForCurrentPlatform()
        => _isWindows
            ? $"{WslcCommand}/{FallbackCommands}"
            : _isMacOS
                ? $"{MacOSContainerCommand}/{FallbackCommands}"
                : FallbackCommands;

    private NotImplementedException CreateRuntimeNotFoundException()
        => new(Resource.FormatString(Strings.ContainerRuntimeProcessCreationFailed, GetCommandsForCurrentPlatform()));
}
