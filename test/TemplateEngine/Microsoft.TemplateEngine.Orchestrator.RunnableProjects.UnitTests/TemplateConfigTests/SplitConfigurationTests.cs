using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge.Mount.FileSystem;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class SplitConfigurationTests : TestBase
    {
        // TODO: find a home for this within the tests
        private static readonly Guid FileSystemMountPointFactoryId = new Guid("8C19221B-DEA3-4250-86FE-2D4E189A11D2");

        [Fact(DisplayName = nameof(SplitConfigurationMergesCorrectlyTest))]
        public void SplitConfigurationMergesCorrectlyTest()
        {
            string templateJsonString = @"
{
  ""author"": ""Microsoft"",
  ""classifications"": [""Common"", ""Console""],
  ""name"": ""Test Split Config Console Application"",
  ""groupIdentity"": ""Testing.Split.Config.Console"",
  ""identity"": ""Testing.Framework.Versioned.Console.CSharp"",
  ""shortName"": ""splitconfigtest"",
  ""sourceName"": ""Company.ConsoleApplication1"",
  ""preferNameDirectory"": true,
  ""AdditionalConfigFiles"": [ 
    ""symbols.template.json""
  ],
  ""symbols"": {
    ""type"": {
                ""type"": ""parameter"",
      ""datatype"": ""choice"",
      ""choices"": [
        {
          ""choice"": ""project""
        }
      ]
    },
    ""language"": {
      ""type"": ""parameter"",
      ""datatype"": ""choice"",
      ""choices"": [
        {
          ""choice"": ""C#""
        }
      ]
    }
  }
}
";

            string symbolsTemplateJsonString = @"
{
  ""symbols"": {
    ""RuntimeFrameworkVersion"": {
                ""type"": ""parameter"",
      ""replaces"": ""2.0.0-beta-xyz""
    },
    ""Framework"": {
                ""type"": ""parameter"",
      ""datatype"": ""choice"",
      ""choices"": [
        {
          ""choice"": ""1.0"",
          ""description"": ""Target netcoreapp1.0""
        },
        {
          ""choice"": ""1.1"",
          ""description"": ""Target netcoreapp1.1""
        },
        {
          ""choice"": ""2.0"",
          ""description"": ""Target netcoreapp2.0 build specified by RuntimeFrameworkVersion""
        }
      ],
      ""defaultValue"": ""1.0""
    },
    ""MyThing"": {
      ""type"": ""parameter"",
      ""datatype"": ""choice"",
      ""choices"": [
        {
          ""choice"": ""foo""
        },
        {
          ""choice"": ""bar""
        },
        {
          ""choice"": ""baz""
        }
      ],
      ""defaultValue"": ""foo""
    }
  }
}
";


            ITemplateEngineHost host = EngineEnvironmentSettings.Host;
            string mountPointPlace = @"/temp/testMountPoint/";
            host.VirtualizeDirectory(mountPointPlace);
            host.FileSystem.CreateDirectory(mountPointPlace);

            MountPointInfo mountPointInfo = new MountPointInfo(Guid.Empty, FileSystemMountPointFactoryId, Guid.NewGuid(), mountPointPlace);
            if (!new FileSystemMountPointFactory().TryMount(EngineEnvironmentSettings, null, mountPointPlace, out IMountPoint mountPoint))
            {
                Assert.True(false, "couldn't create mount point");
            }

            string configDir = ".template.config";
            string configFilePlace = mountPointPlace.CombinePaths(configDir);
            host.FileSystem.CreateDirectory(configFilePlace);

            string templateJsonPath = configFilePlace.CombinePaths("template.json");
            string symbolsJsonPath = configFilePlace.CombinePaths("symbols.template.json");
            host.FileSystem.WriteAllText(templateJsonPath, templateJsonString);
            host.FileSystem.WriteAllText(symbolsJsonPath, symbolsTemplateJsonString);

            string reRead = host.FileSystem.ReadAllText(templateJsonPath);
            Assert.Equal(templateJsonString, reRead);

            string mountRelTemplateJsonPath = configDir.CombinePaths("template.json");
            IFileSystemInfo templateConfigFileInfo = mountPoint.FileSystemInfo(mountRelTemplateJsonPath);

            IFileSystemInfo infoTemplateConfigFileInfo = mountPoint.FileInfo(mountRelTemplateJsonPath);

            IGenerator generator = new RunnableProjectGenerator();
            generator.TryGetTemplateFromConfigInfo(templateConfigFileInfo, out ITemplate template, null, null);

            IDictionary<string, ITemplateParameter> parameters = template.Parameters.ToDictionary(p => p.Name, p => p);
            Assert.Equal(5, parameters.Count);
            Assert.True(parameters.ContainsKey("type"));
            Assert.True(parameters.ContainsKey("language"));
            Assert.True(parameters.ContainsKey("RuntimeFrameworkVersion"));
            Assert.True(parameters.ContainsKey("Framework"));
            Assert.True(parameters.ContainsKey("MyThing"));
        }
    }
}
