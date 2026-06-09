// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Tool;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public class DotnetToolsCommandResolver : ICommandResolver
{
    private readonly string _dotnetToolPath;

    public DotnetToolsCommandResolver(string? dotnetToolPath = null)
    {
        _dotnetToolPath = dotnetToolPath ?? Path.Combine(AppContext.BaseDirectory, "DotnetTools");
    }

    public CommandSpec? Resolve(CommandResolverArguments arguments)
    {
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
