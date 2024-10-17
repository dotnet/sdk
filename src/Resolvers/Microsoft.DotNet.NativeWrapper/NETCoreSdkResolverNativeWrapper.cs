// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.Contracts;

#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable enable
#pragma warning restore IDE0240 // Remove redundant nullable directive

namespace Microsoft.DotNet.NativeWrapper
{
    public static class NETCoreSdkResolverNativeWrapper
    {
        public static SdkResolutionResult ResolveSdk(
            string? dotnetExeDirectory,
            string? globalJsonStartDirectory,
            bool disallowPrerelease = false)
        {
            var result = new SdkResolutionResult();
            var flags = disallowPrerelease ? Interop.hostfxr_resolve_sdk2_flags_t.disallow_prerelease : 0;

            int errorCode = Interop.RunningOnWindows
                ? Interop.Windows.hostfxr_resolve_sdk2(dotnetExeDirectory, globalJsonStartDirectory, flags, result.Initialize)
                : Interop.Unix.hostfxr_resolve_sdk2(dotnetExeDirectory, globalJsonStartDirectory, flags, result.Initialize);

            Debug.Assert((errorCode == 0) == (result.ResolvedSdkDirectory != null));
            return result;
        }

        private sealed class SdkList
        {
            public string[]? Entries;

            public void Initialize(int count, string[] entries)
            {
                entries = entries ?? Array.Empty<string>();
                Debug.Assert(count == entries.Length);
                Entries = entries;
            }
        }

        public static string[]? GetAvailableSdks(string? dotnetExeDirectory)
        {
            var list = new SdkList();

            int errorCode = Interop.RunningOnWindows
                ? Interop.Windows.hostfxr_get_available_sdks(dotnetExeDirectory, list.Initialize)
                : Interop.Unix.hostfxr_get_available_sdks(dotnetExeDirectory, list.Initialize);

            return list.Entries;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct hostfxr_initialize_parameters
        {

        }

        public enum InitializationRuntimeConfigResult
        {
            Success,
            RuntimeConfigNotFound,
        }

        public static InitializationRuntimeConfigResult InitializeForRuntimeConfig(string runtimeConfigPath)
        {
            var result = -1;
            IntPtr hostContextHandle = default;

            hostfxr_initialize_parameters parameters = new hostfxr_initialize_parameters();

            IntPtr parametersPtr = Marshal.AllocHGlobal(Marshal.SizeOf(parameters));
            Marshal.StructureToPtr(parameters, parametersPtr, false);

            if (File.Exists(runtimeConfigPath))
            {
                result = Interop.RunningOnWindows
                    ? Interop.Windows.hostfxr_initialize_for_runtime_config(runtimeConfigPath, default, out hostContextHandle)
                    : Interop.Unix.hostfxr_initialize_for_runtime_config(runtimeConfigPath, default, out hostContextHandle);
            }

            Marshal.FreeHGlobal(parametersPtr);
            switch (result)
            {
                case 0:
                case 1:
                case 2:
                    return InitializationRuntimeConfigResult.Success;
                default:
                    return InitializationRuntimeConfigResult.RuntimeConfigNotFound;
            }
        }
    }
}
