using FluentAssertions;
using Xunit;
using Microsoft.DotNet.Cli.Build;

namespace EndToEnd
{
    public class GenerateDefaultRuntimeFrameworkVersionTests
    {
        [Theory]
        [InlineData("3.0.0-rtm", "3.0.0-rtm")]
        [InlineData("3.1.0", "3.1.0")]
        [InlineData("10.3.10", "10.3.0")]
        [InlineData("1.1.10-prerelease", "1.1.0")]
        public void ItGeneratesDefaultVersionBasedOnRuntimePackVersion(string runtimePackVersion, string defaultRuntimeFrameworkVersion)
        {
            var generateTask = new GenerateDefaultRuntimeFrameworkVersion()
            {
                RuntimePackVersion = runtimePackVersion
            };

            generateTask
                .Execute()
                .Should().BeTrue();

            generateTask.DefaultRuntimeFrameworkVersion.Should().Be(defaultRuntimeFrameworkVersion);
        }
    }
}
