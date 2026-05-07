// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.DotNet.Installer.Windows
{
    [SupportedOSPlatform("windows")]
    /// <summary>
    /// Utility methods, specific to Windows.
    /// </summary>
    public static class WindowsUtils
    {
        /// <summary>
        /// Generate a pseudo-random pipe name using the specified process ID, hashed MAC address and process path.
        /// </summary>
        /// <param name="processId">The process ID to use for generating the pipe name.</param>
        /// <param name="values">Additional values to incorporate into the generated name.</param>
        /// <returns>A string containing the pipe name.</returns>
        public static string CreatePipeName(int processId, params string[] values)
        {
            // Reinvoking the host can cause differences between the original path, e.g.,
            // "C:\Program Files" and "c:\Program Files". This will generate different UUID values and cause
            // deadlock when the client and server are trying to connect, so always use the lower invariant of the process.
            return Uuid.Create($"{processId};{Environment.ProcessPath.ToLowerInvariant()};{Sha256Hasher.Hash(MacAddressGetter.GetMacAddress())};{string.Join(";", values)}")
                .ToString("B");
        }

        /// <summary>
        /// Determines whether the current user has the Administrator role.
        /// </summary>
        /// <returns><see langword="true"/> if the user has the Administrator role.</returns>
        public static bool IsAdministrator()
        {
            WindowsPrincipal principal = new(WindowsIdentity.GetCurrent());

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Determine if an install is running by trying to open the global _MSIExecute mutex. The mutex is
        /// only set while processing the InstallExecuteSequence, AdminExecuteSequence or AdvtExecuteSequence tables.
        /// </summary>
        /// <returns><see langword="true" /> if another install is already running; <see langword="false"/> otherwise.</returns>
        /// See the <see href="https://docs.microsoft.com/en-us/windows/win32/msi/-msiexecute-mutex">_MSIMutex</see> documentation.
        public static bool InstallRunning()
        {
            return !Mutex.TryOpenExisting(@"Global\_MSIExecute", out _);
        }

        /// <summary>
        /// Queries the Windows Update Agent, Component Based Servicing (CBS), and pending file rename registry keys to determine if there is a pending reboot.
        /// </summary>
        /// <returns><see langword="true"/> if there is a pending reboot; <see langword="false"> otherwise.</see></returns>
        public static bool RebootRequired()
        {
            using RegistryKey localMachineKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey auKey = localMachineKey?.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            using RegistryKey cbsKey = localMachineKey?.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            using RegistryKey sessionKey = localMachineKey?.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");

            string[] pendingFileRenameOperations = (string[])sessionKey?.GetValue("PendingFileRenameOperations") ?? new string[0];
            // Destination files for pending renames start with !\??\, whereas the source does not have the leading "!".
            bool hasPendingFileRenames = pendingFileRenameOperations.Any(s => !string.IsNullOrWhiteSpace(s) && s.StartsWith(@"!\??\"));

            return (auKey != null || cbsKey != null || hasPendingFileRenames);
        }

        /// <summary>
        /// Returns the <see cref="SecurityIdentifier"/> of the user associated with the specified process.
        /// </summary>
        /// <param name="process">The process whose user SID to retrieve.</param>
        /// <returns>The <see cref="SecurityIdentifier"/> of the process owner.</returns>
        /// <exception cref="SecurityException">Thrown when the process token cannot be opened.</exception>
        public static SecurityIdentifier GetProcessUserSid(Process process)
        {
            if (!NativeMethods.OpenProcessToken(process.Handle, (uint)TokenAccessLevels.Query, out SafeAccessTokenHandle tokenHandle))
            {
                throw new SecurityException($"Failed to open process token for PID {process.Id}: {Marshal.GetLastPInvokeErrorMessage()}");
            }

            using (tokenHandle)
            using (WindowsIdentity identity = new WindowsIdentity(tokenHandle.DangerousGetHandle()))
            {
                return identity.User
                    ?? throw new SecurityException($"Unable to determine user SID for PID {process.Id}.");
            }
        }

        /// <summary>
        /// Returns the <see cref="SecurityIdentifier"/> that should be granted client access to the IPC pipe.
        /// Resolves the parent process's user SID to restrict pipe access to only the invoking user.
        /// </summary>
        /// <returns>The SID of the client allowed to connect to the pipe.</returns>
        /// <exception cref="SecurityException">Thrown when the parent process user SID cannot be determined.</exception>
        public static SecurityIdentifier GetPipeClientIdentifier()
        {
            return GetProcessUserSid(InstallerBase.ParentProcess);
        }

        /// <summary>
        /// Creates a <see cref="PipeSecurity"/> instance that grants the owner full control and
        /// the specified client identity read/write access.
        /// </summary>
        /// <param name="ownerSid">The SID of the pipe owner (typically the current elevated user).</param>
        /// <param name="clientSid">The SID of the client allowed to connect to the pipe.</param>
        /// <returns>A configured <see cref="PipeSecurity"/> instance.</returns>
        public static PipeSecurity CreatePipeSecurity(SecurityIdentifier ownerSid, SecurityIdentifier clientSid)
        {
            PipeSecurity pipeSecurity = new();

            // The current user has full control and should be running as Administrator.
            pipeSecurity.SetOwner(ownerSid);
            pipeSecurity.AddAccessRule(new PipeAccessRule(ownerSid, PipeAccessRights.FullControl, AccessControlType.Allow));

            // Restrict read/write access to the allowed client (typically in workloads the unelevated process parent talking to the elevated 'server')
            pipeSecurity.AddAccessRule(new PipeAccessRule(clientSid,
                PipeAccessRights.Read | PipeAccessRights.Write | PipeAccessRights.Synchronize, AccessControlType.Allow));

            return pipeSecurity;
        }

        /// <summary>
        /// Validates and returns the log file path to use for MSI operations.
        /// Ensures the path is under the server's temp directory, the trusted client temp directory
        /// (if supplied at server launch), or the parent user's profile temp directory.
        /// If the path is not in an allowed location, it is redirected to the server's temp directory.
        /// </summary>
        /// <param name="logFile">The requested log file path.</param>
        /// <param name="serverTempPath">The server's temp directory. If null, defaults to <see cref="Path.GetTempPath"/>.</param>
        /// <returns>The validated log file path.</returns>
        public static string ValidateLogFilePath(string logFile, string serverTempPath = null)
        {
            string fullLogPath = Path.GetFullPath(logFile);
            string serverTemp = Path.GetFullPath(serverTempPath ?? Path.GetTempPath());

            if (IsPathUnder(fullLogPath, serverTemp))
            {
                return fullLogPath;
            }

            string clientTemp = InstallerBase.TrustedClientTempDirectory;
            if (!string.IsNullOrEmpty(clientTemp) && IsPathUnder(fullLogPath, clientTemp))
            {
                return fullLogPath;
            }

            if (InstallerBase.ParentProcess != null)
            {
                try
                {
                    SecurityIdentifier parentUserSid = GetProcessUserSid(InstallerBase.ParentProcess);
                    string profilePath = GetUserProfilePath(parentUserSid);

                    if (profilePath != null)
                    {
                        string profileTemp = Path.GetFullPath(Path.Combine(profilePath, "AppData", "Local", "Temp"));
                        if (IsPathUnder(fullLogPath, profileTemp))
                        {
                            return fullLogPath;
                        }
                    }
                }
                catch
                {
                }
            }

            return Path.Combine(serverTemp, Path.GetFileName(fullLogPath));
        }

        /// <summary>
        /// Validates that an IPC-supplied workload manifest path lives under an allowed root.
        /// Allowed roots are the server's own temp directory and the trusted client temp
        /// directory (if supplied at server launch via <c>--client-temp</c>).
        /// </summary>
        public static bool ValidateManifestPath(string manifestPath, string serverTempPath = null)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                return false;
            }

            string fullManifestPath;
            try
            {
                fullManifestPath = Path.GetFullPath(manifestPath);
            }
            catch
            {
                return false;
            }

            string serverTemp = Path.GetFullPath(serverTempPath ?? Path.GetTempPath());
            if (IsPathUnder(fullManifestPath, serverTemp))
            {
                return true;
            }

            string clientTemp = InstallerBase.TrustedClientTempDirectory;
            if (!string.IsNullOrEmpty(clientTemp) && IsPathUnder(fullManifestPath, clientTemp))
            {
                return true;
            }

            return false;
        }

        private static bool IsPathUnder(string fullPath, string root)
        {
            string normalizedPath = fullPath.TrimEnd(Path.DirectorySeparatorChar);
            string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar);
            string rootWithSep = normalizedRoot + Path.DirectorySeparatorChar;

            return normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the profile path for the user identified by the specified <see cref="SecurityIdentifier"/>.
        /// Reads the ProfileImagePath value from the registry ProfileList key.
        /// </summary>
        /// <param name="sid">The SID of the user whose profile path to retrieve.</param>
        /// <returns>The profile path, or <see langword="null"/> if the profile is not found.</returns>
        public static string GetUserProfilePath(SecurityIdentifier sid)
        {
            // HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\{SID}\ProfileImagePath
            // RegistryKey.GetValue expands REG_EXPAND_SZ values by default, but call ExpandEnvironmentVariables
            // explicitly to also handle the rare case where the value was stored as REG_SZ with literal %vars%.
            using RegistryKey profileListKey = Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\{sid.Value}");

            string profileImagePath = profileListKey?.GetValue("ProfileImagePath") as string;
            return profileImagePath != null ? Environment.ExpandEnvironmentVariables(profileImagePath) : null;
        }

        /// <summary>
        /// Validates that the specified package path is under the expected cache root directory.
        /// Canonicalizes paths to prevent directory traversal and sibling-prefix attacks.
        /// </summary>
        /// <param name="packagePath">The package path to validate.</param>
        /// <param name="cacheRoot">The expected cache root directory.</param>
        /// <returns><see langword="true"/> if the path is under the cache root; otherwise <see langword="false"/>.</returns>
        public static bool ValidatePackagePath(string packagePath, string cacheRoot)
        {
            return ValidatePathUnderRoot(packagePath, cacheRoot);
        }

        /// <summary>
        /// Validates that a path component (such as a package ID or version) does not contain
        /// directory separator characters or parent-directory traversal sequences.
        /// </summary>
        /// <param name="component">The path component to validate.</param>
        /// <returns><see langword="true"/> if the component is safe to use in <see cref="Path.Combine"/>; otherwise <see langword="false"/>.</returns>
        public static bool ValidatePathComponent(string component)
        {
            if (string.IsNullOrWhiteSpace(component))
            {
                return false;
            }

            return !component.Contains(Path.DirectorySeparatorChar)
                && !component.Contains(Path.AltDirectorySeparatorChar)
                && !component.Contains("..");
        }

        /// <summary>
        /// Validates that the specified path, after canonicalization, is under the expected root directory.
        /// Prevents directory traversal and sibling-prefix attacks.
        /// </summary>
        /// <param name="path">The path to validate.</param>
        /// <param name="expectedRoot">The expected root directory.</param>
        /// <returns><see langword="true"/> if the canonicalized path is under the root; otherwise <see langword="false"/>.</returns>
        public static bool ValidatePathUnderRoot(string path, string expectedRoot)
        {
            string fullPath = Path.GetFullPath(path);
            string fullRoot = Path.GetFullPath(expectedRoot);

            return IsPathUnder(fullPath, fullRoot);
        }
    }
}
