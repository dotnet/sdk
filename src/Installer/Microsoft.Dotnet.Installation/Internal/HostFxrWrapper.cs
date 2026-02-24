// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DotNet.NativeWrapper;
using Microsoft.VisualBasic;

namespace Microsoft.Dotnet.Installation.Internal
{
    internal class HostFxrWrapper
    {
        public static NetEnvironmentInfo getInfo(string installRoot)
        {
            if (!Directory.Exists(installRoot))
            {
                return new NetEnvironmentInfo();
            }
            PreloadHostFxrLibrary(installRoot);

            var bundleProvider = new NETBundlesNativeWrapper();
            return bundleProvider.GetDotnetEnvironmentInfo(installRoot); // Could we use get_available_sdks instead to improve perf?
        }

        public static IEnumerable<DotnetInstall> getInstalls(string installRoot)
        {
            if (!Directory.Exists(installRoot))
            {
                return Enumerable.Empty<DotnetInstall>();
            }
            PreloadHostFxrLibrary(installRoot);

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

        // lpFileName passed to LoadLibraryEx must be a full path.
        private const int LOAD_WITH_ALTERED_SEARCH_PATH = 0x8;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr LoadLibraryExW(string lpFileName, IntPtr hFile, int dwFlags);

        private static void PreloadHostFxrLibrary(string dotnetExeDirectory)
        {
            string? hostFxrPath = FindHostFxrLibrary(dotnetExeDirectory);
            if (hostFxrPath != null)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    LoadLibraryExW(hostFxrPath, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
                }
#if NETCOREAPP
                else
                {
                    AppContext.SetData("HOSTFXR_PATH", hostFxrPath);
                }
#endif
            }
        }

        private static string? FindHostFxrLibrary(string installRoot)
        {
            string hostFxrDirectory = Path.Combine(installRoot, "host", "fxr");
            if (!Directory.Exists(hostFxrDirectory))
            {
                return null;
            }

            string libraryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "hostfxr.dll"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? "libhostfxr.dylib"
                    : "libhostfxr.so";

            return Directory.EnumerateFiles(hostFxrDirectory, libraryName, SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
    }
}
