// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.HostModel.AppHost;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.ShellShim;

internal class AppHostShellShimMaker(string appHostSourceDirectory) : IAppHostShellShimMaker
{
    private const string ApphostNameWithoutExtension = "apphost";
    private readonly string _appHostSourceDirectory = appHostSourceDirectory;
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

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                UnixFileMode existingMode = File.GetUnixFileMode(appHostDestinationFilePath);
                File.SetUnixFileMode(appHostDestinationFilePath, existingMode | UnixFileMode.UserExecute);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or PlatformNotSupportedException)
            {
                throw new FilePermissionSettingException(ex.Message, ex);
            }
        }
    }
}
