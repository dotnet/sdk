﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.HostModel;
using Microsoft.NET.HostModel.AppHost;

namespace Microsoft.DotNet.ShellShim
{
    internal class AppHostShellShimMaker : IAppHostShellShimMaker
    {
        private const string ApphostNameWithoutExtension = "apphost";
        private readonly string _appHostSourceDirectory;
        private readonly IFilePermissionSetter _filePermissionSetter;
        private const ushort WindowsGUISubsystem = 0x2;

        public AppHostShellShimMaker(string appHostSourceDirectory, IFilePermissionSetter filePermissionSetter = null)
        {
            _appHostSourceDirectory = appHostSourceDirectory;

            _filePermissionSetter =
                filePermissionSetter
                ?? new FilePermissionSetter();
        }

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


            if (ResourceUpdater.IsSupportedOS())
            {
                var windowsGraphicalUserInterfaceBit = PEUtils.GetWindowsGraphicalUserInterfaceBit(entryPointFullPath);
                HostWriter.CreateAppHost(appHostSourceFilePath: appHostSourcePath,
                                         appHostDestinationFilePath: appHostDestinationFilePath,
                                         appBinaryFilePath: appBinaryFilePath,
                                         windowsGraphicalUserInterface: (windowsGraphicalUserInterfaceBit == WindowsGUISubsystem),
                                         assemblyToCopyResourcesFrom: entryPointFullPath);
            }
            else
            {
                // by passing null to assemblyToCopyResourcesFrom, it will skip copying resources,
                // which is only supported on Windows
                HostWriter.CreateAppHost(appHostSourceFilePath: appHostSourcePath,
                                         appHostDestinationFilePath: appHostDestinationFilePath,
                                         appBinaryFilePath: appBinaryFilePath,
                                         windowsGraphicalUserInterface: false,
                                         assemblyToCopyResourcesFrom: null,
                                         enableMacOSCodeSign: OperatingSystem.IsMacOS());
            }

            _filePermissionSetter.SetUserExecutionPermission(appHostDestinationFilePath);
        }
    }
}
