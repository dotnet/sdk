// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.PrintEnvScript;

internal class PrintEnvScriptCommand : CommandBase
{
    private readonly IEnvShellProvider? _shellProvider;
    private readonly string? _dotnetInstallPath;
    private readonly IDotnetInstallManager _dotnetInstaller;

    public PrintEnvScriptCommand(ParseResult result, IDotnetInstallManager? dotnetInstaller = null) : base(result)
    {
        _dotnetInstaller = dotnetInstaller ?? new DotnetInstallManager();
        _shellProvider = result.GetValue(PrintEnvScriptCommandParser.ShellOption);
        _dotnetInstallPath = result.GetValue(PrintEnvScriptCommandParser.DotnetInstallPathOption);
    }

    public override int Execute()
    {
        try
        {
            // Check if shell provider was successfully determined
            if (_shellProvider == null)
            {
                var shellPath = Environment.GetEnvironmentVariable("SHELL");
                if (shellPath is null)
                {
                    Console.Error.WriteLine("Error: Unable to detect current shell. The SHELL environment variable is not set.");
                    Console.Error.WriteLine($"Please specify the shell using --shell option. Supported shells: {string.Join(", ", PrintEnvScriptCommandParser.SupportedShells.Select(s => s.ArgumentName))}");
                }
                else
                {
                    var shellName = Path.GetFileName(shellPath);
                    Console.Error.WriteLine($"Error: Unsupported shell '{shellName}'.");
                    Console.Error.WriteLine($"Supported shells: {string.Join(", ", PrintEnvScriptCommandParser.SupportedShells.Select(s => s.ArgumentName))}");
                    Console.Error.WriteLine("Please specify the shell using --shell option.");
                }
                return 1;
            }

            // Determine the dotnet install path
            string installPath = _dotnetInstallPath ?? _dotnetInstaller.GetDefaultDotnetInstallPath();

            // Generate the shell script
            string script = _shellProvider.GenerateEnvScript(installPath);

            // Output the script to stdout
            Console.WriteLine(script);

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error generating environment script: {ex.Message}");
            return 1;
        }
    }
}
