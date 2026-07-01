// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Commands.Tool;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public class DotnetToolsCommandResolver : ICommandResolver
{
    public static DotnetToolsCommandResolver ForSdkRoot(string sdkRoot)
    {
        return new DotnetToolsCommandResolver(Path.Combine(sdkRoot, "DotnetTools"));
    }

    private readonly string _dotnetToolPath;

    public DotnetToolsCommandResolver(string? dotnetToolPath = null)
    {
        // AppContext.BaseDirectory for the NAOT app is the install root, not the per-SDK version path,
        // so fall back to SdkPaths.SdkDirectory (DOTNET_SDK_ROOT when set) instead. See
        // src/Cli/dotnet-aot/SdkRootResolution.md.
        _dotnetToolPath = dotnetToolPath ?? Path.Combine(SdkPaths.SdkDirectory, "DotnetTools");
    }

    public CommandSpec? Resolve(CommandResolverArguments arguments)
    {
        Activity.Current?.AddTag("lookup.root_path", _dotnetToolPath);

        if (string.IsNullOrEmpty(arguments.CommandName))
        {
            return null;
        }

        var packageId = new DirectoryInfo(Path.Combine(_dotnetToolPath, arguments.CommandName));
        if (!packageId.Exists)
        {
            return null;
        }

        var version = packageId.GetDirectories()[0];
        var toolDirectory = version.GetDirectories("tools")[0]
            .GetDirectories()[0] // TFM
            .GetDirectories()[0]; // RID

        var executableName = OperatingSystem.IsWindows() ? $"{arguments.CommandName}.exe" : arguments.CommandName;
        var executable = toolDirectory.GetFiles(executableName).FirstOrDefault();
        if (executable is not null)
        {
            return ToolCommandSpecCreator.CreateToolCommandSpec(
                arguments.CommandName,
                executable.FullName,
                "executable",
                allowRollForward: false,
                arguments.CommandArguments ?? []);
        }

        var dll = toolDirectory.GetFiles($"{arguments.CommandName}.dll")[0];

        return MuxerCommandSpecMaker.CreatePackageCommandSpecUsingMuxer(
                dll.FullName,
                arguments.CommandArguments ?? []);
    }
}
