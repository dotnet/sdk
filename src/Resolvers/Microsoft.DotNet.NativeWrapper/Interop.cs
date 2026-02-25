// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.NativeWrapper
{
    public static partial class Interop
    {
        public static readonly bool RunningOnWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#if NET
        private static readonly string? s_hostFxrPath;
#endif

        static Interop()
        {
#if NET
            if (!RunningOnWindows)
            {
                s_hostFxrPath = (string)AppContext.GetData(Constants.RuntimeProperty.HostFxrPath)!;
                System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly())!.ResolvingUnmanagedDll += HostFxrResolver;
            }
#else
            PreloadWindowsLibrary(Constants.HostFxr);
#endif
        }

#if NETFRAMEWORK
        // MSBuild SDK resolvers are required to be AnyCPU, but we have a native dependency and .NET Framework does not
        // have a built-in facility for dynamically loading user native dlls for the appropriate platform. We therefore
        // preload the version with the correct architecture (from a corresponding sub-folder relative to us) on static
        // construction so that subsequent P/Invokes can find it.
        //
        // An example path this is trying to resolve (when compiled into the MSBuildSdkResolver):
        //
        //  C:\Program Files\Microsoft Visual Studio\18\Preview\MSBuild\Current\Bin\SdkResolvers\Microsoft.DotNet.MSBuildSdkResolver
        //
        // `hostfxr.dll` sits in subfolders there. This isn't necessary to do when running on .NET as the host will
        // already be loaded in the process. Unfortunately there are no issues or pull requests tracking the original
        // implementation of this as it was committed directly from a dev branch into main.

        private static void PreloadWindowsLibrary(string dllFileName)
        {
            string? basePath = Path.GetDirectoryName(typeof(Interop).Assembly.Location);
            string architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
            string dllPath = Path.Combine(basePath ?? string.Empty, architecture, $"{dllFileName}.dll");

            // return value is intentionally ignored as we let the subsequent P/Invokes fail naturally.
            LoadLibraryExW(dllPath, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
        }
#endif

#if NET
        private static IntPtr HostFxrResolver(Assembly assembly, string libraryName)
        {
            if (libraryName != Constants.HostFxr)
            {
                return IntPtr.Zero;
            }

            if (string.IsNullOrEmpty(s_hostFxrPath))
            {
                throw new HostFxrRuntimePropertyNotSetException();
            }

            if (!NativeLibrary.TryLoad(s_hostFxrPath, out var handle))
            {
                throw new HostFxrNotFoundException(s_hostFxrPath);
            }

            return handle;
        }
#endif

        // lpFileName passed to LoadLibraryEx must be a full path.
        private const int LOAD_WITH_ALTERED_SEARCH_PATH = 0x8;

#if NET
        [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial nint LoadLibraryExW(string lpFileName, nint hFile, int dwFlags);
#else
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern IntPtr LoadLibraryExW(string lpFileName, nint hFile, int dwFlags);
#endif

        /// <summary>
        ///  Flags that control SDK resolution behavior in <see cref="hostfxr_resolve_sdk2"/>.
        /// </summary>
        [Flags]
        internal enum hostfxr_resolve_sdk2_flags_t : int
        {
            /// <summary>
            ///  No special flags. Default resolution behavior.
            /// </summary>
            none = 0,

            /// <summary>
            ///  Disallow resolution to return a prerelease SDK version unless a prerelease
            ///  version was explicitly specified via global.json.
            /// </summary>
            disallow_prerelease = 0x1
        }

        /// <summary>
        ///  Identifies the type of result returned through the <see cref="hostfxr_resolve_sdk2"/> callback.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   The callback may be invoked multiple times with different keys to provide
        ///   comprehensive information about the SDK resolution process.
        ///  </para>
        /// </remarks>
        internal enum hostfxr_resolve_sdk2_result_key_t : int
        {
            /// <summary>
            ///  The resolved SDK directory path. Only provided if resolution succeeds.
            /// </summary>
            resolved_sdk_dir = 0,

            /// <summary>
            ///  The path to the global.json file that influenced resolution, if any.
            ///  Not provided if no global.json was found or if it didn't affect resolution.
            /// </summary>
            global_json_path = 1,

            /// <summary>
            ///  The SDK version requested by global.json, if a specific version was specified.
            ///  Provided for both successful and failed resolutions.
            /// </summary>
            requested_version = 2,

            /// <summary>
            ///  The state of global.json processing. One of: "not_found", "valid", "invalid_json", or "invalid_data".
            /// </summary>
            global_json_state = 3
        }

        /// <summary>
        ///  Contains comprehensive information about the .NET environment, passed to the
        ///  <c>hostfxr_get_dotnet_environment_info</c> callback.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   This structure provides information about the hostfxr version, all installed SDKs,
        ///   and all installed shared frameworks.
        ///  </para>
        ///  <para>
        ///   <b>Memory ownership:</b> This structure, the arrays it references, and all strings within those
        ///   arrays are owned by the native hostfxr library. They are valid only for the duration of the
        ///   callback invocation. The caller must copy any data it needs to retain. Do not attempt to free
        ///   any pointers in this structure or the nested structures.
        ///  </para>
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct hostfxr_dotnet_environment_info
        {
            /// <summary>The size of this structure in bytes.</summary>
            public nint size;

            /// <summary>Pointer to the hostfxr version string.</summary>
            public PlatformString hostfxr_version;

            /// <summary>Pointer to the hostfxr commit hash string.</summary>
            public PlatformString hostfxr_commit_hash;

            /// <summary>The number of SDKs in the <see cref="sdks"/> array.</summary>
            public nint sdk_count;

            /// <summary>Pointer to an array of <see cref="hostfxr_dotnet_environment_sdk_info"/> structures.</summary>
            public hostfxr_dotnet_environment_sdk_info* sdks;

            /// <summary>The number of frameworks in the <see cref="frameworks"/> array.</summary>
            public nint framework_count;

            /// <summary>Pointer to an array of <see cref="hostfxr_dotnet_environment_framework_info"/> structures.</summary>
            public hostfxr_dotnet_environment_framework_info* frameworks;
        }

        /// <summary>
        ///  Contains information about an installed shared framework, returned by <see cref="hostfxr_get_dotnet_environment_info"/>.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   <b>Memory ownership:</b> This structure and all referenced strings are owned by the native hostfxr
        ///   library. They are valid only for the duration of the callback invocation. The caller must copy
        ///   any data it needs to retain. Do not attempt to free any pointers in this structure.
        ///  </para>
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        internal struct hostfxr_dotnet_environment_framework_info
        {
            /// <summary>The size of this structure in bytes.</summary>
            public nint size;

            /// <summary>Pointer to the framework name (e.g., "Microsoft.NETCore.App").</summary>
            public PlatformString name;

            /// <summary>Pointer to the framework version string (e.g., "8.0.0").</summary>
            public PlatformString version;

            /// <summary>Pointer to the full path to the framework directory.</summary>
            public PlatformString path;
        }

        /// <summary>
        ///  Contains information about an installed SDK, returned by <see cref="hostfxr_dotnet_environment_info"/>.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   <b>Memory ownership:</b> This structure and all referenced strings are owned by the native hostfxr
        ///   library. They are valid only for the duration of the callback invocation. The caller must copy
        ///   any data it needs to retain. Do not attempt to free any pointers in this structure.
        ///  </para>
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        internal struct hostfxr_dotnet_environment_sdk_info
        {
            /// <summary>The size of this structure in bytes.</summary>
            public nint size;

            /// <summary>Pointer to the SDK version string (e.g., "8.0.100").</summary>
            public PlatformString version;

            /// <summary>Pointer to the full path to the SDK directory.</summary>
            public PlatformString path;
        }

        /// <summary>
        ///  Callback delegate for receiving environment information from <c>hostfxr_get_dotnet_environment_info</c>.
        /// </summary>
        /// <param name="info">Pointer to a <see cref="hostfxr_dotnet_environment_info"/> structure.</param>
        /// <param name="result_context">The context pointer passed to the original function call.</param>
        /// <remarks>
        ///  <para>
        ///   All data referenced by the info structure is valid only for the duration of the callback.
        ///   Copy any needed data before returning.
        ///  </para>
        /// </remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void hostfxr_get_dotnet_environment_info_result_fn(ref hostfxr_dotnet_environment_info info, nint result_context);

        /// <summary>
        ///  Gets comprehensive information about the .NET environment including installed SDKs and frameworks.
        /// </summary>
        /// <param name="dotnetRoot">Path to the .NET installation root, or <c>null</c> to use the default or registered location.</param>
        /// <param name="reserved">Reserved for future use. Must be zero.</param>
        /// <param name="result">Callback invoked with environment information.</param>
        /// <param name="resultContext">Arbitrary context pointer passed through to the callback.</param>
        /// <returns>
        ///  <see cref="StatusCode.Success"/> on success, or an error status code.
        /// </returns>
        /// <remarks>
        ///  <para>
        ///  The callback receives a pointer to a <see cref="hostfxr_dotnet_environment_info"/> structure
        ///  containing hostfxr version information, installed SDKs, and installed frameworks.
        ///  All data is valid only for the duration of the callback.
        ///  </para>
        /// </remarks>
#if NET
        [LibraryImport(Constants.HostFxr, StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(PlatformStringMarshaller))]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial int hostfxr_get_dotnet_environment_info(
#else
        [DllImport(Constants.HostFxr, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int hostfxr_get_dotnet_environment_info(
#endif
            string? dotnetRoot,
            nint reserved,
            hostfxr_get_dotnet_environment_info_result_fn result,
            nint resultContext);

        /// <summary>
        ///  Callback delegate for receiving error messages from the hosting layer.
        /// </summary>
        /// <param name="message">The error message text.</param>
        /// <remarks>
        ///  <para>
        ///   Set via <see cref="hostfxr_set_error_writer"/> to receive error messages that would
        ///   otherwise be written to stderr. Useful for capturing diagnostics in GUI applications
        ///   or custom logging scenarios.
        ///  </para>
        /// </remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void hostfxr_error_writer_fn(PlatformString message);

        /// <summary>
        ///  Sets a callback for receiving error messages from the hosting layer.
        /// </summary>
        /// <param name="error_writer">
        ///  Callback to receive error messages, or <c>null</c> to restore the default behavior (writing to stderr).
        /// </param>
        /// <returns>
        ///  A pointer to the previously registered error writer callback, or zero if none was set.
        /// </returns>
        /// <remarks>
        ///  <para>
        ///   Use this to capture error messages in GUI applications or for custom logging scenarios
        ///   where stderr is not appropriate.
        ///  </para>
        /// </remarks>
#if NET
        [LibraryImport(Constants.HostFxr)]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static unsafe partial delegate* unmanaged[Cdecl]<PlatformString, void> hostfxr_set_error_writer(
#else
        [DllImport(Constants.HostFxr, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe delegate* unmanaged[Cdecl]<PlatformString, void>  hostfxr_set_error_writer(
#endif
            delegate* unmanaged[Cdecl]<PlatformString, void> error_writer);

        /// <summary>
        ///  Callback delegate for receiving SDK resolution results from <see cref="hostfxr_resolve_sdk2"/>.
        /// </summary>
        /// <param name="key">Identifies the type of data being provided.</param>
        /// <param name="value">The string value associated with the key.</param>
        /// <remarks>
        ///  <para>
        ///   This callback may be invoked multiple times with different keys during a single
        ///   resolution operation. The string values are valid only for the duration of the callback.
        ///  </para>
        /// </remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void hostfxr_resolve_sdk2_result_fn(hostfxr_resolve_sdk2_result_key_t key, PlatformString value);

        /// <summary>
        ///  Resolves the SDK directory with detailed results provided via callback.
        /// </summary>
        /// <param name="exeDir">
        ///  Directory containing the dotnet executable, or <see langword="null"/>/empty for default search.
        /// </param>
        /// <param name="workingDir">
        ///  Directory to start searching for global.json, or <see langword="null"/>/empty to disable global.json search.
        /// </param>
        /// <param name="flags">
        ///  Resolution flags controlling behavior (e.g., <see cref="hostfxr_resolve_sdk2_flags_t.disallow_prerelease"/>).
        /// </param>
        /// <param name="result">Dictionary with resolution results.</param>
        /// <returns>
        ///  <see cref="StatusCode.Success"/> if the SDK was resolved, or <see cref="StatusCode.SdkResolveFailure"/> if not found.
        /// </returns>
#if NET
        [LibraryImport(
            Constants.HostFxr,
            EntryPoint = "hostfxr_resolve_sdk2",
            StringMarshalling = StringMarshalling.Custom,
            StringMarshallingCustomType = typeof(PlatformStringMarshaller))]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial StatusCode hostfxr_resolve_sdk2(
#else
        [DllImport(
            Constants.HostFxr,
            EntryPoint = "hostfxr_resolve_sdk2",
            CharSet = CharSet.Unicode,
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern StatusCode hostfxr_resolve_sdk2(
#endif
            string? exeDir,
            string? workingDir,
            hostfxr_resolve_sdk2_flags_t flags,
            hostfxr_resolve_sdk2_result_fn result);

        /// <summary>
        ///  Callback delegate for receiving available SDK list from <see cref="hostfxr_get_available_sdks"/>.
        /// </summary>
        /// <param name="sdk_count">The number of SDKs in the array.</param>
        /// <param name="sdk_dirs">Pointer to an array of pointers to null-terminated SDK directory path strings.</param>
        /// <remarks>
        ///  <para>
        ///   The SDKs are returned in ascending version order. The array and string data are valid
        ///   only for the duration of the callback.
        ///  </para>
        /// </remarks>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void hostfxr_get_available_sdks_result_fn(int sdk_count, nint sdk_dirs);

        /// <summary>
        ///  Gets all installed SDKs in ascending version order.
        /// </summary>
        /// <param name="exeDir">Path to the dotnet executable directory.</param>
        /// <param name="result">List of SDK directories.</param>
        /// <returns>
        ///  Always returns <see cref="StatusCode.Success"/>.
        /// </returns>
        /// <remarks>
        ///  <para>
        ///   The SDKs are returned in ascending version order. This is useful for the MSBuild SDK resolver
        ///   to find a compatible SDK when the latest SDK is incompatible.
        ///  </para>
        /// </remarks>
        internal static StatusCode hostfxr_get_available_sdks(string? exeDir, out string[] result)
        {
            string[]? localResult = null;
            StatusCode status = hostfxr_get_available_sdks_private(exeDir, SdkCallback);
            result = localResult ?? [];
            return status;

            unsafe void SdkCallback(int sdk_count, nint sdk_dirs)
            {
                localResult = new string[sdk_count];

                for (int i = 0; i < sdk_count; i++)
                {
                    localResult[i] = ((PlatformString*)sdk_dirs)[i];
                }
            }
        }

#if NET
        [LibraryImport(
            Constants.HostFxr,
            EntryPoint = "hostfxr_get_available_sdks",
            StringMarshalling = StringMarshalling.Custom,
            StringMarshallingCustomType = typeof(PlatformStringMarshaller))]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        private static partial StatusCode hostfxr_get_available_sdks_private(
#else
        [DllImport(
            Constants.HostFxr,
            EntryPoint = "hostfxr_get_available_sdks",
            CharSet = CharSet.Unicode,
            CallingConvention = CallingConvention.Cdecl)]
        private static extern StatusCode hostfxr_get_available_sdks_private(
#endif
            string? exeDir,
            hostfxr_get_available_sdks_result_fn result);

#if NET
        public static partial class Unix
        {
            [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
            [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
            private static partial nint realpath(string path, nint buffer);

            [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
            [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
            private static partial void free(nint ptr);

            public static string? realpath(string path)
            {
                nint ptr = realpath(path, nint.Zero);
                string? result = Marshal.PtrToStringUTF8(ptr);
                free(ptr);
                return result;
            }
        }
#endif
    }
}
