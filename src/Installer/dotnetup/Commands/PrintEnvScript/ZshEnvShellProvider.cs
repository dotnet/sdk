// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.PrintEnvScript;

public class ZshEnvShellProvider : IEnvShellProvider
{
    public string ArgumentName => "zsh";

    public string Extension => "zsh";

    public string? HelpDescription => "Zsh shell";

    public override string ToString() => ArgumentName;

    public string GenerateEnvScript(string dotnetInstallPath)
    {
        // Escape single quotes in the path for zsh by replacing ' with '\''
        var escapedPath = dotnetInstallPath.Replace("'", "'\\''");

        return
            $"""
            #!/usr/bin/env zsh
            # This script configures the environment for .NET installed at {dotnetInstallPath}
            # Source this script to add .NET to your PATH and set DOTNET_ROOT
            
            export DOTNET_ROOT='{escapedPath}'
            export PATH='{escapedPath}':$PATH
            hash -d dotnet 2>/dev/null || true  # Clear cached dotnet path
            """;
    }
}
