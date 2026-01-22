// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal class EnvCommand : CommandBase
{
    private readonly IEnvShellProvider _shellProvider;
    private readonly string? _dotnetInstallPath;
    private readonly IDotnetInstallManager _dotnetInstaller;

    public EnvCommand(ParseResult result, IDotnetInstallManager? dotnetInstaller = null) : base(result)
    {
        _dotnetInstaller = dotnetInstaller ?? new DotnetInstallManager();
        _shellProvider = result.GetValue(EnvCommandParser.ShellOption)!; // this cannot be null due to the way the ShellOption is defined/configured
        _dotnetInstallPath = result.GetValue(EnvCommandParser.DotnetInstallPathOption);
    }

    public override int Execute()
    {
        try
        {
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
