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
        string sdkDir = baseDir;
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
            nint sdNative = MarshalStringToNative(sdkDir);
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
