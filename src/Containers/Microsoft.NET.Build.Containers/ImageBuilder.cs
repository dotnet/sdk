// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;
using System.Text.Json.Nodes;
using System.Runtime.Intrinsics.Arm;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// The class builds new image based on the base image.
/// </summary>
internal sealed class ImageBuilder
{
    // a snapshot of the manifest that this builder is based on
    private readonly ManifestV2 _baseImageManifest;

    // the mutable internal manifest that we're building by modifying the base and applying customizations
    private readonly ManifestV2 _manifest;
    private readonly Image _baseImage;
    private readonly ILogger _logger;

    /// <summary>
    /// This is a parser for ASPNETCORE_URLS based on https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http/src/BindingAddress.cs
    /// We can cut corners a bit here because we really only care about ports, if they exist.
    /// </summary>
    internal static Regex aspnetPortRegex = new(@"(?<scheme>\w+)://(?<domain>([*+]|).+):(?<port>\d+)");

    private bool _userHasBeenExplicitlySet;

    public ImageExecution BaseImageConfig => _baseImage.Config!;

    /// <summary>
    /// MediaType of the output manifest. By default, this will be the same as the base image manifest.
    /// </summary>
    public string ManifestMediaType {
        get
        {
            return _manifest.MediaType!;
        }

        set
        {
            _manifest.MediaType = value;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageBuilder"/> class.
    /// This is used to build a new image based on the base image.
    /// It does a clone of the base manifest and base image so that the original base image is not modified.
    /// </summary>
    /// <param name="manifest">The parent manifest that this image is based on.</param>
    /// <param name="manifestMediaType">The kind of manifest the to-be-created image should create.</param>
    /// <param name="baseImage">The base image to build upon. This is cloned so as to not modify the original base image.</param>
    /// <param name="logger">The logger to use for logging.</param>
    internal ImageBuilder(ManifestV2 manifest, string manifestMediaType, Image baseImage, ILogger logger)
    {
        _baseImageManifest = manifest;
        _manifest = new ManifestV2()
        {
            SchemaVersion = manifest.SchemaVersion,
            Config = manifest.Config,
            Layers = [.. manifest.Layers],
            MediaType = manifestMediaType,
            Annotations = manifest.Annotations,
            Labels = manifest.Labels
        };
        _baseImage = baseImage with { };
        _baseImage.Config ??= new ImageExecution();
        _logger = logger;
    }

    /// <summary>
    /// Gets a value indicating whether the base image is has a Windows operating system.
    /// </summary>
    public bool IsWindows => _baseImage.IsWindows;

    // For tests
    internal Digest ManifestConfigDigest => _manifest.Config.Digest;

    /// <summary>
    /// Builds the image configuration <see cref="BuiltImage"/> ready for further processing.
    /// </summary>
    internal BuiltImage Build()
    {
        // before we build, we need to make sure that any image customizations occur
        AssignUserFromEnvironment();
        AssignPortsFromEnvironment();

        var mediaType = ManifestMediaType switch
        {
            SchemaTypes.OciManifestV1 => SchemaTypes.OciImageConfigV1,
            SchemaTypes.DockerManifestV2 => SchemaTypes.DockerContainerV1,
            _ => SchemaTypes.OciImageConfigV1 // opinion - defaulting to modern here, but really this should never happen
        };

        Descriptor manifestConfigDescriptor = Descriptor.FromContent(mediaType, DigestAlgorithm.sha256, _baseImage);
        _manifest.Config = manifestConfigDescriptor;

        return new BuiltImage()
        {
            Image = _baseImage,
            Manifest = _manifest with { }
        };
    }

    /// <summary>
    /// Adds a <see cref="Layer"/> to a base image.
    /// </summary>
    internal void AddLayer(Layer l)
    {
        _manifest.Layers.Add(l.Descriptor);
        // the rootfs diffids are the _uncompressed_ digests, so we need to use the uncompressed digest here.
        // a more 'full' treatment would be to see if the mediatype of the layer is 'uncompressed' or not, and 
        // if 'uncompressed' already just use its digest - and if not 'convert' it to an uncompressed digest
        // by uncompressing the layer and calculating the digest.
        _baseImage.RootFS.DiffIDs.Add((Digest)l.Descriptor.UncompressedDigest!);
    }

    internal (string name, string value) AddBaseImageDigestLabel()
    {
        var label = ("org.opencontainers.image.base.digest", _baseImageManifest.GetDigest().ToString());
        AddLabel(label.Item1, label.Item2);
        return label;
    }

    /// <summary>
    /// Adds a label to a base image.
    /// </summary>
    internal void AddLabel(string name, string value) =>
        (_baseImage.Config!.Labels ?? []).Add(name, value);

    /// <summary>
    /// Adds environment variables to a base image.
    /// </summary>
    internal void AddEnvironmentVariable(string envVarName, string value) =>
        (_baseImage.Config!.Env ??= []).Add(new KeyValuePair<string, string>(envVarName, value));

    /// <summary>
    /// Exposes additional port.
    /// </summary>
    internal void ExposePort(int number, PortType type) =>
        (_baseImage.Config!.ExposedPorts ??= []).Add(new Port(number, type));

    /// <summary>
    /// Sets working directory for the image.
    /// </summary>
    internal void SetWorkingDirectory(string workingDirectory) =>
        _baseImage.Config!.WorkingDir = workingDirectory;

    /// <summary>
    /// Sets the ENTRYPOINT and CMD for the image.
    /// </summary>
    internal void SetEntrypointAndCmd(string[] entrypoint, string[] cmd)
    {
        _baseImage.Config!.Entrypoint = entrypoint;
        _baseImage.Config!.Cmd = cmd;
    }

    /// <summary>
    /// Sets the USER for the image.
    /// </summary>
    internal void SetUser(string user, bool isExplicitUserInteraction = true)
    {
        // we don't let automatic/inferred user settings overwrite an explicit user request
        if (_userHasBeenExplicitlySet && !isExplicitUserInteraction)
        {
            return;
        }

        _baseImage.Config!.User = user;
        _userHasBeenExplicitlySet = isExplicitUserInteraction;
    }

    internal static (string[] entrypoint, string[] cmd) DetermineEntrypointAndCmd(
        string[] entrypoint,
        string[] entrypointArgs,
        string[] cmd,
        string[] appCommand,
        string[] appCommandArgs,
        string appCommandInstruction,
        string[]? baseImageEntrypoint,
        Action<string> logWarning,
        Action<string, string?> logError)
    {
        bool setsEntrypoint = entrypoint.Length > 0 || entrypointArgs.Length > 0;
        bool setsCmd = cmd.Length > 0;

        baseImageEntrypoint ??= Array.Empty<string>();
        // Some (Microsoft) base images set 'dotnet' as the ENTRYPOINT. We mustn't use it.
        if (baseImageEntrypoint.Length == 1 && (baseImageEntrypoint[0] == "dotnet" || baseImageEntrypoint[0] == "/usr/bin/dotnet"))
        {
            baseImageEntrypoint = Array.Empty<string>();
        }

        if (string.IsNullOrEmpty(appCommandInstruction))
        {
            if (setsEntrypoint)
            {
                // Backwards-compatibility: before 'AppCommand'/'Cmd' was added, only 'Entrypoint' was available.
                if (!setsCmd && appCommandArgs.Length == 0 && entrypoint.Length == 0)
                {
                    // Copy over the values for starting the application from AppCommand.
                    entrypoint = appCommand;
                    appCommand = Array.Empty<string>();

                    // Use EntrypointArgs as cmd.
                    cmd = entrypointArgs;
                    entrypointArgs = Array.Empty<string>();

                    if (entrypointArgs.Length > 0)
                    {
                        // Log warning: Instead of ContainerEntrypointArgs, use ContainerAppCommandArgs for arguments that must always be set, or ContainerDefaultArgs for default arguments that the user override when creating the container.
                        logWarning(nameof(Strings.EntrypointArgsSetPreferAppCommandArgs));
                    }

                    appCommandInstruction = KnownAppCommandInstructions.None;
                }
                else
                {
                    // There's an Entrypoint. Use DefaultArgs for the AppCommand.
                    appCommandInstruction = KnownAppCommandInstructions.DefaultArgs;
                }
            }
            else
            {
                // Default to use an Entrypoint.
                // If the base image defines an ENTRYPOINT, print a warning.
                if (baseImageEntrypoint.Length > 0)
                {
                    logWarning(nameof(Strings.BaseEntrypointOverwritten));
                }
                appCommandInstruction = KnownAppCommandInstructions.Entrypoint;
            }
        }

        if (entrypointArgs.Length > 0 && entrypoint.Length == 0)
        {
            logError(nameof(Strings.EntrypointArgsSetNoEntrypoint), null);
            return (Array.Empty<string>(), Array.Empty<string>());
        }

        if (appCommandArgs.Length > 0 && appCommand.Length == 0)
        {
            logError(nameof(Strings.AppCommandArgsSetNoAppCommand), null);
            return (Array.Empty<string>(), Array.Empty<string>());
        }

        switch (appCommandInstruction)
        {
            case KnownAppCommandInstructions.None:
                if (appCommand.Length > 0 || appCommandArgs.Length > 0)
                {
                    logError(nameof(Strings.AppCommandSetNotUsed), appCommandInstruction);
                    return (Array.Empty<string>(), Array.Empty<string>());
                }
                break;
            case KnownAppCommandInstructions.DefaultArgs:
                cmd = appCommand.Concat(appCommandArgs).Concat(cmd).ToArray();
                break;
            case KnownAppCommandInstructions.Entrypoint:
                if (setsEntrypoint)
                {
                    logError(nameof(Strings.EntrypointConflictAppCommand), appCommandInstruction);
                    return (Array.Empty<string>(), Array.Empty<string>());
                }
                entrypoint = appCommand;
                entrypointArgs = appCommandArgs;
                break;
            default:
                throw new NotSupportedException(
                    Resource.FormatString(
                        nameof(Strings.UnknownAppCommandInstruction),
                        appCommandInstruction,
                        string.Join(",", KnownAppCommandInstructions.SupportedAppCommandInstructions)));
        }

        return (entrypoint.Length > 0 ? entrypoint.Concat(entrypointArgs).ToArray() : baseImageEntrypoint, cmd);
    }

    /// <summary>
    /// The APP_UID environment variable is a convention used to set the user in a data-driven manner. we should respect it if it's present.
    /// </summary>
    internal void AssignUserFromEnvironment()
    {
        // it's a common convention to apply custom users with the APP_UID convention - we check and apply that here
        if (_baseImage.Config?.Env?.FirstOrDefault(k => k.Key == EnvironmentVariables.APP_UID) is { } appUid)
        {
            _logger.LogTrace("Setting user from APP_UID environment variable");
            SetUser(appUid.Value, isExplicitUserInteraction: false);
        }
    }

    /// <summary>
    /// ASP.NET can have urls/ports set via three environment variables - if we see any of them we should create ExposedPorts for them
    /// to ensure tooling can automatically create port mappings.
    /// </summary>
    internal void AssignPortsFromEnvironment()
    {
        // asp.net images control port bindings via three environment variables. we should check for those variables and ensure that ports are created for them.
        // precendence is captured at https://github.com/dotnet/aspnetcore/blob/f49c1c7f7467c184ffb630086afac447772096c6/src/Hosting/Hosting/src/GenericHost/GenericWebHostService.cs#L68-L119
        // ASPNETCORE_URLS is the most specific and is the only one used if present, followed by ASPNETCORE_HTTPS_PORT and ASPNETCORE_HTTP_PORT together

        // https://learn.microsoft.com//aspnet/core/fundamentals/host/web-host?view=aspnetcore-8.0#server-urls - the format of ASPNETCORE_URLS has been stable for many years now
        if (_baseImage.Config?.Env?.FirstOrDefault(k => k.Key == EnvironmentVariables.ASPNETCORE_URLS) is { } urls)
        {
            foreach (var url in Split(urls.Value))
            {
                _logger.LogTrace("Setting ports from ASPNETCORE_URLS environment variable");
                var match = aspnetPortRegex.Match(url);
                if (match.Success && int.TryParse(match.Groups["port"].Value, out int port))
                {
                    _logger.LogTrace("Added port {port}", port);
                    ExposePort(port, PortType.tcp);
                }
            }
            return; // we're done here - ASPNETCORE_URLS is the most specific and overrides the other two
        }

        // port-specific
        // https://learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-8.0#specify-ports-only - new for .NET 8 - allows just changing port(s) easily
        if (_baseImage.Config?.Env?.FirstOrDefault(k => k.Key == EnvironmentVariables.ASPNETCORE_HTTP_PORTS) is { } httpPorts)
        {
            _logger.LogTrace("Setting ports from ASPNETCORE_HTTP_PORTS environment variable");
            foreach (var port in Split(httpPorts.Value))
            {
                if (int.TryParse(port, out int parsedPort))
                {
                    _logger.LogTrace("Added port {port}", parsedPort);
                    ExposePort(parsedPort, PortType.tcp);
                }
                else
                {
                    _logger.LogTrace("Skipped port {port} because it could not be parsed as an integer", port);
                }
            }
        }

        if (_baseImage.Config?.Env?.FirstOrDefault(k => k.Key == EnvironmentVariables.ASPNETCORE_HTTPS_PORTS) is { } httpsPorts)
        {
            _logger.LogTrace("Setting ports from ASPNETCORE_HTTPS_PORTS environment variable");
            foreach (var port in Split(httpsPorts.Value))
            {
                if (int.TryParse(port, out int parsedPort))
                {
                    _logger.LogTrace("Added port {port}", parsedPort);
                    ExposePort(parsedPort, PortType.tcp);
                }
                else
                {
                    _logger.LogTrace("Skipped port {port} because it could not be parsed as an integer", port);
                }
            }
        }

        static string[] Split(string input)
        {
            return input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    internal static class EnvironmentVariables
    {
        public static readonly string APP_UID = nameof(APP_UID);
        public static readonly string ASPNETCORE_URLS = nameof(ASPNETCORE_URLS);
        public static readonly string ASPNETCORE_HTTP_PORTS = nameof(ASPNETCORE_HTTP_PORTS);
        public static readonly string ASPNETCORE_HTTPS_PORTS = nameof(ASPNETCORE_HTTPS_PORTS);
    }

}
