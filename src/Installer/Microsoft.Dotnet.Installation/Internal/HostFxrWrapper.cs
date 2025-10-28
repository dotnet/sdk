// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.Dotnet.Installation.Internal
{
    internal class HostFxrWrapper
    {
        public static NetEnvironmentInfo getInfo(string installRoot)
        {
            var bundleProvider = new NETBundlesNativeWrapper();
            return bundleProvider.GetDotnetEnvironmentInfo(installRoot); // Could we use get_available_sdks instead to improve perf?
        }

        public static IEnumerable<DotnetInstall> getInstalls(string installRoot)
        {
            var environmentInfo = getInfo(installRoot);
            var installs = new List<DotnetInstall>();
            foreach (var sdk in environmentInfo.SdkInfo.ToList())
            {
                installs.Add(new DotnetInstall(
                    new DotnetInstallRoot(installRoot, InstallerUtilities.GetDefaultInstallArchitecture()),
                    sdk.Version,
                    InstallComponent.SDK));
            }

            foreach (var runtime in environmentInfo.RuntimeInfo.ToList())
            {
                installs.Add(new DotnetInstall(
                    new DotnetInstallRoot(installRoot, InstallerUtilities.GetDefaultInstallArchitecture()),
                    runtime.Version,
                    InstallComponent.Runtime)); // TODO: Determine the correct InstallComponent based on runtime.Name like release manifest does
            }

            return installs;
        }
    }
}
