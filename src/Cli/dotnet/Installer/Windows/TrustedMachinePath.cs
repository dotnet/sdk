// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Microsoft.DotNet.Installer.Windows
{
#if NET
    [SupportedOSPlatform("windows")]
#endif
    internal static class TrustedMachinePath
    {
        private static readonly SecurityIdentifier s_AdministratorsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        private static readonly SecurityIdentifier s_LocalSystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        private static readonly HashSet<SecurityIdentifier> s_TrustedSids = new HashSet<SecurityIdentifier>()
        {
            s_AdministratorsSid,
            s_LocalSystemSid,
        };

        private const FileSystemRights ModificationRights = FileSystemRights.WriteData
            | FileSystemRights.AppendData
            | FileSystemRights.WriteExtendedAttributes
            | FileSystemRights.WriteAttributes
            | FileSystemRights.Delete
            | FileSystemRights.DeleteSubdirectoriesAndFiles
            | FileSystemRights.ChangePermissions
            | FileSystemRights.TakeOwnership;

        public static string ProgramDataDotnetRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "dotnet");

        public static bool IsUnderRoot(string path, string root)
        {
            string fullPath = Path.GetFullPath(path);
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
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
            if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            {
                ThrowUntrusted(path);
            }

            DirectorySecurity security = new DirectoryInfo(path).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
            Validate(security, path);
        }

        public static void Validate(FileSystemSecurity security, string path)
        {
            if (security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier ownerSid || !s_TrustedSids.Contains(ownerSid))
            {
                ThrowUntrusted(path);
            }

            AuthorizationRuleCollection accessRules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier));
            Dictionary<SecurityIdentifier, FileSystemRights> trustedRights = new Dictionary<SecurityIdentifier, FileSystemRights>();

            foreach (FileSystemAccessRule accessRule in accessRules)
            {
                if (accessRule.AccessControlType == AccessControlType.Deny)
                {
                    continue;
                }

                if (accessRule.IdentityReference is not SecurityIdentifier sid)
                {
                    ThrowUntrusted(path);
                    continue;
                }

                if (s_TrustedSids.Contains(sid))
                {
                    trustedRights.TryGetValue(sid, out FileSystemRights existingRights);
                    trustedRights[sid] = existingRights | accessRule.FileSystemRights;
                    continue;
                }

                if ((accessRule.FileSystemRights & ModificationRights) != 0)
                {
                    ThrowUntrusted(path);
                }
            }

            foreach (SecurityIdentifier trustedSid in s_TrustedSids)
            {
                if (!trustedRights.TryGetValue(trustedSid, out FileSystemRights rights) ||
                    (rights & FileSystemRights.FullControl) != FileSystemRights.FullControl)
                {
                    ThrowUntrusted(path);
                }
            }
        }

        private static void ThrowUntrusted(string path)
        {
            throw new InvalidDataException($"'{path}' has unexpected permissions and cannot be trusted. Delete '{path}' and retry.");
        }
    }
}
