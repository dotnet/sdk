// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.NativeWrapper
{
    public class NETBundlesNativeWrapper : INETBundleProvider
    {
        // lpFileName passed to LoadLibraryEx must be a full path.
        private const int LOAD_WITH_ALTERED_SEARCH_PATH = 0x8;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr LoadLibraryExW(string lpFileName, IntPtr hFile, int dwFlags);

        public NetEnvironmentInfo GetDotnetEnvironmentInfo(string dotnetExeDirectory)
        {
            PreloadHostFxrLibrary(dotnetExeDirectory);

            var info = new NetEnvironmentInfo();
            IntPtr reserved = IntPtr.Zero;
            IntPtr resultContext = IntPtr.Zero;

            //  If directory doesn't exist, we may not be able to load hostfxr, so treat as if there are no installed frameworks/SDKs.
            if (Directory.Exists(dotnetExeDirectory))
            {
                int errorCode = Interop.hostfxr_get_dotnet_environment_info(dotnetExeDirectory, reserved, info.Initialize, resultContext);
            }

            return info;
        }

        private void PreloadHostFxrLibrary(string dotnetExeDirectory)
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
                    AppContext.SetData(Constants.RuntimeProperty.HostFxrPath, hostFxrPath);
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
