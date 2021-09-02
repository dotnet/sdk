// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class SplitConfigurationTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;

        public SplitConfigurationTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        private static string TemplateJsonWithProperAdditionalConfigFilesString
        {
            get
            {
                string templateJsonString = @"
{
  ""author"": ""Microsoft"",
  ""classifications"": [""Common"", ""Console""],
  ""name"": ""Test Split Config Console App"",
  ""generatorVersions"": ""[1.0.0.0-*)"",
  ""groupIdentity"": ""Testing.Split.Config.Console"",
  ""identity"": ""Testing.Framework.Versioned.Console.CSharp"",
  ""shortName"": ""splitconfigtest"",
  ""sourceName"": ""Company.ConsoleApplication1"",
  ""preferNameDirectory"": true,
  ""additionalConfigFiles"": [
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
                return templateJsonString;
            }
        }

        private static string SymbolsTemplateJsonString
        {
            get
            {
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
                return symbolsTemplateJsonString;
            }
        }

        private static string TemplateJsonWithAdditionalFileOutsideBasePath
        {
            get
            {
                string templateJsonString = @"
{
  ""additionalConfigFiles"": [
    ""../../improper.template.json""
  ]
}
";
                return templateJsonString;
            }
        }

        [Fact(DisplayName = nameof(SplitConfigCantReferenceFileOutsideBasePath))]
        public void SplitConfigCantReferenceFileOutsideBasePath()
        {
            string sourcePath = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);
            TestTemplateSetup setup = SetupSplitConfigWithAFileOutsideMountPoint(_engineEnvironmentSettings, sourcePath);

            IGenerator generator = new RunnableProjectGenerator();
            IFileSystemInfo templateConfigFileInfo = setup.InfoForSourceFile(TemplateConfigTestHelpers.DefaultConfigRelativePath);
            bool result = generator.TryGetTemplateFromConfigInfo(templateConfigFileInfo, out ITemplate template, null, null);
            Assert.False(result, "Template config should not be readable - additional file is outside the base path.");
            Assert.Null(template);
        }

        [Fact(DisplayName = nameof(SplitConfigReadFailsIfAReferencedFileIsMissing))]
        public void SplitConfigReadFailsIfAReferencedFileIsMissing()
        {
            string sourcePath = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);
            TestTemplateSetup setup = SetupSplitConfigWithAMissingReferencedFile(_engineEnvironmentSettings, sourcePath);
            IGenerator generator = new RunnableProjectGenerator();

            IFileSystemInfo templateConfigFileInfo = setup.InfoForSourceFile(TemplateConfigTestHelpers.DefaultConfigRelativePath);
            bool result = generator.TryGetTemplateFromConfigInfo(templateConfigFileInfo, out ITemplate template, null, null);
            Assert.False(result, "Template config should not be readable - missing additional file.");
            Assert.Null(template);
        }

        [Fact(DisplayName = nameof(SplitConfigTest))]
        public void SplitConfigTest()
        {
            string sourcePath = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);
            TestTemplateSetup setup = SetupSplitConfigTestTemplate(_engineEnvironmentSettings, sourcePath);

            IGenerator generator = new RunnableProjectGenerator();
            IFileSystemInfo templateConfigFileInfo = setup.InfoForSourceFile("templateSource/.template.config/template.json");
            generator.TryGetTemplateFromConfigInfo(templateConfigFileInfo, out ITemplate template, null, null);

            IDictionary<string, ITemplateParameter> parameters = template.Parameters.ToDictionary(p => p.Name, p => p);
            Assert.Equal(5, parameters.Count);  // 5 in the configs + 1 for 'name' (implicit)
            Assert.True(parameters.ContainsKey("type"));
            Assert.True(parameters.ContainsKey("language"));
            Assert.True(parameters.ContainsKey("RuntimeFrameworkVersion"));
            Assert.True(parameters.ContainsKey("Framework"));
            Assert.True(parameters.ContainsKey("MyThing"));
        }

        // The file outside the proper location is not created - it can't be by this mechanism.
        // It doesn't need to exist, the reader will fail in trying to read it.
        private static TestTemplateSetup SetupSplitConfigWithAFileOutsideMountPoint(IEngineEnvironmentSettings environment, string basePath)
        {
            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            templateSourceFiles.Add(".template.config/template.json", TemplateJsonWithAdditionalFileOutsideBasePath);
            TestTemplateSetup setup = new TestTemplateSetup(environment, basePath, templateSourceFiles);
            setup.WriteSource();
            return setup;
        }

        // Uses the same template.json as the test that successfully reads a split config.
        // But doesn't create the additional file
        private static TestTemplateSetup SetupSplitConfigWithAMissingReferencedFile(IEngineEnvironmentSettings environment, string basePath)
        {
            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            templateSourceFiles.Add(".template.config/template.json", TemplateJsonWithProperAdditionalConfigFilesString);
            TestTemplateSetup setup = new TestTemplateSetup(environment, basePath, templateSourceFiles);
            setup.WriteSource();
            return setup;
        }

        private static TestTemplateSetup SetupSplitConfigTestTemplate(IEngineEnvironmentSettings environment, string basePath)
        {
            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            templateSourceFiles.Add("templateSource/.template.config/template.json", TemplateJsonWithProperAdditionalConfigFilesString);
            templateSourceFiles.Add("templateSource/.template.config/symbols.template.json", SymbolsTemplateJsonString);
            TestTemplateSetup setup = new TestTemplateSetup(environment, basePath, templateSourceFiles);
            setup.WriteSource();
            return setup;
        }
    }
}
