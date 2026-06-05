// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.DotNet.Cli;

static unsafe partial class NativeEntryPoint
{
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
            var parseResult = Parser.Parse(args);
            if (parseResult.Errors.Count == 0)
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
                exitCode = ManagedHost.RunApp(hostPath, dotnetRoot, hostfxrPath, appArgs);
                success = true;
                return exitCode;
            }

            // No managed fallback available
            Console.Error.WriteLine($"The managed fallback could not be located. Expected '{dotnetDll}' and '{runtimeConfig}'.");
            return exitCode;
        }
        finally
        {
            mainActivity?.AddTag("process.exit.code", exitCode);
            mainActivity?.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            mainActivity?.Stop();
            TelemetryClient.FlushProviders();
        }
    }
}
