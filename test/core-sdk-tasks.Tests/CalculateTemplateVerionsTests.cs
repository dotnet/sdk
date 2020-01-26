using FluentAssertions;
using Xunit;
using Microsoft.DotNet.Cli.Build;

namespace EndToEnd
{
    public class CalculateTemplateVersionsTests
    {
        [Fact]
        public void ItCanCalculateTemplateVersionsInStableBuilds()
        {
            var result = CalculateTemplateVersions.Calculate("3.1.0", "014885", "dev");
            result.BundledTemplateInstallPath.Should().Be("3.1.1",
                "the patch is 1 higher than aspnetTemplateVersion due to  https://github.com/dotnet/core-sdk/issues/6243");
            result.BundledTemplateMsiVersion.Should().Be("3.1.1.014885");
            result.BundledTemplateMajorMinorVersion.Should().Be("3.1");
        }

        [Fact]
        public void ItCanCalculateTemplateVersionsInNonStableBuilds()
        {
            var result = CalculateTemplateVersions.Calculate("5.0.0-alpha.1.20071.6", "014885", "dev");
            result.BundledTemplateInstallPath.Should().Be("5.0.1-dev");
            result.BundledTemplateMsiVersion.Should().Be("5.0.1.014885");
            result.BundledTemplateMajorMinorVersion.Should().Be("5.0");
        }
    }
}
