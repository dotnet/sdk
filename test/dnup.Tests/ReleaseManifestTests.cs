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
    }
}
