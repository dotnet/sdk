// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

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

            StatusCode errorCode = Interop.hostfxr_resolve_sdk2(dotnetExeDirectory, globalJsonStartDirectory, flags, result.Initialize);

            Debug.Assert((errorCode == 0) == (result.ResolvedSdkDirectory != null));
            return result;
        }

        public static unsafe string? GetGlobalJsonState(string globalJsonStartDirectory)
        {
            // We don't care about the actual SDK resolution, just the global.json information,
            // so just pass empty string as executable directory for resolution. This means that
            // we expect the call to fail to resolve an SDK. Set up the error writer to avoid
            // output going to stderr. We reset it after the call.
            Interop.hostfxr_error_writer_fn swallowErrors = new(message => { });
            nint errorWriter = Marshal.GetFunctionPointerForDelegate(swallowErrors);
            var previousErrorWriter = Interop.hostfxr_set_error_writer((delegate* unmanaged[Cdecl]<PlatformString, void>)errorWriter);
            try
            {
                SdkResolutionResult result = ResolveSdk(string.Empty, globalJsonStartDirectory);
                return result.GlobalJsonState;
            }
            finally
            {
                Interop.hostfxr_set_error_writer(previousErrorWriter);
                GC.KeepAlive(swallowErrors);
            }
        }

        public static string[] GetAvailableSdks(string? dotnetExeDirectory)
        {
            Interop.hostfxr_get_available_sdks(dotnetExeDirectory, out string[] result);
            return result;
        }
    }
}
