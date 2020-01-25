using FluentAssertions;
using Xunit;
using Microsoft.DotNet.Cli.Build;

namespace EndToEnd
{
    public class CalculateTemplateVerionsTests
    {
        [Fact]
        public void ItCanCalculateTemplateVersionsInStableBuilds()
        {
            var result = CalculateTemplateVersions.Calculate("3.1.0", "014885", "dev");
            result.BundledTemplateInstallPath.Should().Be("3.1.0");
            result.BundledTemplateMsiVersion.Should().Be("3.1.0.014885");
            result.BundledTemplateMajorMinorVersion.Should().Be("3.1");
        }

        [Fact]
        public void ItCanCalculateTemplateVersionsInNonStableBuilds()
        {
            var result = CalculateTemplateVersions.Calculate("5.0.0-alpha.1.20071.6", "014885", "dev");
            result.BundledTemplateInstallPath.Should().Be("5.0.0-dev");
            result.BundledTemplateMsiVersion.Should().Be("5.0.0.014885");
            result.BundledTemplateMajorMinorVersion.Should().Be("5.0");
        }
    }
}
