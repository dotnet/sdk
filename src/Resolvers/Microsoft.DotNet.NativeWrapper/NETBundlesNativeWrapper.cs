// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.NativeWrapper
{
    public class NETBundlesNativeWrapper : INETBundleProvider
    {
        public NetEnvironmentInfo GetDotnetEnvironmentInfo(string dotnetExeDirectory)
        {
            NetEnvironmentInfo info = new();
            IntPtr reserved = IntPtr.Zero;
            IntPtr resultContext = IntPtr.Zero;

            int errorCode = Interop.hostfxr_get_dotnet_environment_info(dotnetExeDirectory, default, info.Initialize, default);

            return info;
        }
    }
}
