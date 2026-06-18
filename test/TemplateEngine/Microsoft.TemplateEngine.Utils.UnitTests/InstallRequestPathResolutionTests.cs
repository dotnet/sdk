// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    [TestClass]
    public class InstallRequestPathResolutionTests
    {
        // MSTest has no IClassFixture equivalent; a lazily-initialized static helper
        // mirrors the per-class lifetime that xUnit's IClassFixture provides.
        private static readonly Lazy<EnvironmentSettingsHelper> s_environmentSettingsHelper =
            new(() => new EnvironmentSettingsHelper(NullMessageSink.Instance));

        private IEngineEnvironmentSettings _engineEnvironmentSettings = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _engineEnvironmentSettings = s_environmentSettingsHelper.Value.CreateEnvironment(
                hostIdentifier: GetType().Name,
                virtualize: true);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (s_environmentSettingsHelper.IsValueCreated)
            {
                s_environmentSettingsHelper.Value.Dispose();
            }
        }

        [TestMethod]
        public void CanResolvePath()
        {
            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath(Directory.GetCurrentDirectory(), _engineEnvironmentSettings);
            Assert.AreEqual(Directory.GetCurrentDirectory(), installPath.Single());
        }

        [TestMethod]
        public void CanTrimTrailingSeparator()
        {
            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar, _engineEnvironmentSettings);
            Assert.AreEqual(Directory.GetCurrentDirectory(), installPath.Single());
        }

        [TestMethod]
        public void CanResolveCurrentPath()
        {
            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath(".", _engineEnvironmentSettings);
            Assert.AreEqual(Directory.GetCurrentDirectory(), installPath.Single());
        }

        [TestMethod]
        public void CanResolveParentPath()
        {
            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath("..", _engineEnvironmentSettings);
            Assert.AreEqual(Path.GetDirectoryName(Directory.GetCurrentDirectory()), installPath.Single());
        }

        [TestMethod]
        public void CanResolveSubdirectories()
        {
            var testRootDir = TestUtils.CreateTemporaryFolder();
            Directory.CreateDirectory(Path.Combine(testRootDir, "dir1"));
            Directory.CreateDirectory(Path.Combine(testRootDir, "dir2"));
            Directory.CreateDirectory(Path.Combine(testRootDir, "dir3"));

            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath(Path.Combine(testRootDir, "*"), _engineEnvironmentSettings);

            Assert.HasCount(3, installPath);
            Assert.Contains(Path.Combine(testRootDir, "dir1"), installPath);
        }

        [TestMethod]
        public void CanResolveMaskedSubdirectories()
        {
            var testRootDir = TestUtils.CreateTemporaryFolder();
            Directory.CreateDirectory(Path.Combine(testRootDir, "dir1"));
            Directory.CreateDirectory(Path.Combine(testRootDir, "dar2"));
            Directory.CreateDirectory(Path.Combine(testRootDir, "dir33"));

            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath(Path.Combine(testRootDir, "dir*"), _engineEnvironmentSettings);

            Assert.HasCount(2, installPath);
            Assert.Contains(Path.Combine(testRootDir, "dir1"), installPath);
            Assert.Contains(Path.Combine(testRootDir, "dir33"), installPath);
        }

        [TestMethod]
        public void CanResolveMaskedFiles()
        {
            var testRootDir = TestUtils.CreateTemporaryFolder();
            File.Create(Path.Combine(testRootDir, "1.nupkg"));
            File.Create(Path.Combine(testRootDir, "2.nupkg"));
            File.Create(Path.Combine(testRootDir, "3.txt"));

            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath(Path.Combine(testRootDir, "*.nupkg"), _engineEnvironmentSettings);

            Assert.HasCount(2, installPath);
            Assert.Contains(Path.Combine(testRootDir, "1.nupkg"), installPath);
            Assert.Contains(Path.Combine(testRootDir, "2.nupkg"), installPath);
        }

        [TestMethod]
        public void CannotResolveInvalidPath()
        {
            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath("|path|", _engineEnvironmentSettings);
            Assert.AreEqual("|path|", installPath.Single());
        }

        [TestMethod]
        public void CannotResolveNonExistingPath()
        {
            Assert.IsFalse(File.Exists("path"));
            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath("path", _engineEnvironmentSettings);
            Assert.AreEqual("path", installPath.Single());

            installPath = InstallRequestPathResolution.ExpandMaskedPath("path\\", _engineEnvironmentSettings);
            Assert.AreEqual("path\\", installPath.Single());
        }

        [TestMethod]
        public void CannotResolveMaskedPathInFolder()
        {
            var testRootDir = TestUtils.CreateTemporaryFolder();
            Directory.CreateDirectory(Path.Combine(testRootDir, "dir"));
            File.Create(Path.Combine(testRootDir, "dir", "1.nupkg"));
            File.Create(Path.Combine(testRootDir, "dir", "2.nupkg"));

            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath(Path.Combine(testRootDir, "*", "*.nupkg"), _engineEnvironmentSettings);
            Assert.AreEqual(Path.Combine(testRootDir, "*", "*.nupkg"), installPath.Single());
        }

        [TestMethod]
        public void CanResolveParentOfRootFolder()
        {
            string dir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C:\\" : "/";

            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath(dir + "..", _engineEnvironmentSettings);
            Assert.AreEqual(dir, installPath.Single());
        }
    }
}
