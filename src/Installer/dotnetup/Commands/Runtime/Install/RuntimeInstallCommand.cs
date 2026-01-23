// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;

internal class RuntimeInstallCommand(ParseResult result) : CommandBase(result)
{
    private readonly string _runtimeType = result.GetValue(RuntimeInstallCommandParser.TypeArgument)!;
    private readonly string? _versionOrChannel = result.GetValue(RuntimeInstallCommandParser.ChannelArgument);
    private readonly string? _installPath = result.GetValue(RuntimeInstallCommandParser.InstallPathOption);
    private readonly bool? _setDefaultInstall = result.GetValue(RuntimeInstallCommandParser.SetDefaultInstallOption);
    private readonly string? _manifestPath = result.GetValue(RuntimeInstallCommandParser.ManifestPathOption);
    private readonly bool _interactive = result.GetValue(RuntimeInstallCommandParser.InteractiveOption);
    private readonly bool _noProgress = result.GetValue(RuntimeInstallCommandParser.NoProgressOption);

    private readonly IDotnetInstallManager _dotnetInstaller = new DotnetInstallManager();
    private readonly ChannelVersionResolver _channelVersionResolver = new ChannelVersionResolver();

    /// <summary>
    /// Maps user-friendly runtime type names to InstallComponent enum values.
    /// </summary>
    private static readonly Dictionary<string, (InstallComponent Component, string Description)> RuntimeTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["core"] = (InstallComponent.Runtime, ".NET Runtime"),
        ["aspnetcore"] = (InstallComponent.ASPNETCore, "ASP.NET Core Runtime"),
        ["windowsdesktop"] = (InstallComponent.WindowsDesktop, "Windows Desktop Runtime"),
    };

    public override int Execute()
    {
        if (!RuntimeTypeMap.TryGetValue(_runtimeType, out var runtimeInfo))
        {
            Console.Error.WriteLine($"Error: Unknown runtime type '{_runtimeType}'. Valid types are: {string.Join(", ", RuntimeTypeMap.Keys)}");
            return 1;
        }

        // Feature bands (like 9.0.1xx) are SDK-specific and not valid for runtimes
        if (!string.IsNullOrEmpty(_versionOrChannel) && IsFeatureBand(_versionOrChannel))
        {
            Console.Error.WriteLine($"Error: Feature bands (like '{_versionOrChannel}') are only valid for SDK installations, not runtimes.");
            Console.Error.WriteLine("Use a version channel like '9.0', 'latest', 'lts', or a specific version like '9.0.12'.");
            return 1;
        }

        InstallWorkflow workflow = new(_dotnetInstaller, _channelVersionResolver);

        InstallWorkflow.InstallWorkflowOptions options = new(
            _versionOrChannel,
            _installPath,
            _setDefaultInstall,
            _manifestPath,
            _interactive,
            _noProgress,
            runtimeInfo.Component,
            runtimeInfo.Description);

        InstallWorkflow.InstallWorkflowResult workflowResult = workflow.Execute(options);
        return workflowResult.ExitCode;
    }

    /// <summary>
    /// Checks if the channel string is a feature band pattern (e.g., "9.0.1xx", "10.0.2xx").
    /// </summary>
    private static bool IsFeatureBand(string channel)
    {
        var parts = channel.Split('.');
        return parts.Length >= 3 && parts[2].EndsWith("xx", StringComparison.OrdinalIgnoreCase);
    }
}
