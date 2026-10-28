// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if TARGET_WINDOWS
using Microsoft.DotNet.Cli.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;
#endif

namespace Microsoft.DotNet.Cli;

/// <summary>
///  Resolves the versioned SDK directory for the Native AOT bridge. Prefers the SDK directory the
///  host (the muxer, or the <c>dn</c> harness) passed in, and falls back to self-locating the
///  directory of the running <c>dotnet-aot</c> native library on disk. See
///  <c>src/Cli/dotnet-aot/SdkRootResolution.md</c>.
/// </summary>
internal static unsafe partial class SdkRootLocator
{
    /// <summary>
    ///  Resolves the versioned SDK directory. When the host supplies a non-empty
    ///  <paramref name="sdkDirArgument"/> it is authoritative (and, in debug builds, is asserted to
    ///  match where <c>dotnet-aot</c> was actually loaded from). Otherwise the directory is
    ///  self-located from the native library's own module path.
    /// </summary>
    /// <returns>The resolved SDK directory, or an empty string when it cannot be determined.</returns>
    internal static string Resolve(string sdkDirArgument)
    {
        if (!string.IsNullOrEmpty(sdkDirArgument))
        {
            AssertMatchesSelfLocation(sdkDirArgument);
            return sdkDirArgument;
        }

        return TrySelfLocateSdkDirectory() ?? string.Empty;
    }

    /// <summary>
    ///  Returns the directory of the running <c>dotnet-aot</c> native library, or
    ///  <see langword="null"/> when it cannot be determined - for example when this code runs
    ///  JIT-compiled in unit tests rather than as part of the NativeAOT image.
    /// </summary>
    internal static string? TrySelfLocateSdkDirectory()
    {
        // Only meaningful in a NativeAOT image. Under the JIT the module that owns this code is the
        // test/runtime host, not dotnet-aot.dll, so a self-location would be misleading.
        if (RuntimeFeature.IsDynamicCodeSupported)
        {
            return null;
        }

        try
        {
            string? modulePath = OperatingSystem.IsWindows()
                ? GetSelfModulePathWindows()
                : GetSelfModulePathPosix();

            return string.IsNullOrEmpty(modulePath) ? null : Path.GetDirectoryName(modulePath);
        }
        catch
        {
            // The self-locate is a best-effort fallback; it must never fault the CLI.
            return null;
        }
    }

    [Conditional("DEBUG")]
    private static void AssertMatchesSelfLocation(string sdkDirArgument)
    {
        string? selfLocated = TrySelfLocateSdkDirectory();
        Debug.Assert(
            selfLocated is null || PathsEqual(sdkDirArgument, selfLocated),
            $"Host-provided sdk_dir '{sdkDirArgument}' does not match the directory dotnet-aot was loaded from ('{selfLocated}'). " +
            "The host must pass the versioned SDK directory that contains the dotnet-aot binary.");
    }

    // The address of a method in this assembly identifies the native module that contains it.
    // [UnmanagedCallersOnly] gives the method a stable native entry point in the module to hand to
    // GetModuleHandleEx; it is never called, only its address is taken.
    [UnmanagedCallersOnly]
    private static void SelfAnchor() { }

    private static string? GetSelfModulePathWindows()
    {
#if TARGET_WINDOWS
        nint address = (nint)(delegate* unmanaged<void>)&SelfAnchor;
        HMODULE module;
        if (!PInvoke.GetModuleHandleEx(
                PInvoke.GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | PInvoke.GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                new PCWSTR((char*)address),
                &module))
        {
            // GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS resolves the module from an address in it;
            // GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT means we hold no reference (do not free it).
            return null;
        }

        // GetModuleFileName truncates (returning the buffer length and setting ERROR_INSUFFICIENT_BUFFER)
        // when the path does not fit, so grow the buffer and retry. Stack-allocate MAX_PATH for the common
        // case; a deliberately tiny buffer in debug builds forces the grow path so the AOT tests exercise it.
#if DEBUG
        using BufferScope<char> buffer = new(stackalloc char[8]);
#else
        using BufferScope<char> buffer = new(stackalloc char[(int)PInvoke.MAX_PATH]);
#endif
        while (true)
        {
            uint length = PInvoke.GetModuleFileName(module, buffer.AsSpan());
            if (length == 0)
            {
                return null;
            }

            if (length < (uint)buffer.Length)
            {
                return buffer.Slice(0, (int)length).ToString();
            }

            // The path did not fit (length == buffer capacity); grow and retry.
            buffer.EnsureCapacity(buffer.Length * 2);
        }
#else
        // CsWin32 (hence the Windows self-locate) is unavailable off Windows; callers fall back to
        // the host-provided sdk_dir.
        return null;
#endif
    }

    private static string? GetSelfModulePathPosix()
    {
        // Best-effort on Unix: dladdr resolves the shared object containing the given address. It is
        // mapped to the platform C runtime (libSystem/libdl/libc) by ResolveDlImport; if none load, the
        // catch in TrySelfLocateSdkDirectory falls back to the host-provided sdk_dir.
        nint address = (nint)(delegate* unmanaged<void>)&SelfAnchor;
        if (dladdr(address, out DlInfo info) == 0 || info.dli_fname == 0)
        {
            return null;
        }

        return Marshal.PtrToStringUTF8(info.dli_fname);
    }

    private static bool PathsEqual(string left, string right)
    {
        static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(Normalize(left), Normalize(right), comparison);
    }

    // dladdr does not live in the same library across Unix flavors - libSystem on macOS, libdl on
    // glibc, libc on musl and glibc 2.34+ - so it is imported under a sentinel name that a
    // DllImportResolver (registered in the static constructor) maps to the first library that loads.
    private const string DlLibrary = "dotnet-aot-dl";

    static SdkRootLocator()
    {
        if (!OperatingSystem.IsWindows())
        {
            NativeLibrary.SetDllImportResolver(typeof(SdkRootLocator).Assembly, ResolveDlImport);
        }
    }

    private static nint ResolveDlImport(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != DlLibrary)
        {
            return 0;
        }

        string[] candidates = OperatingSystem.IsMacOS()
            ? ["libSystem.dylib"]
            : ["libdl.so.2", "libc.so.6", "libc.so"];

        foreach (string candidate in candidates)
        {
            if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out nint handle))
            {
                return handle;
            }
        }

        return 0;
    }

    [LibraryImport(DlLibrary, EntryPoint = "dladdr")]
    private static partial int dladdr(nint addr, out DlInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct DlInfo
    {
        public nint dli_fname;
        public nint dli_fbase;
        public nint dli_sname;
        public nint dli_saddr;
    }
}
