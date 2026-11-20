// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.DotNet.Cli.Commands.Test;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    /// <summary>
    /// Covers the defensive paths of <see cref="VSTestArtifactsDirectory"/> that a normal run never reaches: an
    /// unusual umask, a candidate path that is already occupied or is a symbolic link, and repeated or racing
    /// calls.
    /// </summary>
    [TestClass]
    public class VSTestArtifactsDirectoryHardeningTests
    {
        private const UnixFileMode OwnerOnlyMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

        /// <summary>Masks out the owner's write and execute bits, which mkdir(2) would otherwise apply.</summary>
        private const uint RestrictiveUmask = 0x0C0; // 0300

        private readonly List<string> _createdPaths = [];

        /// <summary>
        /// Set only when the default base directory cannot be resolved - on Windows when there is no local application data folder, on Linux when this agent has no <c>/run/user/&lt;uid&gt;</c> - common for hosted CI agents and
        /// containers without a systemd-logind session. <see cref="VSTestArtifactsDirectory.GetBaseDirectory"/>
        /// deliberately throws rather than falling back to the shared system temporary directory there, so the
        /// <see cref="VSTestArtifactsDirectory.GetOrCreate"/>-based tests below need a working override to get a
        /// base directory at all. Covered on its own terms in <c>VSTestArtifactsDirectoryTests</c>.
        /// </summary>
        private static string? s_classLevelBaseDirectoryOverride;

        [DllImport("libc", EntryPoint = "umask", SetLastError = true)]
        private static extern uint SetUmask(uint mask);

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
            {
                try
                {
                    VSTestArtifactsDirectory.GetBaseDirectory();
                }
                catch (Exception ex) when (ex is IOException or Microsoft.DotNet.Cli.Utils.GracefulException)
                {
                    s_classLevelBaseDirectoryOverride = Path.Combine(Path.GetTempPath(), "vstest-artifacts-hardening-tests-" + Guid.NewGuid());
                    Directory.CreateDirectory(s_classLevelBaseDirectoryOverride);
                    Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, s_classLevelBaseDirectoryOverride);
                }
            }
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (s_classLevelBaseDirectoryOverride is not null)
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, null);
                Directory.Delete(s_classLevelBaseDirectoryOverride, recursive: true);
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (string path in _createdPaths)
            {
                try
                {
                    if (Directory.ResolveLinkTarget(path, returnFinalTarget: false) is not null)
                    {
                        Directory.Delete(path);
                    }
                    else if (Directory.Exists(path))
                    {
                        Directory.Delete(path, recursive: true);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }

            _createdPaths.Clear();

            // The per-invocation directory is process-wide state that several tests here reset.
            VSTestArtifactsDirectory.GetOrCreate();
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Linux)]
        public void ItCreatesTheDirectoryWithOwnerOnlyPermissionsEvenUnderARestrictiveUmask()
        {
            uint previousUmask = SetUmask(RestrictiveUmask);

            try
            {
                string path = Track(VSTestArtifactsDirectory.Create(NewCandidatePath));

                // mkdir(2) would have produced 0400 here; the owner must still be able to write into and list
                // the directory, and no permission may leak to group or other.
                new DirectoryInfo(path).UnixFileMode.Should().Be(OwnerOnlyMode);
            }
            finally
            {
                SetUmask(previousUmask);
            }
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        [SupportedOSPlatform("windows")]
        public void ItCreatesTheDirectoryWithOwnerOnlyAclEvenUnderAPermissiveParentAcl()
        {
            // Simulates a system temporary directory like C:\Windows\Temp, where BUILTIN\Users can create files
            // and subdirectories: the new directory must not inherit that.
            string parent = Track(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            Directory.CreateDirectory(parent);

            var parentInfo = new DirectoryInfo(parent);
            DirectorySecurity parentSecurity = parentInfo.GetAccessControl(AccessControlSections.Access);
            parentSecurity.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                FileSystemRights.Modify,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            parentInfo.SetAccessControl(parentSecurity);

            string path = Track(VSTestArtifactsDirectory.Create(() => Path.Combine(parent, Guid.NewGuid().ToString())));

            DirectorySecurity security = new DirectoryInfo(path).GetAccessControl(AccessControlSections.Access);
            IEnumerable<FileSystemAccessRule> rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>();

            security.AreAccessRulesProtected.Should().BeTrue("the inherited BUILTIN\\Users rule from the parent must not apply");
            rules.Should().ContainSingle().Which.IdentityReference.Should().Be(WindowsIdentity.GetCurrent().User);
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        [SupportedOSPlatform("windows")]
        public void ItDoesNotAcceptARuleWithTheWrongPropagationFlags()
        {
            // Shaped like the rule NewOwnerOnlySecurity creates - same identity, rights and type - except for
            // PropagationFlags.InheritOnly, which would apply the rule only to children and grant the current
            // user no access to the directory itself.
            //
            // CreateDirectory unconditionally reapplies NewOwnerOnlySecurity to any directory it touches, which
            // would silently correct this before verification ever saw it, so the wrongly-shaped rule is applied
            // directly here rather than through VSTestArtifactsDirectory.Create.
            string path = Track(NewCandidatePath());
            Directory.CreateDirectory(path);

            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(
                WindowsIdentity.GetCurrent().User!,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.InheritOnly,
                AccessControlType.Allow));
            new DirectoryInfo(path).SetAccessControl(security);

            VSTestArtifactsDirectory.HasOwnerOnlyAcl(new DirectoryInfo(path)).Should().BeFalse();
        }

        [TestMethod]
        public void ItPicksAnotherNameWhenTheCandidatePathIsAlreadyOccupied()
        {
            string occupied = Track(NewCandidatePath());
            Directory.CreateDirectory(occupied);
            File.WriteAllText(Path.Combine(occupied, "foreign.txt"), "not ours");

            int candidates = 0;
            string path = Track(VSTestArtifactsDirectory.Create(() =>
            {
                candidates++;
                return candidates == 1 ? occupied : NewCandidatePath();
            }));

            candidates.Should().Be(2, "the occupied candidate should have been rejected");
            path.Should().NotBe(occupied);
            new DirectoryInfo(path).EnumerateFileSystemInfos().Should().BeEmpty();
            Directory.Exists(occupied).Should().BeTrue("a directory the SDK did not create must not be removed");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Linux)]
        public void ItPicksAnotherNameWhenTheCandidatePathIsASymbolicLink()
        {
            // A symbolic link pointing at an otherwise perfectly fine directory: it must still be rejected,
            // because the target is not a directory the SDK created and controls.
            string target = Track(NewCandidatePath());
            Directory.CreateDirectory(target);

            string link = Track(NewCandidatePath());
            Directory.CreateSymbolicLink(link, target);

            int candidates = 0;
            string path = Track(VSTestArtifactsDirectory.Create(() =>
            {
                candidates++;
                return candidates == 1 ? link : NewCandidatePath();
            }));

            candidates.Should().Be(2, "the symbolic link should have been rejected");
            path.Should().NotBe(link);
            new DirectoryInfo(path).LinkTarget.Should().BeNull();
        }

        [TestMethod]
        public void ItGivesUpWithAClearErrorWhenNoUsableDirectoryCanBeCreated()
        {
            string occupied = Track(NewCandidatePath());
            Directory.CreateDirectory(occupied);
            File.WriteAllText(Path.Combine(occupied, "foreign.txt"), "not ours");

            Action create = () => VSTestArtifactsDirectory.Create(() => occupied);

            create.Should().Throw<IOException>().WithMessage("*artifacts directory*");
        }

        [TestMethod]
        public void ItGivesUpWithAClearErrorWhenEveryAttemptThrows()
        {
            // A regular file, not a directory: creating anything underneath it throws on every attempt, on both
            // platforms, which is what exercises the last attempt specifically - the one case where the retry
            // loop's own catch clause previously did not fire, leaving a just-created directory behind and the
            // raw underlying exception in place of this wrapped one.
            string blocker = NewCandidatePath();
            File.WriteAllText(blocker, string.Empty);
            string underBlocker = Path.Combine(blocker, "sub");

            try
            {
                Action create = () => VSTestArtifactsDirectory.Create(() => underBlocker);

                create.Should().Throw<IOException>()
                    .WithMessage("*artifacts directory*")
                    .Which.InnerException.Should().NotBeNull("the underlying I/O failure should be preserved for diagnosis");
            }
            finally
            {
                File.Delete(blocker);
            }
        }

        [TestMethod]
        public void ItToleratesBeingDeletedTwice()
        {
            VSTestArtifactsDirectory.GetOrCreate();

            VSTestArtifactsDirectory.Delete();
            VSTestArtifactsDirectory.Current.Should().BeNull();

            // A second delete must be a no-op rather than a failure.
            VSTestArtifactsDirectory.Delete();
            VSTestArtifactsDirectory.Current.Should().BeNull();
        }

        [TestMethod]
        public void ItDoesNotThrowWhenTheDirectoryIsAlreadyGone()
        {
            string path = VSTestArtifactsDirectory.GetOrCreate()!;
            Directory.Delete(path, recursive: true);

            // Losing the directory must never fail an otherwise successful test run.
            VSTestArtifactsDirectory.Delete();

            VSTestArtifactsDirectory.Current.Should().BeNull();
        }

        [TestMethod]
        public void ItCreatesASingleDirectoryWhenCalledConcurrently()
        {
            VSTestArtifactsDirectory.Delete();

            const int callers = 16;
            string[] results = new string[callers];

            Parallel.For(0, callers, i => results[i] = VSTestArtifactsDirectory.GetOrCreate()!);

            results.Distinct().Should().HaveCount(1, "every caller in the invocation shares one directory");
            Directory.Exists(results[0]).Should().BeTrue();
        }

        private static string NewCandidatePath() => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        private string Track(string path)
        {
            _createdPaths.Add(path);
            return path;
        }
    }
}
