// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.MSBuildSdkResolver;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    [TestClass]
    public class GivenThatWeWantToParseFXVersions
    {
        [TestMethod]
        [DataRow("")]
        [DataRow("1")]
        [DataRow("1.1")]
        [DataRow("A.1.1")]
        [DataRow("1.A.1")]
        [DataRow("1.1.A")]
        [DataRow("1A.1.1")]
        [DataRow("1.1A.1")]
        [DataRow("1.1.1A")]
        [DataRow("1.1.1-")]
        [DataRow("1.1.1-.")]
        [DataRow("1.1.1-A.")]
        [DataRow("1.1.1-A.B.")]
        [DataRow("1.1.1-.+id")]
        [DataRow("1.1.1-A.+id")]
        [DataRow("1.1.1-A.B.+id")]
        [DataRow("1.1.1-A.B+id.")]
        [DataRow("01.1.1")]
        [DataRow("1.01.1")]
        [DataRow("1.1.01")]
        [DataRow("1.1.1-01.B")]
        [DataRow("1.1.1-A.01")]
        [DataRow("00.1.1")]
        [DataRow("1.00.1")]
        [DataRow("1.1.00")]
        [DataRow("1.1.1-00.B")]
        [DataRow("1.1.1-A.00")]
        [DataRow("1.1.1+")]
        [DataRow("1.1.1-A+")]
        [DataRow("1.1.1-A*B")]
        [DataRow("1.1.1-A/B")]
        [DataRow("1.1.1-A:B")]
        [DataRow("1.1.1-A^B")]
        [DataRow("1.1.1-A|B")]
        public void ReturnsFalseGivenInvalidVersion(string s1)
        {
            FXVersion fxVersion;
            FXVersion.TryParse(s1, out fxVersion).Should().BeFalse();
        }

        [TestMethod]
        [DataRow("1.0.0-0.3.7", 1, 0, 0, "-0.3.7", "")]
        [DataRow("1.0.0-alpha", 1, 0, 0, "-alpha", "")]
        [DataRow("1.0.0-alpha+001", 1, 0, 0, "-alpha", "+001")]
        [DataRow("1.0.0-alpha.1", 1, 0, 0, "-alpha.1", "")]
        [DataRow("1.0.0-alpha.beta", 1, 0, 0, "-alpha.beta", "")]
        [DataRow("1.0.0-beta", 1, 0, 0, "-beta", "")]
        [DataRow("1.0.0-beta+exp.sha.5114f85", 1, 0, 0, "-beta", "+exp.sha.5114f85")]
        [DataRow("1.0.0-beta.2", 1, 0, 0, "-beta.2", "")]
        [DataRow("1.0.0-beta.11", 1, 0, 0, "-beta.11", "")]
        [DataRow("1.0.0-rc.1", 1, 0, 0, "-rc.1", "")]
        [DataRow("1.0.0-x.7.z.92", 1, 0, 0, "-x.7.z.92", "")]
        [DataRow("1.0.0", 1, 0, 0, "", "")]
        [DataRow("1.0.0+20130313144700", 1, 0, 0, "", "+20130313144700")]
        [DataRow("1.9.0-9", 1, 9, 0, "-9", "")]
        [DataRow("1.9.0-10", 1, 9, 0, "-10", "")]
        [DataRow("1.9.0-1A", 1, 9, 0, "-1A", "")]
        [DataRow("1.9.0", 1, 9, 0, "", "")]
        [DataRow("1.10.0", 1, 10, 0, "", "")]
        [DataRow("1.11.0", 1, 11, 0, "", "")]
        [DataRow("2.0.0", 2, 0, 0, "", "")]
        [DataRow("2.1.0", 2, 1, 0, "", "")]
        [DataRow("2.1.1", 2, 1, 1, "", "")]
        [DataRow("4.6.0-preview.19064.1", 4, 6, 0, "-preview.19064.1", "")]
        [DataRow("4.6.0-preview1-27018-01", 4, 6, 0, "-preview1-27018-01", "")]
        public void ReturnsCorrectFXVersion(string s1, int major, int minor, int patch, string pre, string build)
        {
            FXVersion fxVersion;

            var result = FXVersion.TryParse(s1, out fxVersion);

            result.Should().BeTrue();
            fxVersion.Major.Should().Be(major);
            fxVersion.Minor.Should().Be(minor);
            fxVersion.Patch.Should().Be(patch);
            fxVersion.Pre.Should().Be(pre);
            fxVersion.Build.Should().Be(build);
        }

        [TestMethod]
        public void ReturnsNullWhenNoMajorSeparatorIsFound()
        {
            FXVersion fxVersion;
            FXVersion.TryParse("1", out fxVersion).Should().BeFalse();
        }

        [TestMethod]
        public void ReturnsNullWhenMajorPortionIsNotANumber()
        {
            FXVersion fxVersion;
            FXVersion.TryParse("a.0.0", out fxVersion).Should().BeFalse();
        }

        [TestMethod]
        public void ReturnsNullWhenNoMinorSeparatorIsFound()
        {
            FXVersion fxVersion;
            FXVersion.TryParse("1.0", out fxVersion).Should().BeFalse();
        }

        [TestMethod]
        public void ReturnsNullWhenMinorPortionIsNotANumber()
        {
            FXVersion fxVersion;
            FXVersion.TryParse("1.a.0", out fxVersion).Should().BeFalse();
        }

        [TestMethod]
        public void ReturnsNullWhenPatchPortionIsNotANumber()
        {
            FXVersion fxVersion;
            FXVersion.TryParse("1.0.a", out fxVersion).Should().BeFalse();
        }

        [TestMethod]
        public void ReturnsFXVersionWhenOnlyMajorMinorPatchIsFound()
        {
            FXVersion fxVersion;

            var result = FXVersion.TryParse("1.2.3", out fxVersion);

            result.Should().BeTrue();
            fxVersion.Major.Should().Be(1);
            fxVersion.Minor.Should().Be(2);
            fxVersion.Patch.Should().Be(3);
        }

        [TestMethod]
        public void ReturnsFXVersionWhenOnlyMajorMinorPatchAndPreIsFound()
        {
            FXVersion fxVersion;

            var result = FXVersion.TryParse("1.2.3-pre", out fxVersion);

            result.Should().BeTrue();
            fxVersion.Major.Should().Be(1);
            fxVersion.Minor.Should().Be(2);
            fxVersion.Patch.Should().Be(3);
            fxVersion.Pre.Should().Be("-pre");
        }

        [TestMethod]
        public void ReturnsFXVersionWhenMajorMinorPatchAndPreAndBuildIsFound()
        {
            FXVersion fxVersion;

            var result = FXVersion.TryParse("1.2.3-pre+build", out fxVersion);

            result.Should().BeTrue();
            fxVersion.Major.Should().Be(1);
            fxVersion.Minor.Should().Be(2);
            fxVersion.Patch.Should().Be(3);
            fxVersion.Pre.Should().Be("-pre");
            fxVersion.Build.Should().Be("+build");
        }

    }
}
