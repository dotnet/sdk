// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.DotNet.Cli.Utils;
using System.Runtime.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Test;

/// <summary>
/// Owns the directory that vstest.console writes the artifacts of a test session into, so that those artifacts
/// cannot be read or modified by other users of the machine. The SDK creates the directory - once per CLI
/// invocation, under a random GUID name, accessible to the current user only - and hands it to vstest.console
/// through <see cref="EnvironmentVariableName"/>; vstest.console owns the layout inside it.
/// </summary>
/// <remarks>
/// The SDK does not interpret the contents, with one exception: the per-session subdirectory named after the test
/// session correlation id is what tells it whether there is anything to post-process. See
/// <see cref="TestCommand.RunArtifactPostProcessingIfNeeded"/>.
/// </remarks>
internal static class VSTestArtifactsDirectory
{
    /// <summary>
    /// The environment variable vstest.console reads the directory from. It is deliberately not one of the
    /// variables the operating system, MSBuild or the runtime already act on - unlike <c>TMPDIR</c>, which would
    /// also move the diagnostics IPC socket and everything else that resolves through
    /// <see cref="Path.GetTempPath"/> - so it is safe to set on the MSBuild process as well and let it be
    /// inherited by the vstest.console process MSBuild starts.
    /// </summary>
    internal const string EnvironmentVariableName = "VSTEST_ARTIFACTS_DIRECTORY";

    /// <summary>
    /// Overrides the private base directory the per-invocation GUID-named folder is created under - instead of
    /// the platform-specific default <see cref="GetBaseDirectory"/> otherwise picks - for example to redirect
    /// the artifacts to a tmpfs or to a volume with more free space. The base directory does not have to exist,
    /// and whatever is created under it is still hardened to owner-only access like the default is.
    /// </summary>
    internal const string BaseDirectoryEnvironmentVariableName = "VSTEST_ARTIFACTS_BASE_DIRECTORY";

    /// <summary>
    /// Read, write and execute for the owner only (<c>0700</c>): the owner can list and modify the contents, no
    /// other user can.
    /// </summary>
    private const UnixFileMode OwnerOnlyMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const int MaxCreateAttempts = 5;

    private static readonly Lock s_gate = new();

    private static string? s_path;

    private static bool s_creationFailed;

    private static SecurityIdentifier? s_currentUserSid;

    /// <summary>
    /// The directory created by <see cref="GetOrCreate"/>, or <see langword="null"/> when it has not been created
    /// yet, could not be created, or has already been deleted. Unlike <see cref="GetOrCreate"/> this never
    /// creates anything.
    /// </summary>
    internal static string? Current => Volatile.Read(ref s_path);

    /// <summary>
    /// Returns the artifacts directory for this CLI invocation, creating it on first use, or
    /// <see langword="null"/> when it could not be created. All vstest.console processes of one invocation share
    /// it, which is what lets the post-processing run find what the collecting run wrote.
    /// </summary>
    /// <remarks>
    /// Being unable to create the directory does not fail the command. The private base directory can be
    /// read-only or full, and on file systems that cannot represent Unix ownership - a drvfs mount under WSL2, a
    /// bind mount, 9p, CIFS - the requested permissions do not survive being applied. In those environments
    /// vstest.console falls back to choosing the location itself, so the test run still works; only the artifact
    /// post-processing the SDK would have driven is skipped.
    /// </remarks>
    internal static string? GetOrCreate()
    {
        if (Volatile.Read(ref s_path) is { } existing)
        {
            return existing;
        }

        // Nothing reads the directory when post-processing is off, so creating one would be pure overhead. This
        // lives here rather than at the call sites so that every vstest.console the SDK starts, on every code
        // path, agrees on whether there is a directory.
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
                    // Only worth one attempt per invocation: every caller would hit the same cause.
                    s_creationFailed = true;
                    VSTestTrace.SafeWriteTrace(() => $"Could not create an artifacts directory, vstest.console will choose one: \n{ex}");
                }
            }

            return s_path;
        }
    }

    /// <summary>
    /// Deletes the directory and everything vstest.console left in it.
    /// </summary>
    /// <remarks>
    /// Called only when an invocation completes normally, whether the tests passed or failed. An invocation that
    /// crashes, throws or is interrupted never reaches its call site, which deliberately leaves the artifacts on
    /// disk for diagnosis. Failures to delete are traced and swallowed: losing a temporary directory must not
    /// fail an otherwise successful test run.
    /// </remarks>
    internal static void Delete()
    {
        lock (s_gate)
        {
            // The whole per-invocation state is reset, so a later invocation in the same process - which is what
            // the tests are - can try again.
            s_creationFailed = false;
        }

        if (Interlocked.Exchange(ref s_path, null) is not { } path)
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Only the failure is worth reporting. A successful deletion is the normal case, and tracing it would
            // add a line after the attachments that vstest.console prints, which callers parse positionally.
            VSTestTrace.SafeWriteTrace(() => $"Exception while deleting artifacts directory '{path}': \n{ex}");
        }
    }

    /// <summary>
    /// Creates a directory only the current user can access, retrying under a different name when the candidate
    /// path turns out to be occupied. <paramref name="candidatePathFactory"/> is a seam for tests, which cannot
    /// otherwise reach the collision handling: production always supplies random GUID paths.
    /// </summary>
    internal /*internal for testing*/ static string Create(Func<string> candidatePathFactory)
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
                VSTestTrace.SafeWriteTrace(() => $"Could not create artifacts directory '{path}': \n{ex}");
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

    /// <summary>
    /// Returns whether the leaf directory was created by this call rather than already existing.
    /// </summary>
    private static bool CreateDirectory(string path)
    {
        bool existedBefore = Directory.Exists(path);

        if (OperatingSystem.IsLinux())
        {
            // Directory.CreateDirectory maps to a single mkdir(2) call carrying the mode, so the permissions are
            // in place before the directory is reachable under its name. umask can only clear permission bits,
            // never add them, so the result is never more permissive than owner-only.
            Directory.CreateDirectory(path, OwnerOnlyMode);

            // mkdir(2) applies umask to the mode, which - with an unusual umask - can leave the owner without
            // write or execute permission. The directory was created empty and is owned by the current user, so
            // widening it back to exactly 0700 cannot expose anything to another user.
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
            // mode 0700, so no additional hardening is applied and the directory is created as-is. An override
            // through BaseDirectoryEnvironmentVariableName is trusted the same way: this class does not
            // second-guess a location the user explicitly chose.
            Directory.CreateDirectory(path);
        }

        return !existedBefore;
    }

    /// <summary>
    /// Creates the directory with a hardened, owner-only ACL. The base directory is not always per-user: a
    /// process running as a service account, for example, resolves local application data under
    /// C:\Windows\ServiceProfiles, and an overridden base directory could be anything.
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
            // Directory.CreateDirectory succeeds silently when the path already exists, and then neither the
            // permissions nor the contents are ours. A random GUID makes that practically impossible, but verify
            // rather than hand a directory the SDK does not control to vstest.console.
            //
            // A symbolic link would resolve to a directory the SDK does not own, and the permissions reported for
            // it are those of the target rather than of the link.
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
    /// Removes a directory this class created but then rejected, so that failed attempts do not accumulate in the
    /// private base directory. A pre-existing directory is left alone: it is not ours to delete.
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
            VSTestTrace.SafeWriteTrace(() => $"Exception while removing rejected artifacts directory '{path}': \n{ex}");
        }
    }

    /// <summary>
    /// vstest.console requires an absolute path and ignores the variable otherwise. <see cref="GetBaseDirectory"/>
    /// can return a relative path - a relative <see cref="BaseDirectoryEnvironmentVariableName"/> override, or a
    /// relative <c>TMPDIR</c> feeding its <see cref="Path.GetTempPath"/> fallback - so a relative value would
    /// otherwise produce a relative path here. It would also be resolved against a different working directory by
    /// the collecting and the post-processing process, which would not agree on where the artifacts are.
    /// </summary>
    internal /*internal for testing*/ static string NewCandidatePath() => Path.GetFullPath(Path.Combine(GetBaseDirectory(), Guid.NewGuid().ToString()));

    /// <summary>
    /// The directory <see cref="NewCandidatePath"/> creates the per-invocation GUID-named folder under.
    /// </summary>
    /// <remarks>
    /// Defaults to a location that is already private to the current user, unlike the system temporary directory,
    /// which is shared with every other process on the machine: <c>/run/user/&lt;uid&gt;</c> on Linux, the current
    /// user's local application data folder on Windows, and the per-user temporary directory macOS assigns under
    /// <c>/var/folders</c>. <see cref="BaseDirectoryEnvironmentVariableName"/> overrides this unconditionally, for
    /// example to redirect the artifacts to a tmpfs or to a volume with more free space.
    /// </remarks>
    /// <exception cref="IOException">
    /// On Linux, when <c>/run/user/&lt;uid&gt;</c> does not exist - for example in a container or a CI agent with
    /// no systemd-logind session - or geteuid() itself could not be called. Falling back to the shared system
    /// temporary directory there would silently defeat the entire point of this class: giving every caller of
    /// <see cref="GetOrCreate"/> a private directory, not the one every other process on the machine can also
    /// read from. Callers already treat a failure to produce a base directory the same as a failure to create
    /// the directory itself: <see cref="GetOrCreate"/> catches it, traces it, and lets vstest.console choose its
    /// own location instead.
    /// </exception>
    /// <exception cref="GracefulException">
    /// On Windows, when the current user's local application data folder does not exist or could not be resolved.
    /// Unlike Linux this fails the run rather than being traced: <see cref="GetOrCreate"/> does not catch it.
    /// </exception>
    internal /*internal for testing*/ static string GetBaseDirectory()
    {
        // IsNullOrWhiteSpace rather than just checking for an empty string: a whitespace-only value is as much a
        // "nothing set" case as an empty one is, and treating it as a real relative directory would silently
        // create the artifacts directory under the current working directory instead of under the intended
        // platform-specific default.
        if (Environment.GetEnvironmentVariable(BaseDirectoryEnvironmentVariableName) is { } overridden
            && !string.IsNullOrWhiteSpace(overridden))
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
                // Resolved through the Known Folder API (SHGetKnownFolderPath), not the %LOCALAPPDATA% variable,
                // so a user who has repointed that variable without moving the folder it names still gets the
                // real one.
                return ResolveWindowsBaseDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            }
            else if (OperatingSystem.IsMacOS() && GetDarwinUserTempDirectory() is { } darwinUserTempDirectory)
            {
                return darwinUserTempDirectory;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException)
        {
            // Only reachable on a platform missing an API this class otherwise assumes exists. Unlike the Linux
            // case below, falling back here does not reintroduce the shared-directory problem this class exists
            // to avoid: both SHGetKnownFolderPath and confstr are as available as Path.GetTempPath itself is on
            // their respective platforms, so this is not expected to be reachable in practice.
            VSTestTrace.SafeWriteTrace(() => $"Could not determine the platform-specific private base directory, falling back to the system temporary directory: \n{ex}");
        }

        return Path.GetTempPath();
    }

    /// <summary>
    /// Environment.GetFolderPath returns an empty string, not an exception, when the folder it resolves does not
    /// exist - for example when USERPROFILE points at a profile without AppData\Local. Falling back to the system
    /// temporary directory there would silently defeat the purpose of this class, so the run fails with a message
    /// that names the override instead.
    /// </summary>
    internal /*internal for testing*/ static string ResolveWindowsBaseDirectory(string? localApplicationData)
    {
        if (string.IsNullOrEmpty(localApplicationData))
        {
            throw new GracefulException(
                $"Could not determine a private base directory for the test run: the current user's local application data folder does not exist or could not be resolved. Set the {BaseDirectoryEnvironmentVariableName} environment variable to the directory the artifacts directory should be created under.");
        }

        return localApplicationData;
    }

    /// <summary>
    /// <c>/run/user/&lt;uid&gt;</c> - created by systemd-logind, mode <c>0700</c> and owned by the user, for
    /// every login session. Deliberately not read from <c>XDG_RUNTIME_DIR</c>: that variable is unset outside a
    /// login session just as often as the directory itself does not exist, so checking the directory covers both
    /// the "unset" and the "set but wrong" case in one step.
    /// </summary>
    /// <exception cref="IOException">
    /// When the directory does not exist, or <see cref="geteuid"/> could not be called. There is deliberately no
    /// fallback to <see cref="Path.GetTempPath"/> here: that directory is exactly the shared location this class
    /// exists to avoid, so silently using it would be worse than not creating a private artifacts directory at
    /// all. <see cref="BaseDirectoryEnvironmentVariableName"/> is the supported way to use a different directory
    /// in an environment that does not have a login session, such as most containers and CI agents.
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
    /// The per-user temporary directory macOS creates under <c>/var/folders</c> for the current user - already
    /// mode <c>0700</c> and self-cleaning - obtained the same way the <c>getconf DARWIN_USER_TEMP_DIR</c> command
    /// does. Unlike <see cref="Path.GetTempPath"/>, this does not return <c>TMPDIR</c> when the caller has one
    /// set: the two usually agree, but confstr is what actually backs the per-user directory the operating system
    /// assigns.
    /// </summary>
    private static string? GetDarwinUserTempDirectory()
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

            // confstr counts the null terminator in the length it returns, and Marshal.PtrToStringUTF8 does not
            // expect it to be included.
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
