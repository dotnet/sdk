// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
#if !NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#endif
using System.Runtime.InteropServices;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    public class InstallRequestPathResolutionTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;

        public InstallRequestPathResolutionTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [Fact]
        public void CanResolvePath()
        {
            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath(Directory.GetCurrentDirectory(), _engineEnvironmentSettings);
            Assert.Equal(Directory.GetCurrentDirectory(), installPath.Single());
        }

        [Fact]
        public void CanTrimTrailingSeparator()
        {
            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar, _engineEnvironmentSettings);
            Assert.Equal(Directory.GetCurrentDirectory(), installPath.Single());
        }

        [Fact]
        public void CanResolveCurrentPath()
        {
            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath(".", _engineEnvironmentSettings);
            Assert.Equal(Directory.GetCurrentDirectory(), installPath.Single());
        }

        [Fact]
        public void CanResolveParentPath()
        {
            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath("..", _engineEnvironmentSettings);
            Assert.Equal(Path.GetDirectoryName(Directory.GetCurrentDirectory()), installPath.Single());
        }

        [Fact]
        public void CanResolveSubdirectories()
        {
            var testRootDir = TestUtils.CreateTemporaryFolder();
            Directory.CreateDirectory(Path.Combine(testRootDir, "dir1"));
            Directory.CreateDirectory(Path.Combine(testRootDir, "dir2"));
            Directory.CreateDirectory(Path.Combine(testRootDir, "dir3"));

            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath(Path.Combine(testRootDir, "*"), _engineEnvironmentSettings);

            Assert.Equal(3, installPath.Count());
            Assert.Contains(Path.Combine(testRootDir, "dir1"), installPath);
        }

        [Fact]
        public void CanResolveMaskedSubdirectories()
        {
            var testRootDir = TestUtils.CreateTemporaryFolder();
            Directory.CreateDirectory(Path.Combine(testRootDir, "dir1"));
            Directory.CreateDirectory(Path.Combine(testRootDir, "dar2"));
            Directory.CreateDirectory(Path.Combine(testRootDir, "dir33"));

            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath(Path.Combine(testRootDir, "dir*"), _engineEnvironmentSettings);

            Assert.Equal(2, installPath.Count());
            Assert.Contains(Path.Combine(testRootDir, "dir1"), installPath);
            Assert.Contains(Path.Combine(testRootDir, "dir33"), installPath);
        }

        [Fact]
        public void CanResolveMaskedFiles()
        {
            var testRootDir = TestUtils.CreateTemporaryFolder();
            File.Create(Path.Combine(testRootDir, "1.nupkg"));
            File.Create(Path.Combine(testRootDir, "2.nupkg"));
            File.Create(Path.Combine(testRootDir, "3.txt"));

            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath(Path.Combine(testRootDir, "*.nupkg"), _engineEnvironmentSettings);

            Assert.Equal(2, installPath.Count());
            Assert.Contains(Path.Combine(testRootDir, "1.nupkg"), installPath);
            Assert.Contains(Path.Combine(testRootDir, "2.nupkg"), installPath);
        }

        [Fact]
        public void CannotResolveInvalidPath()
        {
            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath("|path|", _engineEnvironmentSettings);
            Assert.Equal("|path|", installPath.Single());
        }

        [Fact]
        public void CannotResolveNonExistingPath()
        {
            Assert.False(File.Exists("path"));
            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath("path", _engineEnvironmentSettings);
            Assert.Equal("path", installPath.Single());

            installPath = InstallRequestPathResolution.ExpandMaskedPath("path\\", _engineEnvironmentSettings);
            Assert.Equal("path\\", installPath.Single());
        }

        [Fact]
        public void CannotResolveMaskedPathInFolder()
        {
            var testRootDir = TestUtils.CreateTemporaryFolder();
            Directory.CreateDirectory(Path.Combine(testRootDir, "dir"));
            File.Create(Path.Combine(testRootDir, "dir", "1.nupkg"));
            File.Create(Path.Combine(testRootDir, "dir", "2.nupkg"));

            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath(Path.Combine(testRootDir, "*", "*.nupkg"), _engineEnvironmentSettings);
            Assert.Equal(Path.Combine(testRootDir, "*", "*.nupkg"), installPath.Single());
        }

        [Fact]
        public void CanResolveParentOfRootFolder()
        {
            string dir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C:\\" : "/";

            IEnumerable<string> installPath = InstallRequestPathResolution.ExpandMaskedPath(dir + "..", _engineEnvironmentSettings);
            Assert.Equal(dir, installPath.Single());
        }
    }
}
