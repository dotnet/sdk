// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Telemetry;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using TestCommand = Microsoft.DotNet.Cli.Commands.Test.TestCommand;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    [TestClass]
    public class VSTestArtifactsDirectoryTests
    {
        private const UnixFileMode OwnerOnlyMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

        /// <summary>
        /// Set only when the default base directory cannot be resolved - on Windows when there is no local application data folder, on Linux when this agent has no <c>/run/user/&lt;uid&gt;</c> - common for hosted CI agents and
        /// containers without a systemd-logind session. <see cref="VSTestArtifactsDirectory.GetBaseDirectory"/>
        /// deliberately throws rather than falling back to the shared system temporary directory there (see
        /// <see cref="ItUsesTheRuntimeDirectoryOnLinuxOrThrowsWithAnOverrideHintWhenUnavailable"/>), so the
        /// general-purpose tests below - which are not themselves about that contract - need a working override
        /// to get a base directory at all.
        /// </summary>
        private static string? s_classLevelBaseDirectoryOverride;

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            TelemetryClient.DisabledForTests = true;

            if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
            {
                try
                {
                    VSTestArtifactsDirectory.GetBaseDirectory();
                }
                catch (Exception ex) when (ex is IOException or Microsoft.DotNet.Cli.Utils.GracefulException)
                {
                    s_classLevelBaseDirectoryOverride = Path.Combine(Path.GetTempPath(), "vstest-artifacts-tests-" + Guid.NewGuid());
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
        public void Cleanup() => VSTestArtifactsDirectory.GetOrCreate();

        [TestMethod]
        public void ItCreatesAGuidNamedDirectoryUnderThePlatformSpecificPrivateBaseDirectory()
        {
            string path = VSTestArtifactsDirectory.GetOrCreate()!;

            Directory.Exists(path).Should().BeTrue();
            Path.GetDirectoryName(path).Should().Be(Path.TrimEndingDirectorySeparator(VSTestArtifactsDirectory.GetBaseDirectory()));
            Guid.TryParse(Path.GetFileName(path), out _).Should().BeTrue("the directory name should be a random GUID");
        }

        [TestMethod]
        public void ItHonorsTheBaseDirectoryOverride()
        {
            string? previousOverride = Environment.GetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName);
            string overrideDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                Directory.CreateDirectory(overrideDirectory);
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, overrideDirectory);

                VSTestArtifactsDirectory.GetBaseDirectory().Should().Be(overrideDirectory);

                string candidatePath = Path.GetFullPath(VSTestArtifactsDirectory.NewCandidatePath());
                Path.GetDirectoryName(candidatePath).Should().Be(Path.TrimEndingDirectorySeparator(overrideDirectory));

                // Exercises the real path end to end - creation, permission hardening and propagation to
                // vstest.console - not just the base-directory computation above.
                VSTestArtifactsDirectory.Delete();
                string createdPath = VSTestArtifactsDirectory.GetOrCreate()!;

                Directory.Exists(createdPath).Should().BeTrue();
                Path.GetDirectoryName(createdPath).Should().Be(Path.TrimEndingDirectorySeparator(overrideDirectory));

                var environment = new VSTestForwardingApp([]).GetProcessStartInfo().Environment;
                environment[VSTestArtifactsDirectory.EnvironmentVariableName].Should().Be(createdPath);
            }
            finally
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, previousOverride);
                VSTestArtifactsDirectory.Delete();
                Directory.Delete(overrideDirectory, recursive: true);
            }
        }

        [TestMethod]
        public void ItTreatsAWhitespaceOnlyBaseDirectoryOverrideAsUnset()
        {
            string? previousOverride = Environment.GetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName);

            try
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, "   ");

                // A whitespace-only override must not be treated as a real relative directory - that would
                // silently create the artifacts directory under the current working directory.
                VSTestArtifactsDirectory.GetBaseDirectory().Should().NotBe("   ");
            }
            finally
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, previousOverride);
            }
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Linux)]
        public void ItUsesTheRuntimeDirectoryOnLinuxOrThrowsWithAnOverrideHintWhenUnavailable()
        {
            // There is deliberately no fallback to the system temporary directory on Linux: that would silently
            // defeat the point of this class. Not every Linux CI agent runs within a login session that has
            // /run/user/<uid>, so this asserts whichever of the two contracts applies to this agent - bypassing
            // ClassInit's own override, which exists only so the general-purpose tests above do not depend on
            // which of those two contracts applies.
            string? previousOverride = Environment.GetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName);

            try
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, null);

                string baseDirectory = VSTestArtifactsDirectory.GetBaseDirectory();

                baseDirectory.Should().StartWith("/run/user/");
                Directory.Exists(baseDirectory).Should().BeTrue();
            }
            catch (IOException ex)
            {
                ex.Message.Should().Contain(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName,
                    "the error should point the user at the override rather than leaving them stuck");
            }
            finally
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, previousOverride);
            }
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void ItUsesLocalApplicationDataOnWindows()
        {
            string? previousOverride = Environment.GetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName);

            try
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, null);

                VSTestArtifactsDirectory.GetBaseDirectory().Should().Be(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            }
            finally
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, previousOverride);
            }
        }

        [TestMethod]
        public void ItFailsTheRunWithAnOverrideHintWhenLocalApplicationDataCannotBeResolved()
        {
            foreach (string? localApplicationData in new string?[] { null, string.Empty })
            {
                Action resolve = () => VSTestArtifactsDirectory.ResolveWindowsBaseDirectory(localApplicationData);

                resolve.Should().Throw<Microsoft.DotNet.Cli.Utils.GracefulException>()
                    .WithMessage($"*{VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName}*");
            }
        }

        [TestMethod]
        public void ItUsesAResolvedLocalApplicationDataAsIs()
        {
            VSTestArtifactsDirectory.ResolveWindowsBaseDirectory(@"C:\Users\someone\AppData\Local")
                .Should().Be(@"C:\Users\someone\AppData\Local");
        }

        [TestMethod]
        public void ItDoesNotSwallowAFailureToProduceABaseDirectory()
        {
            Action create = () => VSTestArtifactsDirectory.Create(
                () => throw new Microsoft.DotNet.Cli.Utils.GracefulException("no base directory"));

            create.Should().Throw<Microsoft.DotNet.Cli.Utils.GracefulException>().WithMessage("no base directory");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.OSX)]
        public void ItUsesTheDarwinUserTemporaryDirectoryOnMacOS()
        {
            string baseDirectory = VSTestArtifactsDirectory.GetBaseDirectory();

            baseDirectory.Should().StartWith("/var/folders/");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.OSX)]
        public void ItDoesNotUseTmpdirOnMacOS()
        {
            // Proves GetBaseDirectory actually calls confstr rather than having silently fallen back to
            // Path.GetTempPath - which would also start with /var/folders/ in the common case, making the
            // previous test alone insufficient to catch that regression.
            string? previousTmpDir = Environment.GetEnvironmentVariable("TMPDIR");
            string distinctTmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                Directory.CreateDirectory(distinctTmpDir);
                Environment.SetEnvironmentVariable("TMPDIR", distinctTmpDir);

                VSTestArtifactsDirectory.GetBaseDirectory().Should().NotBe(distinctTmpDir);
            }
            finally
            {
                Environment.SetEnvironmentVariable("TMPDIR", previousTmpDir);
                Directory.Delete(distinctTmpDir, recursive: true);
            }
        }

        [TestMethod]
        public void ItCreatesTheDirectoryEmptyAndNotAsASymbolicLink()
        {
            var directory = new DirectoryInfo(VSTestArtifactsDirectory.GetOrCreate()!);

            directory.LinkTarget.Should().BeNull();
            directory.EnumerateFileSystemInfos().Should().BeEmpty();
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Linux)]
        public void ItCreatesTheDirectoryAccessibleOnlyToTheCurrentUserOnLinux()
        {
            new DirectoryInfo(VSTestArtifactsDirectory.GetOrCreate()!).UnixFileMode.Should().Be(OwnerOnlyMode);
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        [SupportedOSPlatform("windows")]
        public void ItCreatesTheDirectoryAccessibleOnlyToTheCurrentUserOnWindows()
        {
            var security = new DirectoryInfo(VSTestArtifactsDirectory.GetOrCreate()!).GetAccessControl(AccessControlSections.Access);
            var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>();

            security.AreAccessRulesProtected.Should().BeTrue("inherited rules - for example from a shared system temporary directory - must not apply");
            rules.Should().ContainSingle().Which.Should().Match<FileSystemAccessRule>(rule =>
                rule.AccessControlType == AccessControlType.Allow
                && rule.IdentityReference == WindowsIdentity.GetCurrent().User
                && (rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl);
        }

        [TestMethod]
        public void ItCreatesTheDirectoryOnlyOncePerInvocation()
        {
            string first = VSTestArtifactsDirectory.GetOrCreate()!;
            string second = VSTestArtifactsDirectory.GetOrCreate()!;

            second.Should().Be(first);
            VSTestArtifactsDirectory.Current.Should().Be(first);
        }

        [TestMethod]
        public void ItAlwaysProducesAnAbsolutePath()
        {
            Path.IsPathRooted(VSTestArtifactsDirectory.NewCandidatePath()).Should().BeTrue();
        }

        [TestMethod]
        public void ItProducesAnAbsolutePathEvenWhenTheBaseDirectoryOverrideIsRelative()
        {
            // vstest.console ignores a relative value, and the collecting and post-processing processes would
            // resolve it against different working directories anyway.
            string? previousOverride = Environment.GetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName);

            try
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, "relative-base-dir");

                string path = VSTestArtifactsDirectory.NewCandidatePath();

                Path.IsPathRooted(path).Should().BeTrue($"'{path}' should have been made absolute");
            }
            finally
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, previousOverride);
            }
        }

        [TestMethod]
        public void ItRunsWithoutAnArtifactsDirectoryWhenItCannotBeCreated()
        {
            // A read-only or full base directory, or a file system that cannot represent the requested
            // ownership, must not fail the test run: vstest.console then chooses the location itself and only
            // the post-processing the SDK would have driven is lost.
            string? previousOverride = Environment.GetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName);
            string blocker = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                // A file, not a directory, so nothing can be created beneath it.
                File.WriteAllText(blocker, string.Empty);
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, Path.Combine(blocker, "unusable"));
                VSTestArtifactsDirectory.Delete();

                VSTestArtifactsDirectory.GetOrCreate().Should().BeNull();

                var environment = new VSTestForwardingApp([]).GetProcessStartInfo().Environment;
                environment.Should().NotContainKey(VSTestArtifactsDirectory.EnvironmentVariableName);
            }
            finally
            {
                Environment.SetEnvironmentVariable(VSTestArtifactsDirectory.BaseDirectoryEnvironmentVariableName, previousOverride);
                VSTestArtifactsDirectory.Delete();
                File.Delete(blocker);
            }
        }

        [TestMethod]
        public void ItPassesTheDirectoryToVSTestConsole()
        {
            string path = VSTestArtifactsDirectory.GetOrCreate()!;

            var environment = new VSTestForwardingApp([]).GetProcessStartInfo().Environment;

            environment.Should().ContainKey(VSTestArtifactsDirectory.EnvironmentVariableName);
            environment[VSTestArtifactsDirectory.EnvironmentVariableName].Should().Be(path);
        }

        [TestMethod]
        public void ItPassesTheSameDirectoryToEveryVSTestConsole()
        {
            // The post-processing run has to resolve the same session subdirectory the collecting run wrote.
            string first = new VSTestForwardingApp([]).GetProcessStartInfo().Environment[VSTestArtifactsDirectory.EnvironmentVariableName]!;
            string second = new VSTestForwardingApp([]).GetProcessStartInfo().Environment[VSTestArtifactsDirectory.EnvironmentVariableName]!;

            first.Should().NotBeNullOrEmpty();
            second.Should().Be(first);
        }

        [TestMethod]
        public void ItDoesNotCreateAnArtifactsDirectoryForTheHelpForwardingPath()
        {
            VSTestArtifactsDirectory.Delete();

            var environment = new VSTestForwardingApp(["--help"], useArtifactsDirectory: false).GetProcessStartInfo().Environment;

            environment.Should().NotContainKey(VSTestArtifactsDirectory.EnvironmentVariableName);
            VSTestArtifactsDirectory.Current.Should().BeNull("forwarding --help must not create a directory");
        }

        [TestMethod]
        public void ItDoesNotOverrideTheTemporaryDirectoryOfVSTestConsole()
        {
            // The whole point of using a dedicated variable is that nothing else changes: TMPDIR in particular
            // still resolves the runtime diagnostics socket and everything else that goes through
            // Path.GetTempPath().
            var environment = new VSTestForwardingApp([]).GetProcessStartInfo().Environment;

            foreach (string name in new[] { "TMPDIR", "TMP", "TEMP" })
            {
                environment.TryGetValue(name, out string? value);
                value.Should().Be(Environment.GetEnvironmentVariable(name), $"'{name}' should be left untouched");
            }
        }

        [TestMethod]
        public void ItFlowsTheDirectoryThroughMSBuildWhenVSTestConsoleIsStartedByTheVSTestTask()
        {
            string path = VSTestArtifactsDirectory.GetOrCreate()!;

            var environment = CreateMSBuildTestCommand().GetProcessStartInfo().Environment;

            environment.Should().ContainKey(VSTestArtifactsDirectory.EnvironmentVariableName);
            environment[VSTestArtifactsDirectory.EnvironmentVariableName].Should().Be(path);
        }

        [TestMethod]
        public void ItDeletesTheDirectoryAndItsContents()
        {
            string path = VSTestArtifactsDirectory.GetOrCreate()!;
            Directory.CreateDirectory(Path.Combine(path, "session"));
            File.WriteAllText(Path.Combine(path, "session", "executionComplete.json"), "{}");

            VSTestArtifactsDirectory.Delete();

            Directory.Exists(path).Should().BeFalse();
            VSTestArtifactsDirectory.Current.Should().BeNull();
        }

        private static TestCommand CreateMSBuildTestCommand() =>
            TestCommand.FromArgs(["--disable-build-servers"], "<testSessionCorrelationId>", "<msbuildpath>");
    }
}
