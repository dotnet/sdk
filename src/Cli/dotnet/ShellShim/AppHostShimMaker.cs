// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.HostModel.AppHost;

namespace Microsoft.DotNet.Cli.ShellShim;

internal class AppHostShellShimMaker(string appHostSourceDirectory, IFilePermissionSetter filePermissionSetter = null) : IAppHostShellShimMaker
{
    private const string ApphostNameWithoutExtension = "apphost";
    private readonly string _appHostSourceDirectory = appHostSourceDirectory;
    private readonly IFilePermissionSetter _filePermissionSetter =
            filePermissionSetter
            ?? new FilePermissionSetter();
    private const ushort WindowsGUISubsystem = 0x2;

    public void CreateApphostShellShim(FilePath entryPoint, FilePath shimPath)
    {
        string appHostSourcePath;
        if (OperatingSystem.IsWindows())
        {
            appHostSourcePath = Path.Combine(_appHostSourceDirectory, ApphostNameWithoutExtension + ".exe");
        }
        else
        {
            appHostSourcePath = Path.Combine(_appHostSourceDirectory, ApphostNameWithoutExtension);
        }

        var appHostDestinationFilePath = Path.GetFullPath(shimPath.Value);
        string entryPointFullPath = Path.GetFullPath(entryPoint.Value);
        var appBinaryFilePath = Path.GetRelativePath(Path.GetDirectoryName(appHostDestinationFilePath), entryPointFullPath);

        var windowsGraphicalUserInterfaceBit = PEUtils.GetWindowsGraphicalUserInterfaceBit(entryPointFullPath);
        var windowsGraphicalUserInterface = windowsGraphicalUserInterfaceBit == WindowsGUISubsystem && OperatingSystem.IsWindows();

        HostWriter.CreateAppHost(appHostSourceFilePath: appHostSourcePath,
                                 appHostDestinationFilePath: appHostDestinationFilePath,
                                 appBinaryFilePath: appBinaryFilePath,
                                 windowsGraphicalUserInterface: windowsGraphicalUserInterface,
                                 assemblyToCopyResourcesFrom: entryPointFullPath,
                                 enableMacOSCodeSign: OperatingSystem.IsMacOS());

        _filePermissionSetter.SetUserExecutionPermission(appHostDestinationFilePath);
    }
}
