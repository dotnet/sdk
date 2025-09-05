using System;
using Xunit;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dnup.Tests
{
    public class ReleaseManifestTests
    {
        [Fact]
        public void GetLatestVersionForChannel_MajorOnly_ReturnsLatestVersion()
        {
            var manifest = new ReleaseManifest();
            var version = manifest.GetLatestVersionForChannel("9", InstallMode.SDK);
            Assert.True(!string.IsNullOrEmpty(version));
        }

        [Fact]
        public void GetLatestVersionForChannel_MajorMinor_ReturnsLatestVersion()
        {
            var manifest = new ReleaseManifest();
            var version = manifest.GetLatestVersionForChannel("9.0", InstallMode.SDK);
            Assert.False(string.IsNullOrEmpty(version));
            Assert.StartsWith("9.0.", version);
        }

        [Fact]
        public void GetLatestVersionForChannel_FeatureBand_ReturnsLatestVersion()
        {
            var manifest = new ReleaseManifest();

            var version = manifest.GetLatestVersionForChannel("9.0.1xx", InstallMode.SDK);
            Console.WriteLine($"Version found: {version ?? "null"}");

            // Feature band version should be returned in the format 9.0.100
            Assert.True(!string.IsNullOrEmpty(version));
            Assert.Matches(@"^9\.0\.1\d{2}$", version);
        }

        [Fact]
        public void GetLatestVersionForChannel_LTS_ReturnsLatestLTSVersion()
        {
            var manifest = new ReleaseManifest();
            var version = manifest.GetLatestVersionForChannel("lts", InstallMode.SDK);

            Console.WriteLine($"LTS Version found: {version ?? "null"}");

            // Check that we got a version
            Assert.False(string.IsNullOrEmpty(version));

            // LTS versions should have even minor versions (e.g., 6.0, 8.0, 10.0)
            var versionParts = version.Split('.');
            Assert.True(versionParts.Length >= 2, "Version should have at least major.minor parts");

            int minorVersion = int.Parse(versionParts[1]);
            Assert.True(minorVersion % 2 == 0, $"LTS version {version} should have an even minor version");

            // Should not be a preview version
            Assert.DoesNotContain("-", version);
        }

        [Fact]
        public void GetLatestVersionForChannel_STS_ReturnsLatestSTSVersion()
        {
            var manifest = new ReleaseManifest();
            var version = manifest.GetLatestVersionForChannel("sts", InstallMode.SDK);

            Console.WriteLine($"STS Version found: {version ?? "null"}");

            // Check that we got a version
            Assert.False(string.IsNullOrEmpty(version));

            // STS versions should have odd minor versions (e.g., 7.0, 9.0, 11.0)
            var versionParts = version.Split('.');
            Assert.True(versionParts.Length >= 2, "Version should have at least major.minor parts");

            int minorVersion = int.Parse(versionParts[1]);
            Assert.True(minorVersion % 2 != 0, $"STS version {version} should have an odd minor version");

            // Should not be a preview version
            Assert.DoesNotContain("-", version);
        }
    }
}
