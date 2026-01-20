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

            int errorCode = Interop.RunningOnWindows
                ? Interop.Windows.hostfxr_resolve_sdk2(dotnetExeDirectory, globalJsonStartDirectory, flags, result.Initialize)
                : Interop.Unix.hostfxr_resolve_sdk2(dotnetExeDirectory, globalJsonStartDirectory, flags, result.Initialize);

            Debug.Assert((errorCode == 0) == (result.ResolvedSdkDirectory != null));
            return result;
        }

        public static string? GetGlobalJsonState(string globalJsonStartDirectory)
        {
            // We don't care about the actual SDK resolution, just the global.json information,
            // so just pass empty string as executable directory for resolution. This means that
            // we expect the call to fail to resolve an SDK. Set up the error writer to avoid
            // output going to stderr. We reset it after the call.
            var swallowErrors = new Interop.hostfxr_error_writer_fn(message => { });
            IntPtr errorWriter = Marshal.GetFunctionPointerForDelegate(swallowErrors);
            IntPtr previousErrorWriter = Interop.hostfxr_set_error_writer(errorWriter);
            try
            {
                SdkResolutionResult result = ResolveSdk(string.Empty, globalJsonStartDirectory);
                return result.GlobalJsonState;
            }
            finally
            {
                Interop.hostfxr_set_error_writer(previousErrorWriter);
            }
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
    }
}
