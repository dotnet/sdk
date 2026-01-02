// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Manages the dotnet installation root configuration, including switching between user and admin installations.
/// </summary>
internal class InstallRootManager
{
    private readonly IDotnetInstallManager _dotnetInstaller;

    public InstallRootManager(IDotnetInstallManager? dotnetInstaller = null)
    {
        _dotnetInstaller = dotnetInstaller ?? new DotnetInstallManager();
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

        string userDotnetPath = _dotnetInstaller.GetDefaultDotnetInstallPath();
        bool needToRemoveAdminPath = WindowsPathHelper.AdminPathContainsProgramFilesDotnet(out var foundDotnetPaths);

        // Read both expanded and unexpanded user PATH from registry
        string unexpandedUserPath = WindowsPathHelper.ReadUserPath(expand: false);
        string expandedUserPath = WindowsPathHelper.ReadUserPath(expand: true);

        // Use the helper method to add the path while preserving unexpanded variables
        string newUserPath = WindowsPathHelper.AddPathEntry(unexpandedUserPath, expandedUserPath, userDotnetPath);
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
        bool needToModifyAdminPath = !WindowsPathHelper.SplitPath(WindowsPathHelper.ReadAdminPath(expand: true))
            .Contains(programFilesDotnetPaths.First(), StringComparer.OrdinalIgnoreCase);

        // Get the user dotnet installation path
        string userDotnetPath = _dotnetInstaller.GetDefaultDotnetInstallPath();
        
        // Read both expanded and unexpanded user PATH from registry to preserve environment variables
        string unexpandedUserPath = WindowsPathHelper.ReadUserPath(expand: false);
        string expandedUserPath = WindowsPathHelper.ReadUserPath(expand: true);
        
        // Use the helper method to remove the path while preserving unexpanded variables
        string newUserPath = WindowsPathHelper.RemovePathEntries(unexpandedUserPath, expandedUserPath, [userDotnetPath]);
        bool needToModifyUserPath = newUserPath != unexpandedUserPath;

        bool needToUnsetDotnetRoot = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_ROOT", EnvironmentVariableTarget.User));

        return new AdminInstallRootChanges(
            programFilesDotnetPaths.First(),
            needToModifyAdminPath,
            needToModifyUserPath,
            needToUnsetDotnetRoot,
            userDotnetPath,
            newUserPath);
    }

    /// <summary>
    /// Applies the user install root configuration.
    /// Returns true if successful, false if elevation was cancelled.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public bool ApplyUserInstallRoot(UserInstallRootChanges changes, Action<string> writeOutput, Action<string> writeError)
    {
        if (changes.NeedsRemoveAdminPath)
        {
            if (!RemoveAdminPathIfNeeded(changes.FoundAdminDotnetPaths!, writeOutput, writeError))
            {
                return false; // Elevation was cancelled
            }
        }

        if (changes.NeedsAddToUserPath)
        {
            writeOutput($"Adding {changes.UserDotnetPath} to user PATH.");
            WindowsPathHelper.WriteUserPath(changes.NewUserPath!);
        }

        if (changes.NeedsSetDotnetRoot)
        {
            writeOutput($"Setting DOTNET_ROOT to {changes.UserDotnetPath}");
            Environment.SetEnvironmentVariable("DOTNET_ROOT", changes.UserDotnetPath, EnvironmentVariableTarget.User);
        }

        return true;
    }

    /// <summary>
    /// Applies the admin install root configuration.
    /// Returns true if successful, false if elevation was cancelled.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public bool ApplyAdminInstallRoot(AdminInstallRootChanges changes, Action<string> writeOutput, Action<string> writeError)
    {
        if (changes.NeedsModifyAdminPath)
        {
            if (!AddAdminPathIfNeeded(changes.ProgramFilesDotnetPath, writeOutput, writeError))
            {
                return false; // Elevation was cancelled
            }
        }

        if (changes.NeedsModifyUserPath)
        {
            writeOutput($"Removing {changes.UserDotnetPath} from user PATH.");
            WindowsPathHelper.WriteUserPath(changes.NewUserPath!);
        }

        if (changes.NeedsUnsetDotnetRoot)
        {
            writeOutput("Unsetting DOTNET_ROOT environment variable.");
            Environment.SetEnvironmentVariable("DOTNET_ROOT", null, EnvironmentVariableTarget.User);
        }

        return true;
    }

    [SupportedOSPlatform("windows")]
    private bool RemoveAdminPathIfNeeded(List<string> foundDotnetPaths, Action<string> writeOutput, Action<string> writeError)
    {
        if (Environment.IsPrivilegedProcess)
        {
            if (foundDotnetPaths.Count == 1)
            {
                writeOutput($"Removing {foundDotnetPaths[0]} from system PATH.");
            }
            else
            {
                writeOutput("Removing the following dotnet paths from system PATH:");
                foreach (var path in foundDotnetPaths)
                {
                    writeOutput($"  - {path}");
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
                writeOutput($"Launching elevated process to remove {foundDotnetPaths[0]} from system PATH.");
            }
            else
            {
                writeOutput("Launching elevated process to remove the following dotnet paths from system PATH:");
                foreach (var path in foundDotnetPaths)
                {
                    writeOutput($"  - {path}");
                }
            }

            bool succeeded = WindowsPathHelper.StartElevatedProcess("removedotnet");
            if (!succeeded)
            {
                writeError("Warning: Elevation was cancelled. System PATH was not modified.");
                return false;
            }
        }

        return true;
    }

    [SupportedOSPlatform("windows")]
    private bool AddAdminPathIfNeeded(string programFilesDotnetPath, Action<string> writeOutput, Action<string> writeError)
    {
        if (Environment.IsPrivilegedProcess)
        {
            // We're already elevated, modify the admin PATH directly
            writeOutput($"Adding {programFilesDotnetPath} to system PATH.");
            using var pathHelper = new WindowsPathHelper();
            pathHelper.AddDotnetToAdminPath();
        }
        else
        {
            // Not elevated, shell out to elevated process
            writeOutput($"Launching elevated process to add {programFilesDotnetPath} to system PATH.");
            bool succeeded = WindowsPathHelper.StartElevatedProcess("adddotnet");
            if (!succeeded)
            {
                writeError("Warning: Elevation was cancelled. System PATH was not modified.");
                return false;
            }
        }

        return true;
    }
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
