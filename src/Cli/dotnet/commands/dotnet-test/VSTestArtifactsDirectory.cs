// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test
{
    /// <summary>
    /// Owns the private directory that vstest.console writes the artifacts of a test session into.
    /// </summary>
    internal static class VSTestArtifactsDirectory
    {
        internal const string EnvironmentVariableName = "VSTEST_ARTIFACTS_DIRECTORY";

        /// <summary>
        /// Overrides the private base directory the per-invocation GUID-named folder is created under - instead
        /// of the platform-specific default <see cref="GetBaseDirectory"/> otherwise picks - for example to
        /// redirect the artifacts to a tmpfs or to a volume with more free space. The base directory does not
        /// have to exist, and whatever is created under it is still hardened to owner-only access like the
        /// default is.
        /// </summary>
        internal const string BaseDirectoryEnvironmentVariableName = "VSTEST_ARTIFACTS_BASE_DIRECTORY";

        private const UnixFileMode OwnerOnlyMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

        private const int MaxCreateAttempts = 5;

        private static readonly object s_gate = new();

        private static string s_path;

        private static bool s_creationFailed;

        private static SecurityIdentifier s_currentUserSid;

        internal static string Current => Volatile.Read(ref s_path);

        internal static string GetOrCreate()
        {
            if (Volatile.Read(ref s_path) is string existing)
            {
                return existing;
            }

            if (FeatureFlag.Instance.IsSet(FeatureFlag.DISABLE_ARTIFACTS_POSTPROCESSING))
            {
                return null;
            }

            lock (s_gate)
            {
                if (s_path is null && !s_creationFailed)
                {
                    try
                    {
                        s_path = Create(NewCandidatePath);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        s_creationFailed = true;
                        VSTestTrace.SafeWriteTrace(
                            () => $"Could not create an artifacts directory, vstest.console will choose one: \n{ex}");
                    }
                }

                return s_path;
            }
        }

        internal static void Delete()
        {
            lock (s_gate)
            {
                s_creationFailed = false;
            }

            string path = Interlocked.Exchange(ref s_path, null);
            if (path is null)
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                VSTestTrace.SafeWriteTrace(
                    () => $"Exception while deleting artifacts directory '{path}': \n{ex}");
            }
        }

        internal static string Create(Func<string> candidatePathFactory)
        {
            for (int attempt = 1; ; attempt++)
            {
                string path = candidatePathFactory();
                bool created = false;

                try
                {
                    created = CreateDirectory(path);

                    if (IsPrivateAndEmpty(new DirectoryInfo(path)))
                    {
                        VSTestTrace.SafeWriteTrace(() => $"Created artifacts directory '{path}'.");
                        return path;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    VSTestTrace.SafeWriteTrace(
                        () => $"Could not create artifacts directory '{path}': \n{ex}");
                    RemoveRejectedDirectory(path, created);

                    // The catch above must fire regardless of attempt number: filtering it to attempt <
                    // MaxCreateAttempts would let an exception on the last attempt skip both the cleanup above and
                    // the wrapped exception below, leaving a just-created directory behind and surfacing the raw
                    // underlying exception instead.
                    if (attempt == MaxCreateAttempts)
                    {
                        throw new IOException(
                            $"Could not create a private artifacts directory for the test run under '{Path.GetDirectoryName(path)}'.", ex);
                    }

                    continue;
                }

                RemoveRejectedDirectory(path, created);

                if (attempt == MaxCreateAttempts)
                {
                    throw new IOException(
                        $"Could not create a private artifacts directory for the test run under '{Path.GetDirectoryName(path)}'.");
                }
            }
        }

        private static bool CreateDirectory(string path)
        {
            bool existedBefore = Directory.Exists(path);

            if (OperatingSystem.IsLinux())
            {
                // Create the directory with owner-only permissions atomically so another user can never observe it
                // with more permissive access. Restore owner access if a restrictive umask removed required bits.
                Directory.CreateDirectory(path, OwnerOnlyMode);

                var directory = new DirectoryInfo(path);
                if (directory.LinkTarget is null && directory.UnixFileMode != OwnerOnlyMode)
                {
                    directory.UnixFileMode = OwnerOnlyMode;
                }
            }
            else if (OperatingSystem.IsWindows())
            {
                CreateWindowsDirectory(path);
            }
            else
            {
                // Elsewhere - macOS - the default base directory (see GetBaseDirectory) is already per-user and
                // mode 0700, so no additional hardening is applied and the directory is created as-is. An
                // override through BaseDirectoryEnvironmentVariableName is trusted the same way: this class does
                // not second-guess a location the user explicitly chose.
                Directory.CreateDirectory(path);
            }

            return !existedBefore;
        }

        /// <summary>
        /// Creates the directory with a hardened, owner-only ACL. The system temporary directory is not always
        /// per-user: a process running as a service account, for example, uses C:\Windows\Temp, where
        /// BUILTIN\Users can create files and subdirectories.
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static void CreateWindowsDirectory(string path)
        {
            DirectorySecurity security = NewOwnerOnlySecurity();

            // DirectoryInfo.Create(DirectorySecurity) carries the security descriptor in the same Win32
            // CreateDirectory call that creates the directory, so for a directory that does not already exist the
            // ACL is in place before the directory is reachable under its name, just as the mkdir(2) call for
            // Linux above carries the mode. If the directory already existed, this leaves whatever ACL it already
            // had.
            new DirectoryInfo(path).Create(security);

            // Unconditionally reapplying the same ACL is what catches that pre-existing-directory case. For a
            // directory this process does not own and has no access rule on, SetAccessControl fails with
            // UnauthorizedAccessException - the Windows counterpart of UnixFileMode failing to chmod a foreign
            // directory above - and the caller retries under a different name.
            new DirectoryInfo(path).SetAccessControl(security);
        }

        /// <summary>
        /// Full control for the current user, and no access for anyone else - the Windows counterpart of
        /// <see cref="OwnerOnlyMode"/>. Administrators and SYSTEM are not granted an explicit rule either: like root
        /// on Linux, they can still reach the directory by taking ownership, so omitting them only changes what is
        /// recorded in the ACL, not who can ultimately get in.
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static DirectorySecurity NewOwnerOnlySecurity()
        {
            var security = new DirectorySecurity();

            // Blocks any access rule the parent directory's ACL would otherwise contribute, and any the parent is
            // changed to contribute later.
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(
                CurrentUserSid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            return security;
        }

        /// <summary>
        /// The current user's SID, computed once per process. <see cref="WindowsIdentity.GetCurrent"/> is only
        /// valid on Windows; nothing reaches this accessor from any other platform's code path.
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static SecurityIdentifier CurrentUserSid =>
            s_currentUserSid ??= WindowsIdentity.GetCurrent().User
                ?? throw new IOException("Could not determine the current user's security identifier.");

        private static bool IsPrivateAndEmpty(DirectoryInfo directory)
        {
            try
            {
                if (directory.LinkTarget is not null || directory.EnumerateFileSystemInfos().Any())
                {
                    return false;
                }

                if (OperatingSystem.IsLinux())
                {
                    return directory.UnixFileMode == OwnerOnlyMode;
                }

                if (OperatingSystem.IsWindows())
                {
                    return HasOwnerOnlyAcl(directory);
                }

                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// Verifies that <see cref="NewOwnerOnlySecurity"/>'s rule - and nothing else - is in effect: no inherited
        /// rule has leaked in, and no other explicit rule has been added. The Windows counterpart of comparing
        /// <see cref="DirectoryInfo.UnixFileMode"/> against <see cref="OwnerOnlyMode"/> above.
        /// </summary>
        /// <remarks>
        /// <see cref="CreateDirectory"/> unconditionally reapplies <see cref="NewOwnerOnlySecurity"/> to the
        /// directory right before this runs, so on the success path this only confirms the operating system
        /// actually persisted what was asked - the Windows counterpart of the umask widen-back above - rather than
        /// acting as the primary gate against a foreign directory. <paramref name="directory"/> is exposed as a
        /// seam for tests, which construct a wrongly-shaped rule directly rather than through that self-correcting
        /// path.
        /// </remarks>
        [SupportedOSPlatform("windows")]
        internal /*internal for testing*/ static bool HasOwnerOnlyAcl(DirectoryInfo directory)
        {
            DirectorySecurity security = directory.GetAccessControl(AccessControlSections.Access);
            if (!security.AreAccessRulesProtected)
            {
                return false;
            }

            AuthorizationRuleCollection rules =
                security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier));

            // InheritanceFlags and PropagationFlags are checked alongside identity, rights and type so that a rule
            // shaped like ours but, for example, carrying PropagationFlags.InheritOnly - which would apply only to
            // children and grant the current user no access to the directory itself - is rejected rather than
            // mistaken for the rule NewOwnerOnlySecurity creates.
            return rules.Count == 1
                && rules[0] is FileSystemAccessRule rule
                && rule.AccessControlType == AccessControlType.Allow
                && rule.IdentityReference == CurrentUserSid
                && (rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl
                && rule.InheritanceFlags == (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit)
                && rule.PropagationFlags == PropagationFlags.None;
        }

        /// <summary>
        /// Removes a directory this class created but then rejected, so that failed attempts do not accumulate in
        /// the private base directory. A pre-existing directory is left alone: it is not ours to delete.
        /// </summary>
        private static void RemoveRejectedDirectory(string path, bool created)
        {
            if (!created)
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                VSTestTrace.SafeWriteTrace(
                    () => $"Exception while removing rejected artifacts directory '{path}': \n{ex}");
            }
        }

        /// <summary>
        /// vstest.console requires an absolute path and ignores the variable otherwise. <see cref="GetBaseDirectory"/>
        /// can return a relative path - a relative <see cref="BaseDirectoryEnvironmentVariableName"/> override, or
        /// a relative <c>TMPDIR</c> feeding its <see cref="Path.GetTempPath"/> fallback - so a relative value
        /// would otherwise produce a relative path here. It would also be resolved against a different working
        /// directory by the collecting and the post-processing process, which would not agree on where the
        /// artifacts are.
        /// </summary>
        internal static string NewCandidatePath() =>
            Path.GetFullPath(Path.Combine(GetBaseDirectory(), Guid.NewGuid().ToString()));

        /// <summary>
        /// The directory <see cref="NewCandidatePath"/> creates the per-invocation GUID-named folder under.
        /// Defaults to a location that is already private to the current user, unlike the system temporary
        /// directory, which is shared with every other process on the machine: /run/user/&lt;uid&gt; on Linux,
        /// the current user's local application data folder on Windows, and the per-user temporary directory
        /// macOS assigns under /var/folders. <see cref="BaseDirectoryEnvironmentVariableName"/> overrides this
        /// unconditionally.
        /// </summary>
        /// <exception cref="IOException">
        /// On Linux, when /run/user/&lt;uid&gt; does not exist - for example in a container or a CI agent with no
        /// systemd-logind session - or geteuid() itself could not be called. Falling back to the shared system
        /// temporary directory there would silently defeat the entire point of this class. Callers already treat
        /// a failure to produce a base directory the same as a failure to create the directory itself:
        /// <see cref="GetOrCreate"/> catches it, traces it, and lets vstest.console choose its own location
        /// instead.
        /// </exception>
        /// <exception cref="GracefulException">
        /// On Windows, when the current user's local application data folder does not exist or could not be
        /// resolved. Unlike Linux this fails the run rather than being traced: <see cref="GetOrCreate"/> does not
        /// catch it.
        /// </exception>
        internal static string GetBaseDirectory()
        {
            // IsNullOrWhiteSpace rather than just checking for an empty string: a whitespace-only value is as
            // much a "nothing set" case as an empty one is, and treating it as a real relative directory would
            // silently create the artifacts directory under the current working directory instead of under the
            // intended platform-specific default.
            string overridden = Environment.GetEnvironmentVariable(BaseDirectoryEnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(overridden))
            {
                return overridden;
            }

            if (OperatingSystem.IsLinux())
            {
                return GetLinuxRuntimeDirectory();
            }

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    // Resolved through the Known Folder API (SHGetKnownFolderPath), not the %LOCALAPPDATA%
                    // variable, so a user who has repointed that variable without moving the folder it names
                    // still gets the real one.
                    return ResolveWindowsBaseDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                }
                else if (OperatingSystem.IsMacOS())
                {
                    string darwinUserTempDirectory = GetDarwinUserTempDirectory();
                    if (darwinUserTempDirectory != null)
                    {
                        return darwinUserTempDirectory;
                    }
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException)
            {
                // Only reachable on a platform missing an API this class otherwise assumes exists. Unlike the
                // Linux case below, falling back here does not reintroduce the shared-directory problem this
                // class exists to avoid: both SHGetKnownFolderPath and confstr are as available as
                // Path.GetTempPath itself is on their respective platforms, so this is not expected to be
                // reachable in practice.
                VSTestTrace.SafeWriteTrace(
                    () => $"Could not determine the platform-specific private base directory, falling back to the system temporary directory: \n{ex}");
            }

            return Path.GetTempPath();
        }

        /// <summary>
        /// Environment.GetFolderPath returns an empty string, not an exception, when the folder it resolves does
        /// not exist - for example when USERPROFILE points at a profile without AppData\Local. Falling back to
        /// the system temporary directory there would silently defeat the purpose of this class, so the run
        /// fails with a message that names the override instead.
        /// </summary>
        internal static string ResolveWindowsBaseDirectory(string localApplicationData)
        {
            if (string.IsNullOrEmpty(localApplicationData))
            {
                throw new GracefulException(
                    $"Could not determine a private base directory for the test run: the current user's local application data folder does not exist or could not be resolved. Set the {BaseDirectoryEnvironmentVariableName} environment variable to the directory the artifacts directory should be created under.");
            }

            return localApplicationData;
        }

        /// <summary>
        /// /run/user/&lt;uid&gt; - created by systemd-logind, mode 0700 and owned by the user, for every login
        /// session. Deliberately not read from XDG_RUNTIME_DIR: that variable is unset outside a login session
        /// just as often as the directory itself does not exist, so checking the directory covers both the
        /// "unset" and the "set but wrong" case in one step.
        /// </summary>
        /// <exception cref="IOException">
        /// When the directory does not exist, or geteuid() could not be called. There is deliberately no fallback
        /// to Path.GetTempPath here: that directory is exactly the shared location this class exists to avoid.
        /// <see cref="BaseDirectoryEnvironmentVariableName"/> is the supported way to use a different directory in
        /// an environment that does not have a login session, such as most containers and CI agents.
        /// </exception>
        private static string GetLinuxRuntimeDirectory()
        {
            uint uid;

            try
            {
                uid = geteuid();
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException)
            {
                throw new IOException(
                    $"Could not determine the current user id to compute a private base directory. Set the {BaseDirectoryEnvironmentVariableName} environment variable to override the base directory used for the artifacts directory.",
                    ex);
            }

            string runtimeDirectory = $"/run/user/{uid}";

            if (!Directory.Exists(runtimeDirectory))
            {
                throw new IOException(
                    $"Could not use a private base directory: '{runtimeDirectory}' does not exist. This is common in containers and CI agents that have no systemd-logind session. Set the {BaseDirectoryEnvironmentVariableName} environment variable to override the base directory used for the artifacts directory.");
            }

            return runtimeDirectory;
        }

        [DllImport("libc", SetLastError = true)]
        private static extern uint geteuid();

        /// <summary>
        /// The per-user temporary directory macOS creates under /var/folders for the current user - already mode
        /// 0700 and self-cleaning - obtained the same way the getconf DARWIN_USER_TEMP_DIR command does. Unlike
        /// Path.GetTempPath, this does not return TMPDIR when the caller has one set.
        /// </summary>
        private static string GetDarwinUserTempDirectory()
        {
            const int ConfstrDarwinUserTempDir = 65537; // _CS_DARWIN_USER_TEMP_DIR

            nuint length = confstr(ConfstrDarwinUserTempDir, IntPtr.Zero, 0);
            if (length == 0)
            {
                return null;
            }

            IntPtr buffer = Marshal.AllocHGlobal((int)length);

            try
            {
                nuint written = confstr(ConfstrDarwinUserTempDir, buffer, length);
                if (written == 0 || written > length)
                {
                    return null;
                }

                // confstr counts the null terminator in the length it returns, and Marshal.PtrToStringUTF8 does
                // not expect it to be included.
                int contentLength = (int)written - 1;
                return contentLength > 0 ? Marshal.PtrToStringUTF8(buffer, contentLength) : null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [DllImport("libSystem.dylib", SetLastError = true)]
        private static extern nuint confstr(int name, IntPtr buf, nuint len);
    }
}
