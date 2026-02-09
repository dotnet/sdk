// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;

internal class RuntimeInstallCommand(ParseResult result) : CommandBase(result)
{
    private readonly string? _componentSpec = result.GetValue(RuntimeInstallCommandParser.ComponentSpecArgument);
    private readonly string? _installPath = result.GetValue(CommonOptions.InstallPathOption);
    private readonly bool? _setDefaultInstall = result.GetValue(CommonOptions.SetDefaultInstallOption);
    private readonly string? _manifestPath = result.GetValue(CommonOptions.ManifestPathOption);
    private readonly bool _interactive = result.GetValue(CommonOptions.InteractiveOption);
    private readonly bool _noProgress = result.GetValue(CommonOptions.NoProgressOption);

    private readonly IDotnetInstallManager _dotnetInstaller = new DotnetInstallManager();
    private readonly ChannelVersionResolver _channelVersionResolver = new ChannelVersionResolver();

    /// <summary>
    /// Maps user-friendly runtime type names to InstallComponent enum values.
    /// Descriptions are obtained from InstallComponentExtensions.GetDisplayName().
    /// </summary>
    private static readonly Dictionary<string, InstallComponent> RuntimeTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["runtime"] = InstallComponent.Runtime,
        ["aspnetcore"] = InstallComponent.ASPNETCore,
        ["windowsdesktop"] = InstallComponent.WindowsDesktop,
    };

    protected override string GetCommandName() => "runtime/install";

    protected override int ExecuteCore()
    {
        // Parse the component spec to determine runtime type and version
        var (component, versionOrChannel, errorMessage) = ParseComponentSpec(_componentSpec);

        if (errorMessage != null)
        {
            Console.Error.WriteLine(errorMessage);
            return 1;
        }

        // Windows Desktop Runtime is only available on Windows
        if (component == InstallComponent.WindowsDesktop && !OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Error: Windows Desktop Runtime is only available on Windows.");
            Console.Error.WriteLine($"Valid component types for this platform are: {string.Join(", ", GetValidRuntimeTypes())}");
            return 1;
        }

        // SDK versions and feature bands (like 9.0.103, 9.0.1xx) are SDK-specific and not valid for runtimes
        if (!string.IsNullOrEmpty(versionOrChannel) && new UpdateChannel(versionOrChannel).IsSdkVersionOrFeatureBand())
        {
            Console.Error.WriteLine($"Error: '{versionOrChannel}' looks like an SDK version or feature band, which is not valid for runtime installations.");
            Console.Error.WriteLine("Use a version channel like '9.0', 'latest', 'lts', or a specific runtime version like '9.0.12'.");
            return 1;
        }

        // Use GetDisplayName() from InstallComponentExtensions for consistent descriptions
        string componentDescription = component.GetDisplayName();

        InstallWorkflow workflow = new(_dotnetInstaller, _channelVersionResolver);

        InstallWorkflow.InstallWorkflowOptions options = new(
            versionOrChannel,
            _installPath,
            _setDefaultInstall,
            _manifestPath,
            _interactive,
            _noProgress,
            component,
            componentDescription);

        InstallWorkflow.InstallWorkflowResult workflowResult = workflow.Execute(options);
        return workflowResult.ExitCode;
    }

    /// <summary>
    /// Parses the component specification.
    /// Formats:
    ///   - null or empty: defaults to latest core runtime
    ///   - "10.0.1" or "latest": core runtime with specified version/channel
    ///   - "aspnetcore@10.0.1": ASP.NET Core runtime with specified version
    ///   - "windowsdesktop@9.0": Windows Desktop runtime with specified channel
    /// </summary>
    internal static (InstallComponent Component, string? VersionOrChannel, string? ErrorMessage) ParseComponentSpec(string? spec)
    {
        // Default: install latest core runtime
        if (string.IsNullOrWhiteSpace(spec))
        {
            return (InstallComponent.Runtime, null, null);
        }

        // Check for component@version syntax
        int atIndex = spec.IndexOf('@');
        if (atIndex > 0)
        {
            string componentName = spec[..atIndex];
            string versionPart = spec[(atIndex + 1)..];

            if (string.IsNullOrWhiteSpace(versionPart))
            {
                return (default, null, $"Error: Invalid component specification '{spec}'. Version is required after '@'.");
            }

            if (!RuntimeTypeMap.TryGetValue(componentName, out var component))
            {
                return (default, null, $"Error: Unknown component type '{componentName}'. Valid types are: {string.Join(", ", GetValidRuntimeTypes())}");
            }

            return (component, versionPart, null);
        }

        // No '@' - treat as version/channel for core runtime
        return (InstallComponent.Runtime, spec, null);
    }

    /// <summary>
    /// Gets the list of valid runtime types for the current platform.
    /// </summary>
    internal static IEnumerable<string> GetValidRuntimeTypes()
    {
        foreach (var kvp in RuntimeTypeMap)
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
