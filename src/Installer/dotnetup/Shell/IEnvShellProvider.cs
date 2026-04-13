// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Shell;

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
    /// Generates a shell-specific script that configures the environment.
    /// </summary>
    /// <param name="dotnetInstallPath">The path to the .NET installation directory</param>
    /// <param name="dotnetupDir">The directory containing the dotnetup binary, or null to omit</param>
    /// <param name="includeDotnet">When true, sets DOTNET_ROOT and adds dotnet to PATH. When false, only adds dotnetup to PATH.</param>
    /// <returns>A shell script that can be sourced to configure the environment</returns>
    string GenerateEnvScript(string dotnetInstallPath, string? dotnetupDir = null, bool includeDotnet = true);

    /// <summary>
    /// Returns the profile file paths that should be modified for this shell.
    /// For bash, this may return multiple files (e.g., ~/.bashrc and a login profile).
    /// </summary>
    IReadOnlyList<string> GetProfilePaths();

    /// <summary>
    /// Generates the line(s) to append to a shell profile that will eval dotnetup's env script.
    /// Includes a marker comment for identification and removal.
    /// </summary>
    /// <param name="dotnetupPath">The full path to the dotnetup binary</param>
    /// <param name="dotnetupOnly">When true, the profile entry only adds dotnetup to PATH (no DOTNET_ROOT or dotnet PATH).</param>
    string GenerateProfileEntry(string dotnetupPath, bool dotnetupOnly = false);

    /// <summary>
    /// Generates a command that the user can paste into the current terminal to activate .NET.
    /// </summary>
    /// <param name="dotnetupPath">The full path to the dotnetup binary</param>
    /// <param name="dotnetupOnly">When true, the command only adds dotnetup to PATH.</param>
    string GenerateActivationCommand(string dotnetupPath, bool dotnetupOnly = false);
}
