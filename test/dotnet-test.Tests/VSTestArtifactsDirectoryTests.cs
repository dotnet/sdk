// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using Microsoft.DotNet.Tools.Test;
using TestCommand = Microsoft.DotNet.Tools.Test.TestCommand;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    // The artifacts directory is process-wide state shared with every in-process VSTestForwardingApp and
    // TestCommand, so these tests must not run in parallel with the other tests that create them.
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class VSTestArtifactsDirectoryTests : IDisposable
    {
        private const UnixFileMode OwnerOnlyMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

        /// <summary>
        /// Set only when the default base directory cannot be resolved - on Windows when there is no local application data folder, on Linux when this agent has no /run/user/&lt;uid&gt; - common for hosted CI agents and
        /// containers without a systemd-logind session. VSTestArtifactsDirectory.GetBaseDirectory deliberately
        /// throws rather than falling back to the shared system temporary directory there, so the general-purpose
        /// tests in this class need a working override to get a base directory at all.
        /// </summary>
        private readonly string _baseDirectoryOverride;

        public VSTestArtifactsDirectoryTests()
        {
            Telemetry.Telemetry.DisableForTests();
            VSTestArtifactsDirectory.Delete();

            if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
            {
                try
                {
                    VSTestArtifactsDirectory.GetBaseDirectory();
                }
                catch (Exception ex) when (ex is IOException or Microsoft.DotNet.Cli.Utils.GracefulException)
                {
                    _baseDirectoryOverride = Path.Combine(Path.GetTempPath(), "vstest-artifacts-tests-" + Guid.NewGuid());
                    Directory.CreateDirectory(_baseDirectoryOverride);
                    Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, _baseDirectoryOverride);
                }
            }
        }

        public void Dispose()
        {
            VSTestArtifactsDirectory.Delete();

            if (_baseDirectoryOverride != null)
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, null);
                Directory.Delete(_baseDirectoryOverride, recursive: true);
            }
        }

        [Fact]
        public void ItCreatesAndReusesAGuidNamedDirectory()
        {
            string first = VSTestArtifactsDirectory.GetOrCreate();
            string second = VSTestArtifactsDirectory.GetOrCreate();

            Directory.Exists(first).Should().BeTrue();
            Path.GetDirectoryName(first).Should().Be(Path.TrimEndingDirectorySeparator(VSTestArtifactsDirectory.GetBaseDirectory()));
            Guid.TryParse(Path.GetFileName(first), out _).Should().BeTrue();
            second.Should().Be(first);
            VSTestArtifactsDirectory.Current.Should().Be(first);
        }

        [Fact]
        public void ItHonorsTheBaseDirectoryOverride()
        {
            string overrideDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(overrideDirectory);

            try
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, overrideDirectory);

                VSTestArtifactsDirectory.GetBaseDirectory().Should().Be(overrideDirectory);

                string created = VSTestArtifactsDirectory.GetOrCreate();

                Directory.Exists(created).Should().BeTrue();
                Path.GetDirectoryName(created).Should().Be(Path.TrimEndingDirectorySeparator(overrideDirectory));
            }
            finally
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, _baseDirectoryOverride);
                Directory.Delete(overrideDirectory, recursive: true);
            }
        }

        [Fact]
        public void ItTreatsAWhitespaceOnlyBaseDirectoryOverrideAsUnset()
        {
            try
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, "   ");

                // A whitespace-only override must not be treated as a real relative directory - that would
                // silently create the artifacts directory under the current working directory.
                VSTestArtifactsDirectory.GetBaseDirectory().Should().NotBe("   ");
            }
            finally
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, _baseDirectoryOverride);
            }
        }

        [PlatformSpecificFact(TestPlatforms.Linux)]
        public void ItUsesTheRuntimeDirectoryOnLinuxOrThrowsWithAnOverrideHint()
        {
            // There is deliberately no fallback to the system temporary directory on Linux: that would silently
            // defeat the point of this class. Not every Linux CI agent runs within a login session that has
            // /run/user/<uid>, so this asserts whichever of the two contracts applies to this agent - bypassing
            // the constructor's own override, which exists only so the general-purpose tests above do not depend
            // on which of those two contracts applies.
            try
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, null);

                string baseDirectory = VSTestArtifactsDirectory.GetBaseDirectory();

                baseDirectory.Should().StartWith("/run/user/");
                Directory.Exists(baseDirectory).Should().BeTrue();
            }
            catch (IOException ex)
            {
                ex.Message.Should().Contain(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName);
            }
            finally
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, _baseDirectoryOverride);
            }
        }

        [PlatformSpecificFact(TestPlatforms.Windows)]
        public void ItUsesLocalApplicationDataOnWindows()
        {
            try
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, null);

                VSTestArtifactsDirectory.GetBaseDirectory().Should().Be(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            }
            finally
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, _baseDirectoryOverride);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ItFailsTheRunWithAnOverrideHintWhenLocalApplicationDataCannotBeResolved(string localApplicationData)
        {
            Action resolve = () => VSTestArtifactsDirectory.ResolveWindowsBaseDirectory(localApplicationData);

            resolve.Should().Throw<Microsoft.DotNet.Cli.Utils.GracefulException>()
                .WithMessage($"*{VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName}*");
        }

        [Fact]
        public void ItUsesAResolvedLocalApplicationDataAsIs()
        {
            VSTestArtifactsDirectory.ResolveWindowsBaseDirectory(@"C:\Users\someone\AppData\Local")
                .Should().Be(@"C:\Users\someone\AppData\Local");
        }

        [Fact]
        public void ItDoesNotSwallowAFailureToProduceABaseDirectory()
        {
            Action create = () => VSTestArtifactsDirectory.Create(
                () => throw new Microsoft.DotNet.Cli.Utils.GracefulException("no base directory"));

            create.Should().Throw<Microsoft.DotNet.Cli.Utils.GracefulException>().WithMessage("no base directory");
        }

        [PlatformSpecificFact(TestPlatforms.OSX)]
        public void ItUsesTheDarwinUserTemporaryDirectoryOnMacOSInsteadOfTmpdir()
        {
            string previousTmpDir = Environment.GetEnvironmentVariable("TMPDIR");
            string distinctTmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                Directory.CreateDirectory(distinctTmpDir);
                Environment.SetEnvironmentVariable("TMPDIR", distinctTmpDir);

                string baseDirectory = VSTestArtifactsDirectory.GetBaseDirectory();

                baseDirectory.Should().StartWith("/var/folders/");
                baseDirectory.Should().NotBe(distinctTmpDir);
            }
            finally
            {
                Environment.SetEnvironmentVariable("TMPDIR", previousTmpDir);
                Directory.Delete(distinctTmpDir, recursive: true);
            }
        }

        [PlatformSpecificFact(TestPlatforms.Linux)]
        public void ItCreatesTheDirectoryWithOwnerOnlyPermissionsOnLinux()
        {
            string path = VSTestArtifactsDirectory.GetOrCreate();

            new DirectoryInfo(path).UnixFileMode.Should().Be(OwnerOnlyMode);
        }

        [PlatformSpecificFact(TestPlatforms.Windows)]
        [SupportedOSPlatform("windows")]
        public void ItCreatesTheDirectoryAccessibleOnlyToTheCurrentUserOnWindows()
        {
            string path = VSTestArtifactsDirectory.GetOrCreate();

            DirectorySecurity security = new DirectoryInfo(path).GetAccessControl(AccessControlSections.Access);
            var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>();

            security.AreAccessRulesProtected.Should().BeTrue();
            rules.Should().ContainSingle().Which.Should().Match<FileSystemAccessRule>(rule =>
                rule.AccessControlType == AccessControlType.Allow
                && rule.IdentityReference == WindowsIdentity.GetCurrent().User
                && (rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl);
        }

        [PlatformSpecificFact(TestPlatforms.Windows)]
        [SupportedOSPlatform("windows")]
        public void ItDoesNotAcceptARuleWithTheWrongPropagationFlags()
        {
            // Shaped like the rule VSTestArtifactsDirectory creates on Windows - same identity, rights and type -
            // except for PropagationFlags.InheritOnly, which would apply the rule only to children and grant the
            // current user no access to the directory itself.
            //
            // CreateDirectory unconditionally reapplies the correct ACL to any directory it touches, which would
            // silently correct this before verification ever saw it, so the wrongly-shaped rule is applied
            // directly here rather than through VSTestArtifactsDirectory.Create.
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(path);

            try
            {
                var security = new DirectorySecurity();
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                security.AddAccessRule(new FileSystemAccessRule(
                    WindowsIdentity.GetCurrent().User,
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.InheritOnly,
                    AccessControlType.Allow));
                new DirectoryInfo(path).SetAccessControl(security);

                VSTestArtifactsDirectory.HasOwnerOnlyAcl(new DirectoryInfo(path)).Should().BeFalse();
            }
            finally
            {
                // The InheritOnly ACE above denies the owner delete access to the directory itself; restore a
                // normal, non-InheritOnly rule before cleaning up.
                var cleanupSecurity = new DirectorySecurity();
                cleanupSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                cleanupSecurity.AddAccessRule(new FileSystemAccessRule(
                    WindowsIdentity.GetCurrent().User,
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                new DirectoryInfo(path).SetAccessControl(cleanupSecurity);

                Directory.Delete(path, recursive: true);
            }
        }

        [Fact]
        public void ItGivesUpWithAClearErrorWhenEveryAttemptThrows()
        {
            // A regular file, not a directory: creating anything underneath it throws on every attempt, on both
            // platforms, which is what exercises the last attempt specifically - the one case where the retry
            // loop's own catch clause previously did not fire, leaving a just-created directory behind and the
            // raw underlying exception in place of this wrapped one.
            string blocker = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            File.WriteAllText(blocker, string.Empty);
            string underBlocker = Path.Combine(blocker, "sub");

            try
            {
                Action create = () => VSTestArtifactsDirectory.Create(() => underBlocker);

                create.Should().Throw<IOException>()
                    .WithMessage("*artifacts directory*")
                    .Which.InnerException.Should().NotBeNull();
            }
            finally
            {
                File.Delete(blocker);
            }
        }

        [Fact]
        public void ItPassesTheSameDirectoryToDirectAndMSBuildVSTestInvocations()
        {
            string path = VSTestArtifactsDirectory.GetOrCreate();

            var directEnvironment = new VSTestForwardingApp(Array.Empty<string>())
                .GetProcessStartInfo().Environment;
            var msbuildEnvironment = TestCommand
                .FromArgs(new[] { "--disable-build-servers" }, "<testSessionCorrelationId>", "<msbuildpath>")
                .GetProcessStartInfo().Environment;

            directEnvironment[VSTestArtifactsDirectory.EnvironmentVariableName].Should().Be(path);
            msbuildEnvironment[VSTestArtifactsDirectory.EnvironmentVariableName].Should().Be(path);
        }

        [Fact]
        public void ItDoesNotCreateAnArtifactsDirectoryForHelp()
        {
            var environment = new VSTestForwardingApp(
                new[] { "--help" },
                useArtifactsDirectory: false).GetProcessStartInfo().Environment;

            environment.Should().NotContainKey(VSTestArtifactsDirectory.EnvironmentVariableName);
            VSTestArtifactsDirectory.Current.Should().BeNull();
        }

        [Fact]
        public void ItRejectsAnOccupiedCandidateAndDoesNotDeleteIt()
        {
            string occupied = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(occupied);
            File.WriteAllText(Path.Combine(occupied, "foreign.txt"), "not ours");

            try
            {
                int candidates = 0;
                string path = VSTestArtifactsDirectory.Create(() =>
                {
                    candidates++;
                    return candidates == 1
                        ? occupied
                        : Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                });

                candidates.Should().Be(2);
                path.Should().NotBe(occupied);
                Directory.Exists(occupied).Should().BeTrue();
                Directory.Delete(path);
            }
            finally
            {
                Directory.Delete(occupied, recursive: true);
            }
        }

        [Fact]
        public void ItOnlyDiscoversSessionsInsideThePrivateArtifactsDirectory()
        {
            string correlationId = $"{Environment.ProcessId}_{Guid.NewGuid()}";
            string systemTempCandidate = Path.Combine(Path.GetTempPath(), correlationId);
            Directory.CreateDirectory(systemTempCandidate);

            try
            {
                TestCommand.TryFindArtifactDirectory(correlationId, out string artifactDirectory)
                    .Should().BeFalse();
                artifactDirectory.Should().BeNull();

                string expected = Path.Combine(VSTestArtifactsDirectory.GetOrCreate(), correlationId);
                Directory.CreateDirectory(expected);

                TestCommand.TryFindArtifactDirectory(correlationId, out artifactDirectory)
                    .Should().BeTrue();
                artifactDirectory.Should().Be(expected);
            }
            finally
            {
                Directory.Delete(systemTempCandidate, recursive: true);
            }
        }

        [Fact]
        public void ItDeletesTheDirectoryAndItsContents()
        {
            string path = VSTestArtifactsDirectory.GetOrCreate();
            Directory.CreateDirectory(Path.Combine(path, "session"));
            File.WriteAllText(Path.Combine(path, "session", "executionComplete.json"), "{}");

            VSTestArtifactsDirectory.Delete();

            Directory.Exists(path).Should().BeFalse();
            VSTestArtifactsDirectory.Current.Should().BeNull();
        }
    }
}
