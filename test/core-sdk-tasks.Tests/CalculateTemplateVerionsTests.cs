using FluentAssertions;
using Xunit;
using Microsoft.DotNet.Cli.Build;

namespace EndToEnd
{
    public class CalculateTemplateVersionsTests
    {
        [Fact]
        public void WhenAspNetCoreTemplateMajorVersionLowerthan3ItCanCalculateTemplateVersionsInStableBuilds()
        {
            var result = CalculateTemplateVersions.Calculate("3.1.0");

            result.Should()
                .Be(("3.1.1", "3.1", "3.1.1"),
                    "the patch is 1 higher than aspnetTemplateVersion " +
                    "due to https://github.com/dotnet/core-sdk/issues/6243");
        }

        [Fact]
        public void WhenAspNetCoreTemplateMajorVersionLowerthan3ItCanCalculateTemplateVersionsInNonStableBuilds()
        {
            var result = CalculateTemplateVersions.Calculate("3.0.0-alpha.1.20071.6");

            result.Should()
                .Be(("3.0.1-alpha.1.20071.6", "3.0", "3.0.1"));
        }

        [Fact]
        public void WhenAspNetCoreTemplateMajorVersionHigherthan3ItCanCalculateTemplateVersionsInStableBuilds()
        {
            var result = CalculateTemplateVersions.Calculate("5.1.0");

            result.Should()
                .Be(("5.1.0", "5.1", "5.1.0"),
                     "the patch align with AspNetCoreTemplateMajorVersion again, " +
                     "since there is no non-deterministic existing ComponentId under Major version 5.");
        }

        [Fact]
        public void WhenAspNetCoreTemplateMajorVersionHigherthan3ItCanCalculateTemplateVersionsInNonStableBuilds()
        {
            var result = CalculateTemplateVersions.Calculate("5.0.0-alpha.1.20071.6");

            result.Should()
                .Be(("5.0.0-alpha.1.20071.6", "5.0", "5.0.0"));
        }
    }
}
