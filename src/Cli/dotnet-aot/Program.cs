using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli;

public class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet-aot <hostfxr_path> <dotnet_path> ...");
            return 1;
        }
        string hostfxrPath = args[0];
        string dotnetPath = args[1];

        Console.WriteLine("Hostfxr path: " + hostfxrPath);
        if (Path.GetFileNameWithoutExtension(hostfxrPath) is not ("libhostfxr" or "hostfxr"))
        {
            Console.WriteLine("Error: The provided path is not a valid hostfxr file.");
            return 1;
        }
        if (!File.Exists(hostfxrPath))
        {
            Console.WriteLine($"Error: The provided path does not exist: {hostfxrPath}");
            return 1;
        }
        Console.WriteLine("Dotnet path: " + dotnetPath);
        if (Path.GetFileName(dotnetPath) is not "dotnet.dll")
        {
            Console.WriteLine("Error: The provided path is not a valid dotnet file.");
            return 1;
        }
        if (!File.Exists(dotnetPath))
        {
            Console.WriteLine($"Error: The provided path does not exist: {dotnetPath}");
            return 1;
        }

        return RunSdk(hostfxrPath, dotnetPath, args[2..]);
    }

    /// <summary>
    /// Run the CLI SDK with the provided arguments.
    /// </summary>
    /// <param name="dotnetDllPathPtr">
    /// Null-terminated absolute path to dotnet.dll. Wstring on Windows, UTF-8 on Unix.
    /// </param>
    /// <param name="sdkArgsPtr">Pointer to array of null-terminated command line args.</param>
    /// <param name="sdkArgc">Length of command line arg array.</param>
    [UnmanagedCallersOnly(EntryPoint = "run_sdk")]
    public unsafe static int RunSdk(
        IntPtr hostfxrPathPtr,
        IntPtr dotnetDllPathPtr,
        IntPtr* sdkArgsPtr,
        int sdkArgc)
    {
        var hostfxrPath = Marshal.PtrToStringAuto(hostfxrPathPtr)
            ?? throw new ArgumentNullException(nameof(hostfxrPathPtr));
        var dotnetDllPath = Marshal.PtrToStringAuto(dotnetDllPathPtr)
            ?? throw new ArgumentNullException(nameof(dotnetDllPathPtr));
        var sdkArgs = new string[sdkArgc];
        for (int i = 0; i < sdkArgc; i++)
        {
            var arg = Marshal.PtrToStringAuto(sdkArgsPtr[i])
                ?? throw new ArgumentNullException(nameof(sdkArgsPtr), $"Argument {i} is null");
            sdkArgs[i] = arg;
        }
        return RunSdk(
            hostfxrPath,
            dotnetDllPath,
            sdkArgs);
    }

    private static int RunSdk(
        string hostfxrPath,
        string dotnetDllPath,
        string[] sdkArgs)
    {
        if (TryRunAotCli(dotnetDllPath, sdkArgs, out int exitCode))
        {
            // If these arguments were be handled by the AOT CLI, run directly. Otherwise
            // bail out to load CoreCLR and run the full CLI.
            return exitCode;
        }

        return LoadClrAndRun(hostfxrPath, dotnetDllPath, sdkArgs);
    }

    private static bool TryRunAotCli(string dotnetDllPath, string[] sdkArgs, out int exitCode)
    {
        // Only supported command right now is "--version"
        if (sdkArgs is ["--version"])
        {
            CommandLineInfo.PrintVersion(dotnetDllPath);
            exitCode = 0;
            return true;
        }
        exitCode = 0;
        return false;
    }

    public static int LoadClrAndRun(string hostfxrPath, string dotnetPath, string[] dotnetArgs)
    {
        // We need CoreCLR. Load it and run 'dotnet.dll'
        var hostFxr = HostFxr.Load(hostfxrPath);
        return hostFxr.Run(dotnetPath, dotnetArgs);
    }
}
