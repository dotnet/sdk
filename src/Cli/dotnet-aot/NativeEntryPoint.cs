// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.DotNet.Cli;

static unsafe partial class NativeEntryPoint
{
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsDebuggerPresent();

    [UnmanagedCallersOnly(EntryPoint = "dotnet_execute")]
    static int Execute(
        nint hostPathPtr,      // const char_t* host_path
        nint dotnetRootPtr,    // const char_t* dotnet_root
        nint sdkDirPtr,        // const char_t* sdk_dir
        nint hostfxrPathPtr,   // const char_t* hostfxr_path
        int argc,              // int argc (user args, no dotnet exe)
        nint argvPtr)          // const char_t** argv
    {
        string hostPath = PlatformStringMarshaller.ConvertToManaged(hostPathPtr) ?? string.Empty;
        string dotnetRoot = PlatformStringMarshaller.ConvertToManaged(dotnetRootPtr) ?? string.Empty;
        string sdkDir = PlatformStringMarshaller.ConvertToManaged(sdkDirPtr) ?? string.Empty;
        string hostfxrPath = PlatformStringMarshaller.ConvertToManaged(hostfxrPathPtr) ?? string.Empty;

        string[] args = new string[argc];
        nint* argv = (nint*)argvPtr;
        for (int i = 0; i < argc; i++)
        {
            args[i] = PlatformStringMarshaller.ConvertToManaged(argv[i]) ?? string.Empty;
        }

        // Try the AOT-compiled path first for supported commands
        var parseResult = Parser.Parse(args);
        bool handled = parseResult.Errors.Count == 0;

        if (handled)
        {
            return Parser.Invoke(parseResult);
        }

        // If a native debugger is attached, signal the managed code to call Debugger.Launch()
        // so a managed debugger can be attached for mixed-mode debugging.
        if (IsNativeDebuggerAttached())
        {
            Environment.SetEnvironmentVariable("DOTNET_LAUNCH_MANAGED_DEBUGGER", "1");
        }

        // Fall back to the fully managed dotnet CLI by hosting .NET
        string dotnetDll = Path.Combine(sdkDir, "dotnet.dll");
        string runtimeConfig = Path.Combine(sdkDir, "dotnet.runtimeconfig.json");

        if (File.Exists(dotnetDll) && File.Exists(runtimeConfig))
        {
            // Use the command-line hosting path to run dotnet.dll
            string[] appArgs = new string[args.Length + 1];
            appArgs[0] = dotnetDll;
            Array.Copy(args, 0, appArgs, 1, args.Length);
            return ManagedHost.RunApp(dotnetRoot, appArgs);
        }

        // No managed fallback available
        return Program.Main(args);
    }

    /// <summary>
    ///  Checks if a native debugger is attached to the current process.
    /// </summary>
    private static bool IsNativeDebuggerAttached()
    {
        if (OperatingSystem.IsWindows())
        {
            return IsDebuggerPresent();
        }

        // On Linux, check TracerPid in /proc/self/status
        if (OperatingSystem.IsLinux())
        {
            try
            {
                string status = File.ReadAllText("/proc/self/status");
                foreach (string line in status.Split('\n'))
                {
                    if (line.StartsWith("TracerPid:", StringComparison.Ordinal))
                    {
                        string pid = line["TracerPid:".Length..].Trim();
                        return pid != "0";
                    }
                }
            }
            catch
            {
                // Ignore errors reading proc filesystem
            }
        }

        return false;
    }
}
