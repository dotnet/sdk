using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateEngine.Edge.TemplateUpdates;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using System.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class InstallUnitDescriptorFactoryTests : TestBase
    {
        [Fact(DisplayName = nameof(InstallUnitDescriptorFactoryTryParseFailsGracefullyOnNullDescriptorObjectTest))]
        public void InstallUnitDescriptorFactoryTryParseFailsGracefullyOnNullDescriptorObjectTest()
        {
            Assert.False(InstallUnitDescriptorFactory.TryParse(EngineEnvironmentSettings, null, out IInstallUnitDescriptor descriptor));
        }

        [Fact(DisplayName = nameof(InstallUnitDescriptorFactoryFailsGracefullyOnUnknownDescriptorFactoryIdTest))]
        public void InstallUnitDescriptorFactoryFailsGracefullyOnUnknownDescriptorFactoryIdTest()
        {
            // guid was randomly generated, doesn't match any descriptor factory
            string serializedDescriptor = @"
{
  ""2DB9FBE1-5DFA-4A56-A9B7-8291236D7580"": 
    {
        ""FactoryId"": ""25AB3648-DC67-4A95-A658-5EEE8ADC2695"",
        ""Details"": {
        }
    }
}";
            JObject descriptorObject = JObject.Parse(serializedDescriptor);
            JProperty descriptorProperty = descriptorObject.Properties().First();

            Assert.False(InstallUnitDescriptorFactory.TryParse(EngineEnvironmentSettings, descriptorProperty, out IInstallUnitDescriptor descriptor));
        }

        [Fact(DisplayName = nameof(InstallUnitDescriptorFactoryFailsGracefullyOnMissingFactoryIdTest))]
        public void InstallUnitDescriptorFactoryFailsGracefullyOnMissingFactoryIdTest()
        {
            string serializedDescriptor = @"
{
    ""Details"": {
    }
}";
            JObject descriptorObject = JObject.Parse(serializedDescriptor);
            JProperty descriptorProperty = descriptorObject.Properties().First();
            Assert.False(InstallUnitDescriptorFactory.TryParse(EngineEnvironmentSettings, descriptorProperty, out IInstallUnitDescriptor descriptor));
        }

        [Fact(DisplayName = nameof(InstallUnitDescriptorFactoryFailsGracefullyOnMissingDetailsTest))]
        public void InstallUnitDescriptorFactoryFailsGracefullyOnMissingDetailsTest()
        {
            string serializedDescriptor = @"
{
    ""FactoryId"": ""25AB3648-DC67-4A95-A658-5EEE8ADC2695"",
}";

            JObject descriptorObject = JObject.Parse(serializedDescriptor);
            JProperty descriptorProperty = descriptorObject.Properties().First();
            Assert.False(InstallUnitDescriptorFactory.TryParse(EngineEnvironmentSettings, descriptorProperty, out IInstallUnitDescriptor descriptor));
        }

        [Fact(DisplayName = nameof(InstallUnitDescriptorFactoryFailsGracefullyOnStructuredDetailsDataTest))]
        public void InstallUnitDescriptorFactoryFailsGracefullyOnStructuredDetailsDataTest()
        {
            string serializedDescriptor = @"
{
    ""FactoryId"": ""25AB3648-DC67-4A95-A658-5EEE8ADC2695"",
    ""Details"": {
        ""OuterKey"" : {
            ""InnerKey"": ""InnerValue""
        }
    }
}";
            JObject descriptorObject = JObject.Parse(serializedDescriptor);
            JProperty descriptorProperty = descriptorObject.Properties().First();
            Assert.False(InstallUnitDescriptorFactory.TryParse(EngineEnvironmentSettings, descriptorProperty, out IInstallUnitDescriptor descriptor));
        }
    }
}
