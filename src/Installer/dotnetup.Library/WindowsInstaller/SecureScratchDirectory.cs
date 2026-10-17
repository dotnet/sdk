// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Microsoft.DotNet.Tools.Bootstrapper.WindowsInstaller;

/// <summary>
/// Creates scratch directories for files that an elevated process will run. The directory is created with a
/// protected ACL, so its security applies from the moment it exists: SYSTEM and Administrators have full control,
/// and every other user, including the same user running without elevation, can only read.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class SecureScratchDirectory
{
    /// <summary>
    /// <c>%windir%\Temp</c>. Standard users can create entries there but cannot list it or rename or delete
    /// entries they do not own, so a directory created there by an elevated process cannot be swapped out.
    /// The per-user temp directory does not have that property.
    /// </summary>
    internal static string DefaultParentDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");

    /// <summary>Creates a new, uniquely named secure directory and returns its full path.</summary>
    /// <exception cref="UnauthorizedAccessException">The process is not elevated.</exception>
    /// <exception cref="IOException">The directory was not created with the expected owner.</exception>
    public static string Create(string? parentDirectory = null)
    {
        string path = Path.Combine(parentDirectory ?? DefaultParentDirectory, $"dotnetup-{Guid.NewGuid():N}");
        DirectoryInfo directory = CreateSecurity().CreateDirectory(path);
        EnsureCreatedSecurely(directory);
        return directory.FullName;
    }

    /// <summary>The security applied to new secure directories. Exposed for testing.</summary>
    internal static DirectorySecurity CreateSecurity()
    {
        const InheritanceFlags Inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

        var security = new DirectorySecurity();
        security.SetOwner(administrators);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, Inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            administrators, FileSystemRights.FullControl, Inherit, PropagationFlags.None, AccessControlType.Allow));

        // Read access keeps installer helper processes that drop elevation working; it never allows writes.
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute, Inherit, PropagationFlags.None, AccessControlType.Allow));
        return security;
    }

    /// <summary>
    /// Rejects a directory that already existed (creation returns an existing directory unchanged) or that is a
    /// link to somewhere else. Only an administrator can create a directory owned by Administrators.
    /// </summary>
    private static void EnsureCreatedSecurely(DirectoryInfo directory)
    {
        directory.Refresh();
        if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException($"The secure download directory '{directory.FullName}' is a link.");
        }

        var owner = directory.GetAccessControl(AccessControlSections.Owner).GetOwner(typeof(SecurityIdentifier));
        if (owner is not SecurityIdentifier sid || !sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid))
        {
            throw new IOException($"The secure download directory '{directory.FullName}' is not owned by Administrators.");
        }
    }
}
