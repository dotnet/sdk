// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Manages the dotnet installation root configuration, including switching between user and admin installations.
/// </summary>
internal class InstallRootManager
{
    private readonly IDotnetEnvironmentManager _dotnetEnvironment;

    public InstallRootManager(IDotnetEnvironmentManager? dotnetEnvironment = null)
    {
        _dotnetEnvironment = dotnetEnvironment ?? new DotnetEnvironmentManager();
    }

    /// <summary>
    /// Gets the changes needed to configure user install root.
    /// </summary>
    public UserInstallRootChanges GetUserInstallRootChanges()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("User install root configuration is only supported on Windows.");
        }

        string userDotnetPath = _dotnetEnvironment.GetDefaultDotnetInstallPath();
        bool needToRemoveAdminPath = WindowsPathHelper.AdminPathContainsProgramFilesDotnet(out var foundDotnetPaths);

        // Read both expanded and unexpanded user PATH from registry
        string unexpandedUserPath = WindowsPathHelper.ReadUserPath(expand: false);
        string expandedUserPath = WindowsPathHelper.ReadUserPath(expand: true);

        // Use the helper method to add the path while preserving unexpanded variables
        string newUserPath = WindowsPathHelper.AddPathEntry(unexpandedUserPath, expandedUserPath, userDotnetPath, "dotnet");
        bool needToAddToUserPath = newUserPath != unexpandedUserPath;

        var existingDotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT", EnvironmentVariableTarget.User);
        bool needToSetDotnetRoot = !string.Equals(userDotnetPath, existingDotnetRoot, StringComparison.OrdinalIgnoreCase);

        return new UserInstallRootChanges(
            userDotnetPath,
            needToRemoveAdminPath,
            needToAddToUserPath,
            needToSetDotnetRoot,
            newUserPath,
            foundDotnetPaths);
    }

    /// <summary>
    /// Gets the changes needed to configure admin install root.
    /// </summary>
    public AdminInstallRootChanges GetAdminInstallRootChanges()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Admin install root configuration is only supported on Windows.");
        }

        var programFilesDotnetPaths = WindowsPathHelper.GetProgramFilesDotnetPaths();

        // When no dotnet is installed in Program Files, there is no admin path to reference or modify.
        string primaryProgramFilesDotnetPath = programFilesDotnetPaths.FirstOrDefault() ?? string.Empty;

        // Use PathContainsDotnet, as that handles differences in trailing directory separators
        // GetProgramFilesDotnetPaths trims the trailing separator while the .NET installer writes
        // the system PATH entry *with* one (e.g. "C:\Program Files\dotnet\")
        bool needToModifyAdminPath = programFilesDotnetPaths.Count > 0
            && !WindowsPathHelper.PathContainsDotnet(
                WindowsPathHelper.SplitPath(WindowsPathHelper.ReadAdminPath(expand: true)),
                programFilesDotnetPaths);

        // Get the user dotnet installation path
        string userDotnetPath = _dotnetEnvironment.GetDefaultDotnetInstallPath();

        // Read both expanded and unexpanded user PATH from registry to preserve environment variables
        string unexpandedUserPath = WindowsPathHelper.ReadUserPath(expand: false);
        string expandedUserPath = WindowsPathHelper.ReadUserPath(expand: true);

        // Use the helper method to remove the path while preserving unexpanded variables
        string newUserPath = WindowsPathHelper.RemovePathEntries(unexpandedUserPath, expandedUserPath, [userDotnetPath]);
        bool needToModifyUserPath = newUserPath != unexpandedUserPath;

        bool needToUnsetDotnetRoot = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_ROOT", EnvironmentVariableTarget.User));

        return new AdminInstallRootChanges(
            primaryProgramFilesDotnetPath,
            needToModifyAdminPath,
            needToModifyUserPath,
            needToUnsetDotnetRoot,
            userDotnetPath,
            newUserPath);
    }

    /// <summary>
    /// Applies the user install root configuration. Throws
    /// <see cref="DotnetInstallException"/> if the user declines an elevation prompt.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void ApplyUserInstallRoot(UserInstallRootChanges changes, Action<string> writeOutput)
    {
        if (changes.NeedsRemoveAdminPath)
        {
            RemoveAdminPathIfNeeded(changes.FoundAdminDotnetPaths!, writeOutput);
        }

        if (changes.NeedsAddToUserPath)
        {
            writeOutput($"Adding {Highlight(changes.UserDotnetPath)} to user PATH.");
            WindowsPathHelper.WriteUserPath(changes.NewUserPath!);
        }

        if (changes.NeedsSetDotnetRoot)
        {
            writeOutput($"Setting {Highlight("DOTNET_ROOT")} to {Highlight(changes.UserDotnetPath)}");
            Environment.SetEnvironmentVariable("DOTNET_ROOT", changes.UserDotnetPath, EnvironmentVariableTarget.User);
        }
    }

    /// <summary>
    /// Applies the admin install root configuration. Throws
    /// <see cref="DotnetInstallException"/> if the user declines an elevation prompt.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void ApplyAdminInstallRoot(AdminInstallRootChanges changes, Action<string> writeOutput)
    {
        if (changes.NeedsModifyAdminPath)
        {
            AddAdminPathIfNeeded(changes.ProgramFilesDotnetPath, writeOutput);
        }

        if (changes.NeedsModifyUserPath)
        {
            writeOutput($"Removing {Highlight(changes.UserDotnetPath)} from user PATH.");
            WindowsPathHelper.WriteUserPath(changes.NewUserPath!);
        }

        if (changes.NeedsUnsetDotnetRoot)
        {
            writeOutput($"Removing {Highlight("DOTNET_ROOT")} environment variable.");
            Environment.SetEnvironmentVariable("DOTNET_ROOT", null, EnvironmentVariableTarget.User);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RemoveAdminPathIfNeeded(List<string> foundDotnetPaths, Action<string> writeOutput)
    {
        if (Environment.IsPrivilegedProcess)
        {
            if (foundDotnetPaths.Count == 1)
            {
                writeOutput($"Removing {Highlight(foundDotnetPaths[0])} from system PATH.");
            }
            else
            {
                writeOutput("Removing the following dotnet paths from system PATH:");
                foreach (var path in foundDotnetPaths)
                {
                    writeOutput($"  - {Highlight(path)}");
                }
            }

            // We're already elevated, modify the admin PATH directly
            using var pathHelper = new WindowsPathHelper();
            pathHelper.RemoveDotnetFromAdminPath();
        }
        else
        {
            // Not elevated, shell out to elevated process
            if (foundDotnetPaths.Count == 1)
            {
                writeOutput($"Launching elevated process to remove {Highlight(foundDotnetPaths[0])} from system PATH.");
            }
            else
            {
                writeOutput("Launching elevated process to remove the following dotnet paths from system PATH:");
                foreach (var path in foundDotnetPaths)
                {
                    writeOutput($"  - {Highlight(path)}");
                }
            }

            WindowsPathHelper.StartElevatedProcess("removedotnet");
        }
    }

    [SupportedOSPlatform("windows")]
    private static void AddAdminPathIfNeeded(string programFilesDotnetPath, Action<string> writeOutput)
    {
        if (Environment.IsPrivilegedProcess)
        {
            // We're already elevated, modify the admin PATH directly
            writeOutput($"Adding {Highlight(programFilesDotnetPath)} to system PATH.");
            using var pathHelper = new WindowsPathHelper();
            pathHelper.AddDotnetToAdminPath();
        }
        else
        {
            // Not elevated, shell out to elevated process
            writeOutput($"Launching elevated process to add {Highlight(programFilesDotnetPath)} to system PATH.");
            WindowsPathHelper.StartElevatedProcess("adddotnet");
        }
    }

    // Colors a path or environment-variable name with the theme accent, escaping any markup so
    // it renders correctly when the output is written with AnsiConsole.MarkupLine.
    private static string Highlight(string value) => DotnetupTheme.Accent(value.EscapeMarkup());
}

/// <summary>
/// Represents the changes needed to configure user install root.
/// </summary>
internal record UserInstallRootChanges(
    string UserDotnetPath,
    bool NeedsRemoveAdminPath,
    bool NeedsAddToUserPath,
    bool NeedsSetDotnetRoot,
    string? NewUserPath,
    List<string>? FoundAdminDotnetPaths)
{
    /// <summary>
    /// Checks if any changes are needed to configure user install root.
    /// </summary>
    public bool NeedsChange() => NeedsRemoveAdminPath || NeedsAddToUserPath || NeedsSetDotnetRoot;
}

/// <summary>
/// Represents the changes needed to configure admin install root.
/// </summary>
internal record AdminInstallRootChanges(
    string ProgramFilesDotnetPath,
    bool NeedsModifyAdminPath,
    bool NeedsModifyUserPath,
    bool NeedsUnsetDotnetRoot,
    string UserDotnetPath,
    string? NewUserPath)
{
    /// <summary>
    /// Checks if any changes are needed to configure admin install root.
    /// </summary>
    public bool NeedsChange() => NeedsModifyAdminPath || NeedsModifyUserPath || NeedsUnsetDotnetRoot;
}
