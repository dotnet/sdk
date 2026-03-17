// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;

internal class RuntimeInstallCommand(ParseResult result) : CommandBase(result)
{
    private readonly string[] _componentSpecs = result.GetValue(RuntimeInstallCommandParser.ComponentSpecsArgument) ?? [];
    private readonly string? _installPath = result.GetValue(CommonOptions.InstallPathOption);
    private readonly bool? _setDefaultInstall = result.GetValue(CommonOptions.SetDefaultInstallOption);
    private readonly string? _manifestPath = result.GetValue(CommonOptions.ManifestPathOption);
    private readonly bool _interactive = result.GetValue(CommonOptions.InteractiveOption);
    private readonly bool _noProgress = result.GetValue(CommonOptions.NoProgressOption);
    private readonly Verbosity _verbosity = result.GetValue(CommonOptions.VerbosityOption);
    private readonly bool _requireMuxerUpdate = result.GetValue(CommonOptions.RequireMuxerUpdateOption);
    private readonly bool _untracked = result.GetValue(CommonOptions.UntrackedOption);

    private readonly DotnetInstallManager _dotnetInstaller = new();
    private readonly ChannelVersionResolver _channelVersionResolver = new();

    /// <summary>
    /// Maps user-friendly runtime type names to InstallComponent enum values.
    /// Descriptions are obtained from InstallComponentExtensions.GetDisplayName().
    /// </summary>
    private static readonly Dictionary<string, InstallComponent> s_runtimeTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["runtime"] = InstallComponent.Runtime,
        ["aspnetcore"] = InstallComponent.ASPNETCore,
        ["aspnet"] = InstallComponent.ASPNETCore,
        ["core"] = InstallComponent.ASPNETCore,
        ["windowsdesktop"] = InstallComponent.WindowsDesktop,
        ["desktop"] = InstallComponent.WindowsDesktop,
    };

    /// <summary>Primary (non-alias) names shown in help text and error messages.</summary>
    private static readonly string[] s_primaryRuntimeTypes = ["runtime", "aspnetcore", "windowsdesktop"];

    protected override string GetCommandName() => "runtime/install";

    protected override int ExecuteCore()
    {
        // Single spec (or none): use existing InstallWorkflow for full walkthrough support
        if (_componentSpecs.Length <= 1)
        {
            string? singleSpec = _componentSpecs.Length == 1 ? _componentSpecs[0] : null;
            return ExecuteSingleInstall(singleSpec);
        }

        // Multiple specs: validate all upfront, then download concurrently and commit sequentially
        return ExecuteMultipleInstalls(_componentSpecs);
    }

    private int ExecuteSingleInstall(string? spec)
    {
        var (component, versionOrChannel) = ParseAndValidateComponentSpec(spec);

        string componentDescription = component.GetDisplayName();
        var (pathPreference, setDefault) = InstallExecutor.ResolveInstallDefaults(_interactive, _setDefaultInstall);

        InstallWorkflow workflow = new(_dotnetInstaller, _channelVersionResolver);

        InstallWorkflow.InstallWorkflowOptions options = new(
            versionOrChannel,
            _installPath,
            setDefault,
            _manifestPath,
            _interactive,
            _noProgress,
            component,
            componentDescription,
            RequireMuxerUpdate: _requireMuxerUpdate,
            Untracked: _untracked,
            PathPreference: pathPreference,
            Verbosity: _verbosity);

        workflow.Execute(options);
        return 0;
    }

    private int ExecuteMultipleInstalls(string[] specs)
    {
        // Parse and validate all specs upfront before any downloads begin
        var parsed = new List<(InstallComponent Component, string? VersionOrChannel)>();
        foreach (var spec in specs)
        {
            parsed.Add(ParseAndValidateComponentSpec(spec));
        }

        var (_, setDefault) = InstallExecutor.ResolveInstallDefaults(_interactive, _setDefaultInstall);
        string installPath = _installPath ?? _dotnetInstaller.GetDefaultDotnetInstallPath();
        var installRoot = new DotnetInstallRoot(installPath, InstallerUtilities.GetDefaultInstallArchitecture());

        // Deduplicate parsed specs so the same component+channel is not downloaded twice.
        var seen = new HashSet<(InstallComponent, string)>();
        var requests = new List<DotnetInstallRequest>();
        foreach (var p in parsed)
        {
            string channel = p.VersionOrChannel ?? ChannelVersionResolver.LatestChannel;
            if (!seen.Add((p.Component, channel)))
            {
                continue;
            }

            requests.Add(new DotnetInstallRequest(
                installRoot,
                new UpdateChannel(channel),
                p.Component,
                new InstallRequestOptions
                {
                    ManifestPath = _manifestPath,
                    RequireMuxerUpdate = _requireMuxerUpdate,
                    Untracked = _untracked,
                    Verbosity = _verbosity
                }));
        }

        InstallExecutor.RunMultiInstall(requests, installPath, _noProgress, setDefault, _dotnetInstaller);

        return 0;
    }

    private static void ValidateComponentForPlatform(InstallComponent component)
    {
        if (component == InstallComponent.WindowsDesktop && !OperatingSystem.IsWindows())
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.PlatformNotSupported,
                $"Windows Desktop Runtime is only available on Windows. Valid component types for this platform are: {string.Join(", ", GetValidRuntimeTypes())}");
        }
    }

    private static void ValidateNotSdkVersion(string? versionOrChannel)
    {
        if (!string.IsNullOrEmpty(versionOrChannel) && new UpdateChannel(versionOrChannel).IsSdkVersionOrFeatureBand())
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.InvalidChannel,
                $"'{versionOrChannel}' looks like an SDK version or feature band, which is not valid for runtime installations. "
                + "Use a version channel like '9.0', 'latest', 'lts', or a specific runtime version like '9.0.12'.");
        }
    }

    /// <summary>
    /// Parses and validates a component specification, checking platform support and SDK version conflicts.
    /// </summary>
    private static (InstallComponent Component, string? VersionOrChannel) ParseAndValidateComponentSpec(string? spec)
    {
        var (component, versionOrChannel) = ParseComponentSpec(spec);
        ValidateComponentForPlatform(component);
        ValidateNotSdkVersion(versionOrChannel);
        return (component, versionOrChannel);
    }

    /// <summary>
    /// Parses the component specification.
    /// Formats:
    ///   - null or empty: defaults to latest core runtime
    ///   - "10.0.1" or "latest": core runtime with specified version/channel
    ///   - "aspnetcore@10.0.1": ASP.NET Core runtime with specified version
    ///   - "windowsdesktop@9.0": Windows Desktop runtime with specified channel
    /// </summary>
    internal static (InstallComponent Component, string? VersionOrChannel) ParseComponentSpec(string? spec)
    {
        // Default: install latest core runtime
        if (string.IsNullOrWhiteSpace(spec))
        {
            return (InstallComponent.Runtime, null);
        }

        // Check for component@version syntax
        int atIndex = spec.IndexOf('@');
        if (atIndex == 0)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.InvalidChannel,
                $"Error: Invalid component specification '{spec}'. Component name is required before '@'.");
        }
        if (atIndex > 0)
        {
            string componentName = spec[..atIndex];
            string versionPart = spec[(atIndex + 1)..];

            if (string.IsNullOrWhiteSpace(versionPart))
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.InvalidChannel,
                    $"Error: Invalid component specification '{spec}'. Version is required after '@'.");
            }

            if (!s_runtimeTypeMap.TryGetValue(componentName, out var component))
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.InvalidChannel,
                    $"Error: Unknown component type '{componentName}'. Valid types are: {string.Join(", ", GetValidRuntimeTypes())}");
            }

            return (component, versionPart);
        }

        // No '@' - treat as version/channel for core runtime
        return (InstallComponent.Runtime, spec);
    }

    /// <summary>
    /// Gets the list of valid runtime types for the current platform.
    /// </summary>
    internal static IEnumerable<string> GetValidRuntimeTypes()
    {
        foreach (var name in s_primaryRuntimeTypes)
        {
            if (s_runtimeTypeMap.TryGetValue(name, out var component) &&
                component == InstallComponent.WindowsDesktop && !OperatingSystem.IsWindows())
            {
                continue;
            }

            yield return name;
        }
    }
}
