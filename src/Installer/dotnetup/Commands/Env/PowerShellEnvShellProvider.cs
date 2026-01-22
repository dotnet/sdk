// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

public class PowerShellEnvShellProvider : IEnvShellProvider
{
    public string ArgumentName => "pwsh";

    public string Extension => "ps1";

    public string? HelpDescription => "PowerShell Core (pwsh)";

    public override string ToString() => ArgumentName;

    public string GenerateEnvScript(string dotnetInstallPath)
    {
        return
            $"""
            # This script configures the environment for .NET installed at {dotnetInstallPath}
            # Source this script (dot-source) to add .NET to your PATH and set DOTNET_ROOT
            # Example: . ./dotnet-env.ps1
            
            $env:DOTNET_ROOT = "{dotnetInstallPath}"
            $env:PATH = "{dotnetInstallPath}" + [IO.Path]::PathSeparator + $env:PATH
            """;
    }
}
