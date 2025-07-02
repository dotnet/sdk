// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;

class Program
{
    static int Main(string[] args)
    {
        // Get the path to the current executable
        string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (exePath == null)
        {
            Console.Error.WriteLine("Could not determine the path to the current executable.");
            return 1;
        }
        string? exeDir = Path.GetDirectoryName(exePath);
        if (exeDir == null)
        {
            Console.Error.WriteLine("Could not determine the directory of the current executable.");
            return 1;
        }
        // Path to dotnet in the same directory
        string dotnetPath = Path.Combine(exeDir, "dotnet");
#if WINDOWS
        dotnetPath += ".exe";
#endif
        // Build argument list: "dnx" + all args
        string arguments = "dnx";
        if (args.Length > 0)
        {
            arguments += " " + string.Join(" ", args.Select(a => $"\"{a.Replace("\"", "\\\"")}"));
        }
        var psi = new ProcessStartInfo
        {
            FileName = dotnetPath,
            Arguments = arguments,
            UseShellExecute = false
        };
        try
        {
            using var process = Process.Start(psi);
            if (process == null)
            {
                Console.Error.WriteLine($"Failed to start process: {dotnetPath}");
                return 1;
            }
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error launching process: {ex.Message}");
            return 1;
        }
    }
}
