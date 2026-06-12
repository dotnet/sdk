using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests
{
    public class CalculateTemplateVersionsTests(ITestOutputHelper log) : SdkTest(log)
    {
        [Fact]
        public void WhenAspNetCoreTemplateMajorVersionLowerthan3ItCanCalculateTemplateVersionsInStableBuilds()
        {
            var result = CalculateTemplateVersions.Calculate("3.1.0");

            // The patch is 1 higher than aspnetTemplateVersion due to https://github.com/dotnet/core-sdk/issues/6243
            result.InstallPath.Should().Be("3.1.1");
            result.MajorMinorVersion.Should().Be("3.1");
            result.MajorMinorPatchVersion.Should().Be("3.1.1");
        }

        [Fact]
        public void WhenAspNetCoreTemplateMajorVersionLowerthan3ItCanCalculateTemplateVersionsInNonStableBuilds()
        {
            var result = CalculateTemplateVersions.Calculate("3.0.0-alpha.1.20071.6");

            result.InstallPath.Should().Be("3.0.1-alpha.1.20071.6");
            result.MajorMinorVersion.Should().Be("3.0");
            result.MajorMinorPatchVersion.Should().Be("3.0.1");
        }

        [Fact]
        public void WhenAspNetCoreTemplateMajorVersionHigherthan3ItCanCalculateTemplateVersionsInStableBuilds()
        {
            var result = CalculateTemplateVersions.Calculate("5.1.0");

            // The patch align with AspNetCoreTemplateMajorVersion again, since there is no non-deterministic existing ComponentId under Major version 5.
            result.InstallPath.Should().Be("5.1.0");
            result.MajorMinorVersion.Should().Be("5.1");
            result.MajorMinorPatchVersion.Should().Be("5.1.0");
        }

        [Fact]
        public void WhenAspNetCoreTemplateMajorVersionHigherthan3ItCanCalculateTemplateVersionsInNonStableBuilds()
        {
            var result = CalculateTemplateVersions.Calculate("5.0.0-alpha.1.20071.6");

            result.InstallPath.Should().Be("5.0.0-alpha.1.20071.6");
            result.MajorMinorVersion.Should().Be("5.0");
            result.MajorMinorPatchVersion.Should().Be("5.0.0");
        }
    }
}
