// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.PrintEnvScript;

/// <summary>
/// Provides shell-specific environment configuration scripts.
/// </summary>
public interface IEnvShellProvider
{
    /// <summary>
    /// The name of this shell as exposed on the command line arguments.
    /// </summary>
    string ArgumentName { get; }

    /// <summary>
    /// The file extension typically used for this shell's scripts (sans period).
    /// </summary>
    string Extension { get; }

    /// <summary>
    /// This will be used when specifying the shell in CLI help text.
    /// </summary>
    string? HelpDescription { get; }

    /// <summary>
    /// Generates a shell-specific script that configures PATH and DOTNET_ROOT.
    /// </summary>
    /// <param name="dotnetInstallPath">The path to the .NET installation directory</param>
    /// <returns>A shell script that can be sourced to configure the environment</returns>
    string GenerateEnvScript(string dotnetInstallPath);
}
