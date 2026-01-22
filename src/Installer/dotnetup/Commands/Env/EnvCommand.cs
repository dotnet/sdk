// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal class EnvCommand : CommandBase
{
    private readonly string _shellName;
    private readonly string? _dotnetInstallPath;
    private readonly IDotnetInstallManager _dotnetInstaller;

    public EnvCommand(ParseResult result, IDotnetInstallManager? dotnetInstaller = null) : base(result)
    {
        _dotnetInstaller = dotnetInstaller ?? new DotnetInstallManager();
        _shellName = result.GetValue(EnvCommandParser.ShellOption)!;
        _dotnetInstallPath = result.GetValue(EnvCommandParser.DotnetInstallPathOption);
    }

    public override int Execute()
    {
        try
        {
            // Find the shell provider
            var shellProvider = EnvCommandParser.SupportedShells.FirstOrDefault(s => 
                s.ArgumentName.Equals(_shellName, StringComparison.OrdinalIgnoreCase));

            if (shellProvider == null)
            {
                Console.Error.WriteLine($"Error: Unsupported shell '{_shellName}'. Supported shells: {string.Join(", ", EnvCommandParser.SupportedShells.Select(s => s.ArgumentName))}");
                return 1;
            }

            // Determine the dotnet install path
            string installPath = _dotnetInstallPath ?? _dotnetInstaller.GetDefaultDotnetInstallPath();

            // Generate the shell script
            string script = shellProvider.GenerateEnvScript(installPath);

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
