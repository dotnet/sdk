// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Defines some generic security related helper methods.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class SecurityUtils
    {
        /// <summary>
        /// Default inheritance to apply to directory ACLs.
        /// </summary>
        private static readonly InheritanceFlags s_DefaultInheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

        /// <summary>
        /// SID that matches built-in administrators.
        /// </summary>
        private static readonly SecurityIdentifier s_AdministratorsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

        /// <summary>
        /// SID that matches everyone.
        /// </summary>
        private static readonly SecurityIdentifier s_EveryoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

        /// <summary>
        /// Local SYSTEM SID.
        /// </summary>
        private static readonly SecurityIdentifier s_LocalSystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

        /// <summary>
        /// SID matching built-in user accounts.
        /// </summary>
        private static readonly SecurityIdentifier s_UsersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        /// <summary>
        /// ACL rule associated with the Administrators SID.
        /// </summary>
        private static readonly FileSystemAccessRule s_AdministratorRule = new FileSystemAccessRule(s_AdministratorsSid, FileSystemRights.FullControl,
            s_DefaultInheritance, PropagationFlags.None, AccessControlType.Allow);

        /// <summary>
        /// ACL rule associated with the Everyone SID.
        /// </summary>
        private static readonly FileSystemAccessRule s_EveryoneRule = new FileSystemAccessRule(s_EveryoneSid, FileSystemRights.ReadAndExecute,
            s_DefaultInheritance, PropagationFlags.None, AccessControlType.Allow);

        /// <summary>
        /// ACL rule associated with the Local SYSTEM SID.
        /// </summary>
        private static readonly FileSystemAccessRule s_LocalSystemRule = new FileSystemAccessRule(s_LocalSystemSid, FileSystemRights.FullControl,
            s_DefaultInheritance, PropagationFlags.None, AccessControlType.Allow);

        /// <summary>
        /// ACL rule associated with the built-in users SID.
        /// </summary>
        private static readonly FileSystemAccessRule s_UsersRule = new FileSystemAccessRule(s_UsersSid, FileSystemRights.ReadAndExecute,
            s_DefaultInheritance, PropagationFlags.None, AccessControlType.Allow);

        /// <summary>
        /// Creates the specified directory and secures it by configuring access rules (ACLs) that allow sub-directories
        /// and files to inherit access control entries. 
        /// </summary>
        /// <param name="path">The path of the directory to create.</param>
        public static void CreateSecureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                DirectorySecurity ds = new();
                SecurityUtils.SetDirectoryAccessRules(ds);
                ds.CreateDirectory(path);
            }
        }

        /// <summary>
        /// Moves a file from one location to another if the destination file does not already exist and
        /// configure its permissions.
        /// </summary>
        /// <param name="sourceFile">The source file to move.</param>
        /// <param name="destinationFile">The destination where the source file will be moved.</param>
        /// <param name="log">The underlying setup log to use.</param>
        public static void MoveAndSecureFile(string sourceFile, string destinationFile, ISetupLogger log = null)
        {
            if (!File.Exists(destinationFile))
            {
                FileAccessRetrier.RetryOnMoveAccessFailure(() =>
                {
                    // Moving the file preserves the owner SID and fails to inherit the WD ACE.
                    File.Copy(sourceFile, destinationFile, overwrite: true);
                    File.Delete(sourceFile);
                });
                log?.LogMessage($"Moved '{sourceFile}' to '{destinationFile}'");

                SecureFile(destinationFile);
            }
        }

        /// <summary>
        /// Secures a file by setting the owner and group to built-in administrators (BA). All other ACE values are inherited from
        /// the parent directory.
        /// </summary>
        /// <param name="path">The path of the file to secure.</param>
        public static void SecureFile(string path)
        {
            FileInfo fi = new(path);
            FileSecurity fs = new();

            // See https://github.com/dotnet/sdk/issues/28450. If the directory's descriptor
            // is correctly configured, we should end up with an inherited ACE for Everyone: (A;ID;0x1200a9;;;WD)
            fs.SetOwner(s_AdministratorsSid);
            fs.SetGroup(s_AdministratorsSid);
            fi.SetAccessControl(fs);
        }

        /// <summary>
        /// Apply a standard set of access rules to the directory security descriptor. The owner and group will
        /// be set to built-in Administrators. Full access is granted to built-in administators and SYSTEM with
        /// read, execute, synchronize permssions for built-in users and Everyone.
        /// </summary>
        /// <param name="ds">The security descriptor to update.</param>
        private static void SetDirectoryAccessRules(DirectorySecurity ds)
        {
            ds.SetOwner(s_AdministratorsSid);
            ds.SetGroup(s_AdministratorsSid);
            ds.SetAccessRule(s_AdministratorRule);
            ds.SetAccessRule(s_LocalSystemRule);
            ds.SetAccessRule(s_UsersRule);
            ds.SetAccessRule(s_EveryoneRule);
        }
    }
}
