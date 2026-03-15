// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;

internal class RuntimeInstallCommand(ParseResult result) : CommandBase(result)
{
    private readonly string[] _componentSpecs = result.GetValue(RuntimeInstallCommandParser.ComponentSpecArgument) ?? [];
    private readonly string? _installPath = result.GetValue(CommonOptions.InstallPathOption);
    private readonly bool? _setDefaultInstall = result.GetValue(CommonOptions.SetDefaultInstallOption);
    private readonly string? _manifestPath = result.GetValue(CommonOptions.ManifestPathOption);
    private readonly bool _interactive = result.GetValue(CommonOptions.InteractiveOption);
    private readonly bool _noProgress = result.GetValue(CommonOptions.NoProgressOption);
    private readonly bool _requireMuxerUpdate = result.GetValue(CommonOptions.RequireMuxerUpdateOption);
    private readonly bool _untracked = result.GetValue(CommonOptions.UntrackedOption);

    private readonly IDotnetInstallManager _dotnetInstaller = new DotnetInstallManager();
    private readonly ChannelVersionResolver _channelVersionResolver = new();

    /// <summary>
    /// Maps user-friendly runtime type names to InstallComponent enum values.
    /// Descriptions are obtained from InstallComponentExtensions.GetDisplayName().
    /// </summary>
    private static readonly Dictionary<string, InstallComponent> s_runtimeTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["runtime"] = InstallComponent.Runtime,
        ["aspnetcore"] = InstallComponent.ASPNETCore,
        ["windowsdesktop"] = InstallComponent.WindowsDesktop,
    };

    protected override string GetCommandName() => "runtime/install";

    protected override int ExecuteCore()
    {
        // If no specs provided, default to installing latest core runtime
        var specs = _componentSpecs.Length > 0 ? (string?[])_componentSpecs : [null];

        // Parse and validate all specs up front before installing any
        var parsed = new List<(InstallComponent Component, string? VersionOrChannel, string Description)>();
        foreach (var spec in specs)
        {
            var (component, versionOrChannel) = ParseComponentSpec(spec);

            if (component == InstallComponent.WindowsDesktop && !OperatingSystem.IsWindows())
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.PlatformNotSupported,
                    $"Windows Desktop Runtime is only available on Windows. Valid component types for this platform are: {string.Join(", ", GetValidRuntimeTypes())}");
            }

            if (!string.IsNullOrEmpty(versionOrChannel) && new UpdateChannel(versionOrChannel).IsSdkVersionOrFeatureBand())
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.InvalidChannel,
                    $"'{versionOrChannel}' looks like an SDK version or feature band, which is not valid for runtime installations. "
                    + "Use a version channel like '9.0', 'latest', 'lts', or a specific runtime version like '9.0.12'.");
            }

            parsed.Add((component, versionOrChannel, component.GetDisplayName()));
        }

        InstallWorkflow workflow = new(_dotnetInstaller, _channelVersionResolver);

        foreach (var (component, versionOrChannel, componentDescription) in parsed)
        {
            InstallWorkflow.InstallWorkflowOptions options = new(
                versionOrChannel,
                _installPath,
                _setDefaultInstall,
                _manifestPath,
                _interactive,
                _noProgress,
                component,
                componentDescription,
                RequireMuxerUpdate: _requireMuxerUpdate,
                Untracked: _untracked);

            workflow.Execute(options);
        }

        return 0;
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
        foreach (var kvp in s_runtimeTypeMap)
        {
            // Windows Desktop is only valid on Windows
            if (kvp.Value == InstallComponent.WindowsDesktop && !OperatingSystem.IsWindows())
            {
                continue;
            }
            yield return kvp.Key;
        }
    }
}
