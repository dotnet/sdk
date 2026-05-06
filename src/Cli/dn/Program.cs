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
        // Check DOTNET_ROOT first (standard on all platforms)
        string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(dotnetRoot) && Directory.Exists(dotnetRoot))
        {
            return dotnetRoot;
        }

        // On Windows, also check the architecture-specific variant
        if (OperatingSystem.IsWindows())
        {
            string archVar = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "DOTNET_ROOT(x64)",
                Architecture.X86 => "DOTNET_ROOT(x86)",
                Architecture.Arm64 => "DOTNET_ROOT(ARM64)",
                _ => ""
            };

            if (!string.IsNullOrEmpty(archVar))
            {
                dotnetRoot = Environment.GetEnvironmentVariable(archVar);
                if (!string.IsNullOrEmpty(dotnetRoot) && Directory.Exists(dotnetRoot))
                {
                    return dotnetRoot;
                }
            }
        }

        // Fall back to resolving from the process path
        string? processPath = Environment.ProcessPath;
        if (processPath is not null)
        {
            string? processDir = Path.GetDirectoryName(processPath);
            if (processDir is not null)
            {
                // Walk up looking for a directory with dotnet(.exe)
                string? candidate = processDir;
                while (candidate is not null)
                {
                    if (File.Exists(Path.Combine(candidate, "dotnet" + (OperatingSystem.IsWindows() ? ".exe" : ""))))
                    {
                        return candidate;
                    }
                    candidate = Path.GetDirectoryName(candidate);
                }
            }
        }

        // Last resort: assume relative to AppContext.BaseDirectory
        return Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)) ?? AppContext.BaseDirectory;
    }

    /// <summary>
    ///  Finds the hostfxr library path under the given .NET root.
    /// </summary>
    private static string ResolveHostfxrPath(string dotnetRoot)
    {
        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        if (!Directory.Exists(fxrDir))
        {
            return string.Empty;
        }

        // Pick the highest version directory by parsing version numbers
        string? latestFxr = Directory.GetDirectories(fxrDir)
            .Select(path => new
            {
                Path = path,
                Version = Version.TryParse(Path.GetFileName(path), out Version? version) ? version : null
            })
            .Where(candidate => candidate.Version is not null)
            .OrderByDescending(candidate => candidate.Version)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();

        if (latestFxr is null)
        {
            return string.Empty;
        }

        string hostfxrName = OperatingSystem.IsWindows()
            ? "hostfxr.dll"
            : OperatingSystem.IsMacOS()
                ? "libhostfxr.dylib"
                : "libhostfxr.so";

        string hostfxrPath = Path.Combine(latestFxr, hostfxrName);
        return File.Exists(hostfxrPath) ? hostfxrPath : string.Empty;
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
