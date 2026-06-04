// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.DotNet.Cli;

static unsafe partial class NativeEntryPoint
{
    /// <summary>
    ///  When set by the native entry point, commands use this instead of
    ///  discovering the dotnet root via PATH / environment probing.
    /// </summary>
    internal static string? DotnetRoot { get; set; }

    [UnmanagedCallersOnly(EntryPoint = "dotnet_execute")]
    static int Execute(
        nint hostPathPtr,      // const char_t* host_path
        nint dotnetRootPtr,    // const char_t* dotnet_root
        nint sdkDirPtr,        // const char_t* sdk_dir
        nint hostfxrPathPtr,   // const char_t* hostfxr_path
        int argc,              // int argc (user args, no dotnet exe)
        nint argvPtr)          // const char_t** argv
    {
        try
        {
            return ExecuteCore(hostPathPtr, dotnetRootPtr, sdkDirPtr, hostfxrPathPtr, argc, argvPtr);
        }
        catch (Exception e)
        {
            // No managed exception must escape an [UnmanagedCallersOnly] method.
            try { Console.Error.WriteLine(e.Message); } catch { }
            return 1;
        }
    }

    static int ExecuteCore(
        nint hostPathPtr,
        nint dotnetRootPtr,
        nint sdkDirPtr,
        nint hostfxrPathPtr,
        int argc,
        nint argvPtr)
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

        return ExecuteCore(hostPath, dotnetRoot, sdkDir, hostfxrPath, args);
    }

    /// <summary>
    ///  Core execution logic, separated from native marshalling for testability.
    /// </summary>
    internal static int ExecuteCore(
        string hostPath, string dotnetRoot, string sdkDir,
        string hostfxrPath, string[] args)
    {
        // Make hostfxr discoverable for NativeWrapper P/Invokes (required on non-Windows)
        if (!string.IsNullOrEmpty(hostfxrPath))
        {
            AppContext.SetData("HOSTFXR_PATH", hostfxrPath);
        }

        // Try the AOT-compiled path for supported commands (if enabled)
        if (EnvironmentVariableParser.ParseBool(Environment.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_ENABLEAOT), defaultValue: false))
        {
            DotnetRoot = string.IsNullOrEmpty(dotnetRoot) ? null : dotnetRoot;
            try
            {
                var parseResult = Parser.Parse(args);
                if (parseResult.Errors.Count == 0)
                {
                    return Parser.Invoke(parseResult);
                }
            }
            catch (Exception e) when (e.ShouldBeDisplayedAsError())
            {
                Reporter.Error.WriteLine(e.Message);
                return 1;
            }
            catch (Exception e)
            {
                try
                {
                    return Parser.Invoke(parseResult);
                }
                catch (CommandNotAvailableInAotException)
                {
                    // Command requires managed CLI — fall through to managed fallback below.
                }
                catch (Utils.GracefulException ex)
                {
                    Reporter.Error.WriteLine(ex.Message.Red());
                    return 1;
                }
            }
        }

        // Fall back to the fully managed dotnet CLI by hosting .NET
        string dotnetDll = Path.Join(sdkDir, "dotnet.dll");
        string runtimeConfig = Path.Join(sdkDir, "dotnet.runtimeconfig.json");

        if (File.Exists(dotnetDll) && File.Exists(runtimeConfig))
        {
            // Use the command-line hosting path to run dotnet.dll
            string[] appArgs = new string[args.Length + 1];
            appArgs[0] = dotnetDll;
            Array.Copy(args, 0, appArgs, 1, args.Length);
            return ManagedHost.RunApp(hostPath, dotnetRoot, hostfxrPath, appArgs);
        }

        // No managed fallback available
        Console.Error.WriteLine($"The managed fallback could not be located. Expected '{dotnetDll}' and '{runtimeConfig}'.");
        return 1;
    }
}
