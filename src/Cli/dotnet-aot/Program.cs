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

        return RunCli(hostfxrPath, dotnetPath, args[2..]);
    }

    public static int RunCli(string hostfxrPath, string dotnetPath, string[] dotnetArgs)
    {
        // Try to handle AOT-compatible options inline
        // TODO: The dotnet CLI parser is not AOT-compatible because it includes running commands in
        // the same code paths as the parser. We need to separate the two.
        if (dotnetArgs.SequenceEqual([ "--info" ]))
        {
            CommandLineInfo.PrintInfo();
            return 0;
        }

        // We need CoreCLR. Load it and run 'dotnet.dll'
        var hostFxr = HostFxr.Load(hostfxrPath);
        return hostFxr.Run(dotnetPath, dotnetArgs);
    }
}
