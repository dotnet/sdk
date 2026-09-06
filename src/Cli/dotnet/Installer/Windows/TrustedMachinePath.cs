// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Establishes that the machine-wide dotnet directory can be trusted by elevated code.
    /// </summary>
#if NET
    [SupportedOSPlatform("windows")]
#endif
    internal static class TrustedMachinePath
    {
        private static readonly SecurityIdentifier s_administratorsSid =
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        private static readonly SecurityIdentifier s_localSystemSid =
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        private static readonly HashSet<SecurityIdentifier> s_trustedSids = new HashSet<SecurityIdentifier>
        {
            s_administratorsSid,
            s_localSystemSid,
        };

        private const FileSystemRights ModificationRights =
            FileSystemRights.WriteData |
            FileSystemRights.AppendData |
            FileSystemRights.WriteExtendedAttributes |
            FileSystemRights.WriteAttributes |
            FileSystemRights.Delete |
            FileSystemRights.DeleteSubdirectoriesAndFiles |
            FileSystemRights.ChangePermissions |
            FileSystemRights.TakeOwnership;

        public static string ProgramDataDotnetRoot { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "dotnet");

        public static bool IsUnderRoot(string path, string root)
        {
            string fullPath = Path.GetFullPath(path);
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        public static void ValidateProgramDataRoot()
        {
            if (Directory.Exists(ProgramDataDotnetRoot))
            {
                ValidateDirectory(ProgramDataDotnetRoot);
            }
        }

        public static void ValidateDirectory(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                ThrowUntrusted(path);
            }

            Validate(new DirectoryInfo(path).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner), path);
        }

        public static void Validate(FileSystemSecurity security, string path)
        {
            SecurityIdentifier owner = security.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
            if (owner == null || !s_trustedSids.Contains(owner))
            {
                ThrowUntrusted(path);
            }

            AuthorizationRuleCollection rules = security.GetAccessRules(
                includeExplicit: true,
                includeInherited: true,
                typeof(SecurityIdentifier));
            Dictionary<SecurityIdentifier, FileSystemRights> trustedRights = new Dictionary<SecurityIdentifier, FileSystemRights>();

            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.AccessControlType == AccessControlType.Deny)
                {
                    continue;
                }

                SecurityIdentifier sid = rule.IdentityReference as SecurityIdentifier;
                if (sid == null)
                {
                    ThrowUntrusted(path);
                    continue;
                }

                if (s_trustedSids.Contains(sid))
                {
                    trustedRights.TryGetValue(sid, out FileSystemRights existingRights);
                    trustedRights[sid] = existingRights | rule.FileSystemRights;
                }
                else if ((rule.FileSystemRights & ModificationRights) != 0)
                {
                    ThrowUntrusted(path);
                }
            }

            foreach (SecurityIdentifier trustedSid in s_trustedSids)
            {
                if (!trustedRights.TryGetValue(trustedSid, out FileSystemRights rights) ||
                    (rights & FileSystemRights.FullControl) != FileSystemRights.FullControl)
                {
                    ThrowUntrusted(path);
                }
            }
        }

        private static void ThrowUntrusted(string path) =>
            throw new InvalidDataException(
                $"'{path}' has unexpected permissions and cannot be trusted. Delete '{path}' and retry.");
    }
}
