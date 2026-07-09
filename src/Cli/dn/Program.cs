// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Cli;

partial class Program
{
    [LibraryImport("dotnet-aot", EntryPoint = "dotnet_execute")]
    private static partial int DotnetExecute(
        nint hostPath,
        nint dotnetRoot,
        nint sdkDir,
        nint hostfxrPath,
        int argc,
        nint argv);

    static unsafe int Main(string[] args)
    {
        string hostPath = Environment.ProcessPath!;
        string baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string dotnetRoot = ResolveDotnetRoot();

        // The muxer loads dotnet-aot from the resolved SDK directory. For local testing, allow the
        // harness to emulate the deployed (non-flat) layout - where dotnet-aot lives in sdk\<version>\
        // while dn stays in the parent - via DOTNET_AOT_SDK_DIR, which overrides both where the
        // library is loaded from and the sdk_dir passed to dotnet_execute. Defaults to dn's own
        // directory (the flat layout).
        string sdkDir = ResolveAotSdkDir(baseDir);
        if (!string.Equals(sdkDir, baseDir, StringComparison.OrdinalIgnoreCase))
        {
            NativeLibrary.SetDllImportResolver(typeof(Program).Assembly, (name, assembly, searchPath) =>
                string.Equals(name, "dotnet-aot", StringComparison.Ordinal)
                    && NativeLibrary.TryLoad(Path.Combine(sdkDir, AotLibraryFileName), out nint handle)
                        ? handle
                        : nint.Zero);
        }

        // Test hook: pass an empty sdk_dir to exercise dotnet-aot's self-locate fallback while still
        // loading the library from the resolved directory above.
        string sdkDirArg = string.Equals(Environment.GetEnvironmentVariable("DOTNET_AOT_BLANK_SDKDIR"), "1", StringComparison.Ordinal)
            ? string.Empty
            : sdkDir;
        string hostfxrPath = ResolveHostfxrPath(dotnetRoot);

        // Marshal argv to native platform strings (UTF-16 on Windows, UTF-8 on Unix)
        // to match hostfxr's char_t definition used by PlatformStringMarshaller
        // in dotnet-aot.dll.
        nint* nativeArgv = stackalloc nint[args.Length];
        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                nativeArgv[i] = MarshalStringToNative(args[i]);
            }

            nint hpNative = MarshalStringToNative(hostPath);
            nint drNative = MarshalStringToNative(dotnetRoot);
            nint sdNative = MarshalStringToNative(sdkDirArg);
            nint hfNative = MarshalStringToNative(hostfxrPath);

            try
            {
                return DotnetExecute(
                    hpNative,
                    drNative,
                    sdNative,
                    hfNative,
                    args.Length,
                    (nint)nativeArgv);
            }
            finally
            {
                Marshal.FreeCoTaskMem(hpNative);
                Marshal.FreeCoTaskMem(drNative);
                Marshal.FreeCoTaskMem(sdNative);
                Marshal.FreeCoTaskMem(hfNative);
            }
        }
        finally
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (nativeArgv[i] != 0)
                {
                    Marshal.FreeCoTaskMem(nativeArgv[i]);
                }
            }
        }
    }

    /// <summary>
    ///  Resolves the .NET installation root directory, mimicking muxer behavior.
    /// </summary>
    private static string ResolveDotnetRoot()
    {
        return DotnetRootResolver.ResolveDotnetRoot(
            Environment.GetEnvironmentVariable,
            Environment.ProcessPath,
            RuntimeInformation.ProcessArchitecture,
            OperatingSystem.IsWindows(),
            Directory.Exists,
            File.Exists,
            AppContext.BaseDirectory);
    }

    /// <summary>
    ///  Finds the hostfxr library path under the given .NET root.
    /// </summary>
    private static string ResolveHostfxrPath(string dotnetRoot)
    {
        return DotnetRootResolver.ResolveHostfxrPath(
            dotnetRoot,
            OperatingSystem.IsWindows(),
            OperatingSystem.IsMacOS(),
            Directory.Exists,
            Directory.GetDirectories,
            File.Exists);
    }

    /// <summary>
    ///  Resolves the directory dotnet-aot is loaded from (and passed as sdk_dir). Honors the
    ///  DOTNET_AOT_SDK_DIR override for emulating the deployed non-flat layout; otherwise defaults
    ///  to dn's own directory.
    /// </summary>
    private static string ResolveAotSdkDir(string baseDir)
    {
        string? overrideDir = Environment.GetEnvironmentVariable("DOTNET_AOT_SDK_DIR");
        return !string.IsNullOrEmpty(overrideDir) && Directory.Exists(overrideDir)
            ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(overrideDir))
            : baseDir;
    }

    /// <summary>
    ///  The platform-specific file name of the dotnet-aot native library.
    /// </summary>
    private static string AotLibraryFileName =>
        OperatingSystem.IsWindows() ? "dotnet-aot.dll"
        : OperatingSystem.IsMacOS() ? "dotnet-aot.dylib"
        : "dotnet-aot.so";

    /// <summary>
    ///  Marshals a string to a native platform string (UTF-16 on Windows, UTF-8 on Unix)
    ///  to match hostfxr's char_t definition.
    /// </summary>
    private static nint MarshalStringToNative(string value)
    {
        return OperatingSystem.IsWindows()
            ? Marshal.StringToCoTaskMemUni(value)
            : Marshal.StringToCoTaskMemUTF8(value);
    }
}
