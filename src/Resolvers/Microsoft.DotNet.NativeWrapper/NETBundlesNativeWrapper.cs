// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.NativeWrapper
{
    public class NETBundlesNativeWrapper : INETBundleProvider
    {
        public NetEnvironmentInfo GetDotnetEnvironmentInfo(string dotnetExeDirectory)
        {
            var info = new NetEnvironmentInfo();
            IntPtr reserved = IntPtr.Zero;
            IntPtr resultContext = IntPtr.Zero;

            int errorCode = Interop.hostfxr_get_dotnet_environment_info(dotnetExeDirectory, reserved, info.Initialize, resultContext);

            return info;
        }

        /// <summary>
        /// Checks if frameworks can be resolved for a given runtime config path using hostfxr
        /// </summary>
        /// <param name="runtimeConfigPath">Path to the runtimeconfig.json file</param>
        /// <returns>True if frameworks can be resolved successfully</returns>
        public bool CanResolveFrameworks(string runtimeConfigPath)
        {
            if (!File.Exists(runtimeConfigPath))
            {
                return false;
            }

            try
            {
                bool resolved = false;
                IntPtr parameters = IntPtr.Zero;
                IntPtr resultContext = IntPtr.Zero;

                void Callback(IntPtr assemblyPath, IntPtr nativeSearchPath, nuint frameworkCount, IntPtr frameworks)
                {
                    // If we get here with frameworks, resolution succeeded
                    resolved = frameworkCount > 0 || assemblyPath != IntPtr.Zero;
                }

                int errorCode = Interop.hostfxr_resolve_frameworks_for_runtime_config(
                    runtimeConfigPath,
                    parameters,
                    Callback,
                    resultContext);

                // Return code 0 means success
                return errorCode == 0 && resolved;
            }
            catch
            {
                // If hostfxr call fails, return false
                return false;
            }
        }
    }
}
