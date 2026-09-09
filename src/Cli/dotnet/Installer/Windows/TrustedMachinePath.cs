// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    ///  Establishes that the machine-wide dotnet directory can be trusted by elevated code. The default
    ///  <c>%ProgramData%</c> descriptor lets a standard user create a top-level directory, so an object's
    ///  location alone does not make it trustworthy.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Only the root is validated. Once the root grants modification rights to no principal outside
    ///   <see cref="s_TrustedSids"/>, an unprivileged user cannot create, replace, rename, or delete
    ///   anything below it, so descendants are trusted by induction. Validating each descendant again
    ///   would cost a descriptor read per component without excluding any additional attacker.
    ///  </para>
    ///  <para>
    ///   Trust is evaluated by capability rather than by exact descriptor equality: a trusted owner,
    ///   no reparse point, and no modification rights outside the trusted principals. Additional read-only
    ///   allow rules and restrictive deny rules are permitted so enterprise ACL customization does not
    ///   break workload operations.
    ///  </para>
    /// </remarks>
#if NET
    [SupportedOSPlatform("windows")]
#endif
    internal static class TrustedMachinePath
    {
        private const uint ReadControl = 0x00020000;
        private const uint OpenExisting = 3;
        private const uint FileFlagBackupSemantics = 0x02000000;
        private const uint FileFlagOpenReparsePoint = 0x00200000;
        private const uint OwnerSecurityInformation = 0x00000001;
        private const uint DaclSecurityInformation = 0x00000004;

        private static readonly SecurityIdentifier s_AdministratorsSid = new(WellKnownSidType.BuiltinAdministratorsSid, null);
        private static readonly SecurityIdentifier s_LocalSystemSid = new(WellKnownSidType.LocalSystemSid, null);
        private static readonly HashSet<SecurityIdentifier> s_TrustedSids = new()
        {
            s_AdministratorsSid,
            s_LocalSystemSid
        };

        /// <summary>
        ///  Rights that let a principal alter an object, its children, or its security descriptor.
        ///  <see cref="FileSystemRights.DeleteSubdirectoriesAndFiles"/> on a directory removes a child
        ///  whatever the child's own descriptor says, which is why directory rights are the check that
        ///  matters rather than the rights on an individual file.
        /// </summary>
        private const FileSystemRights ModificationRights =
            FileSystemRights.WriteData |
            FileSystemRights.AppendData |
            FileSystemRights.WriteExtendedAttributes |
            FileSystemRights.WriteAttributes |
            FileSystemRights.Delete |
            FileSystemRights.DeleteSubdirectoriesAndFiles |
            FileSystemRights.ChangePermissions |
            FileSystemRights.TakeOwnership;

        /// <summary>
        ///  The machine-wide dotnet directory that anchors trust for everything below it.
        /// </summary>
        public static string ProgramDataDotnetRoot { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "dotnet");

        /// <summary>
        ///  Determines whether a path resolves to <paramref name="root"/> or one of its descendants.
        /// </summary>
        public static bool IsUnderRoot(string path, string root)
        {
            string fullPath = Path.GetFullPath(path);
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///  Validates <see cref="ProgramDataDotnetRoot"/> when it exists. A missing root is not an error;
        ///  the caller creates it with the intended descriptor.
        /// </summary>
        /// <exception cref="InvalidDataException">The root cannot be trusted.</exception>
        public static void ValidateProgramDataRoot()
        {
            if (Directory.Exists(ProgramDataDotnetRoot))
            {
                ValidateDirectory(ProgramDataDotnetRoot);
            }
        }

        /// <summary>
        ///  Validates that a directory is not a reparse point and that its descriptor is trusted.
        /// </summary>
        public static void ValidateDirectory(string path)
        {
            using SafeFileHandle handle = CreateFile(
                path,
                ReadControl,
                FileShare.ReadWrite | FileShare.Delete,
                IntPtr.Zero,
                OpenExisting,
                FileFlagBackupSemantics | FileFlagOpenReparsePoint,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                ThrowNativeError(Marshal.GetLastWin32Error(), path);
            }

            if (!GetFileInformationByHandleEx(handle, FileInfoByHandleClass.FileAttributeTagInfo, out FileAttributeTagInfo fileInformation, (uint)Marshal.SizeOf<FileAttributeTagInfo>()))
            {
                ThrowNativeError(Marshal.GetLastWin32Error(), path);
            }

            if ((fileInformation.FileAttributes & FileAttributes.ReparsePoint) != 0)
            {
                ThrowUntrusted(path);
            }

            uint error = GetSecurityInfo(handle, SeObjectType.FileObject, OwnerSecurityInformation | DaclSecurityInformation, out _, out _, out _, out _, out IntPtr securityDescriptor);
            if (error != 0)
            {
                ThrowNativeError((int)error, path);
            }

            try
            {
                int length = checked((int)GetSecurityDescriptorLength(securityDescriptor));
                byte[] binarySecurityDescriptor = new byte[length];
                Marshal.Copy(securityDescriptor, binarySecurityDescriptor, 0, length);

                DirectorySecurity security = new DirectorySecurity();
                security.SetSecurityDescriptorBinaryForm(binarySecurityDescriptor, AccessControlSections.Access | AccessControlSections.Owner);
                Validate(security, path);
            }
            finally
            {
                LocalFree(securityDescriptor);
            }
        }

        /// <summary>
        ///  Validates that a descriptor has a trusted machine owner, grants full control to Administrators
        ///  and SYSTEM, and grants no modification rights to any other principal.
        /// </summary>
        public static void Validate(FileSystemSecurity security, string path)
        {
            if (security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner || !s_TrustedSids.Contains(owner))
            {
                ThrowUntrusted(path);
            }

            AuthorizationRuleCollection rules = security.GetAccessRules(
                includeExplicit: true,
                includeInherited: true,
                typeof(SecurityIdentifier));
            Dictionary<SecurityIdentifier, FileSystemRights> trustedRights = new();

            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.AccessControlType == AccessControlType.Deny)
                {
                    continue;
                }

                if (rule.IdentityReference is not SecurityIdentifier sid)
                {
                    ThrowUntrusted(path);
                    continue;
                }

                if (s_TrustedSids.Contains(sid))
                {
                    trustedRights.TryGetValue(sid, out FileSystemRights existingRights);
                    trustedRights[sid] = existingRights | rule.FileSystemRights;
                }
                else if ((rule.FileSystemRights & ModificationRights) != 0)
                {
                    // Inherit-only rules count here: they become effective rights on descendants.
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
            throw new InvalidDataException(
                $"'{path}' has unexpected permissions and cannot be trusted. Delete '{path}' and retry.");
        }

        private static void ThrowNativeError(int error, string path)
        {
            var exception = new Win32Exception(error);
            throw error switch
            {
                2 or 3 => new DirectoryNotFoundException($"Could not find '{path}'.", exception),
                5 => new UnauthorizedAccessException(exception.Message, exception),
                _ => new IOException(exception.Message, exception),
            };
        }

        private enum FileInfoByHandleClass { FileAttributeTagInfo = 9 }
        private enum SeObjectType { FileObject = 1 }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileAttributeTagInfo
        {
            public FileAttributes FileAttributes;
            public uint ReparseTag;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, FileShare shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(SafeFileHandle file, FileInfoByHandleClass fileInformationClass, out FileAttributeTagInfo fileInformation, uint bufferSize);

        [DllImport("advapi32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern uint GetSecurityInfo(SafeFileHandle handle, SeObjectType objectType, uint securityInformation, out IntPtr owner, out IntPtr group, out IntPtr dacl, out IntPtr sacl, out IntPtr securityDescriptor);

        [DllImport("advapi32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern uint GetSecurityDescriptorLength(IntPtr securityDescriptor);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern IntPtr LocalFree(IntPtr memory);
    }
}
