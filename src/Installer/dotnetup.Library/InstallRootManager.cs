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

        // everywhere mode inserts the user (dotnetup) dotnet directory into the system PATH
        // immediately before the machine-wide Program Files dotnet entry (or appends it when there
        // is none), rather than removing the Program Files entry. A later machine-wide install
        // appends its own PATH entry, so it lands after the user entry and the user install keeps
        // precedence.
        //
        // Windows composes the effective PATH as system-then-user, so this system-PATH entry makes
        // the user install win for every process. No matching user-scope PATH entry is needed; only
        // DOTNET_ROOT is set at user scope (for apphost/runtime resolution).
        string unexpandedSystemPath = WindowsPathHelper.ReadSystemPath(expand: false);
        string expandedSystemPath = WindowsPathHelper.ReadSystemPath(expand: true);
        string newSystemPath = WindowsPathHelper.InsertPathEntryBeforeProgramFilesDotnet(
            unexpandedSystemPath, expandedSystemPath, userDotnetPath);
        bool needToInsertUserDotnetIntoSystemPath = !string.Equals(newSystemPath, unexpandedSystemPath, StringComparison.Ordinal);
        bool systemPathHasProgramFilesDotnet = WindowsPathHelper.SystemPathContainsProgramFilesDotnet();

        var existingDotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT", EnvironmentVariableTarget.User);
        bool needToSetDotnetRoot = !string.Equals(userDotnetPath, existingDotnetRoot, StringComparison.OrdinalIgnoreCase);

        return new UserInstallRootChanges(
            userDotnetPath,
            needToInsertUserDotnetIntoSystemPath,
            needToSetDotnetRoot,
            systemPathHasProgramFilesDotnet);
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

        // Get the user (dotnetup) dotnet installation path
        string userDotnetPath = _dotnetEnvironment.GetDefaultDotnetInstallPath();

        // Switching away from everywhere mode removes the user (dotnetup) dotnet directory that
        // everywhere mode inserted into the system PATH. The machine-wide Program Files dotnet entry
        // was never removed, so there is nothing to restore.
        string unexpandedSystemPath = WindowsPathHelper.ReadSystemPath(expand: false);
        string expandedSystemPath = WindowsPathHelper.ReadSystemPath(expand: true);
        string newSystemPath = WindowsPathHelper.RemovePathEntries(
            unexpandedSystemPath, expandedSystemPath, [userDotnetPath]);
        bool needToRemoveUserDotnetFromSystemPath = !string.Equals(newSystemPath, unexpandedSystemPath, StringComparison.Ordinal);

        bool needToUnsetDotnetRoot = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_ROOT", EnvironmentVariableTarget.User));

        return new AdminInstallRootChanges(
            needToRemoveUserDotnetFromSystemPath,
            needToUnsetDotnetRoot,
            userDotnetPath);
    }

    /// <summary>
    /// Applies the user install root configuration. Throws
    /// <see cref="DotnetInstallException"/> if the user declines an elevation prompt.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void ApplyUserInstallRoot(UserInstallRootChanges changes, Action<string> writeOutput)
    {
        if (changes.NeedsInsertUserDotnetIntoSystemPath)
        {
            InsertUserDotnetIntoSystemPathIfNeeded(changes.UserDotnetPath, changes.SystemPathHasProgramFilesDotnet, writeOutput);
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
        if (changes.NeedsRemoveUserDotnetFromSystemPath)
        {
            RemoveUserDotnetFromSystemPathIfNeeded(changes.UserDotnetPath, writeOutput);
        }

        if (changes.NeedsUnsetDotnetRoot)
        {
            writeOutput($"Removing {Highlight("DOTNET_ROOT")} environment variable.");
            Environment.SetEnvironmentVariable("DOTNET_ROOT", null, EnvironmentVariableTarget.User);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void InsertUserDotnetIntoSystemPathIfNeeded(string userDotnetPath, bool systemPathHasProgramFilesDotnet, Action<string> writeOutput)
    {
        // When a machine-wide install is present the entry is inserted just ahead of it; otherwise
        // it is appended to the end of the system PATH.
        string target = systemPathHasProgramFilesDotnet
            ? "system PATH, ahead of the machine-wide .NET install"
            : "system PATH";

        if (Environment.IsPrivilegedProcess)
        {
            // We're already elevated, modify the system PATH directly
            writeOutput($"Adding {Highlight(userDotnetPath)} to {target}.");
            using var pathHelper = new WindowsPathHelper();
            pathHelper.InsertDotnetIntoSystemPath(userDotnetPath);
        }
        else
        {
            // Not elevated, shell out to elevated process
            writeOutput($"Launching elevated process to add {Highlight(userDotnetPath)} to {target}.");
            WindowsPathHelper.StartElevatedProcess("insertdotnet", userDotnetPath);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RemoveUserDotnetFromSystemPathIfNeeded(string userDotnetPath, Action<string> writeOutput)
    {
        if (Environment.IsPrivilegedProcess)
        {
            // We're already elevated, modify the system PATH directly
            writeOutput($"Removing {Highlight(userDotnetPath)} from system PATH.");
            using var pathHelper = new WindowsPathHelper();
            pathHelper.RemoveDotnetFromSystemPath(userDotnetPath);
        }
        else
        {
            // Not elevated, shell out to elevated process
            writeOutput($"Launching elevated process to remove {Highlight(userDotnetPath)} from system PATH.");
            WindowsPathHelper.StartElevatedProcess("removedotnet", userDotnetPath);
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
    bool NeedsInsertUserDotnetIntoSystemPath,
    bool NeedsSetDotnetRoot,
    bool SystemPathHasProgramFilesDotnet)
{
    /// <summary>
    /// Checks if any changes are needed to configure user install root.
    /// </summary>
    public bool NeedsChange() => NeedsInsertUserDotnetIntoSystemPath || NeedsSetDotnetRoot;
}

/// <summary>
/// Represents the changes needed to configure admin install root.
/// </summary>
internal record AdminInstallRootChanges(
    bool NeedsRemoveUserDotnetFromSystemPath,
    bool NeedsUnsetDotnetRoot,
    string UserDotnetPath)
{
    /// <summary>
    /// Checks if any changes are needed to configure admin install root.
    /// </summary>
    public bool NeedsChange() => NeedsRemoveUserDotnetFromSystemPath || NeedsUnsetDotnetRoot;
}
