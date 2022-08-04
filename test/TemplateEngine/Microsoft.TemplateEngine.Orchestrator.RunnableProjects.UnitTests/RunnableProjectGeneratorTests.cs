// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests
{
    public class RunnableProjectGeneratorTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private EnvironmentSettingsHelper _environmentSettingsHelper;

        public RunnableProjectGeneratorTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        [Fact]
        public async void CreateAsyncTest_GuidsMacroProcessingCaseSensitivity()
        {
            //
            // Template content preparation
            //

            Guid inputTestGuid = new Guid("12aa8f4e-a4aa-4ac1-927c-94cb99485ef1");
            string contentFileNamePrefix = "content - ";
            SimpleConfigModel config = new SimpleConfigModel()
            {
                Identity = "test",
                Guids = new List<Guid>()
                {
                    inputTestGuid
                }
            };

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>();
            // template.json
            templateSourceFiles.Add(TestFileSystemHelper.DefaultConfigRelativePath, config.ToJObject().ToString());

            //content
            foreach (string guidFormat in GuidMacroConfig.DefaultFormats.Select(c => c.ToString()))
            {
                templateSourceFiles.Add(contentFileNamePrefix + guidFormat, inputTestGuid.ToString(guidFormat));
            }

            //
            // Dependencies preparation and mounting
            //

            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(environment);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();

            TestFileSystemHelper.WriteTemplateSource(environment, sourceBasePath, templateSourceFiles);
            IMountPoint? sourceMountPoint = TestFileSystemHelper.CreateMountPoint(environment, sourceBasePath);
            RunnableProjectConfig runnableConfig = new RunnableProjectConfig(environment, rpg, config, sourceMountPoint.FileInfo(TestFileSystemHelper.DefaultConfigRelativePath));
            ParameterSetData parametersData = new ParameterSetData(runnableConfig);
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await rpg.CreateAsync(environment, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Veryfying the outputs
            //

            Guid expectedResultGuid = Guid.Empty;
            foreach (string guidFormat in GuidMacroConfig.DefaultFormats.Select(c => c.ToString()))
            {
                string resultContent = environment.Host.FileSystem.ReadAllText(Path.Combine(targetDir, contentFileNamePrefix + guidFormat));
                Guid resultGuid;
                Assert.True(
                    Guid.TryParseExact(resultContent, guidFormat, out resultGuid),
                    $"Expected the result conent ({resultContent}) to be parseable by Guid format '{guidFormat}'");

                if (expectedResultGuid == Guid.Empty)
                {
                    expectedResultGuid = resultGuid;
                }
                else
                {
                    Assert.Equal(expectedResultGuid, resultGuid);
                }
            }
            Assert.NotEqual(inputTestGuid, expectedResultGuid);
        }

        private const string TemplateConfigQuotelessLiteralsNotEnabled = @"
{
    ""identity"": ""test.template"",
    ""symbols"": {
	    ""ChoiceParam"": {
	      ""type"": ""parameter"",
	      ""description"": ""sample switch"",
	      ""datatype"": ""choice"",
	      ""choices"": [
		    {
		      ""choice"": ""FirstChoice"",
		      ""description"": ""First Sample Choice""
		    },
		    {
		      ""choice"": ""SecondChoice"",
		      ""description"": ""Second Sample Choice""
		    },
		    {
		      ""choice"": ""ThirdChoice"",
		      ""description"": ""Third Sample Choice""
		    }
	      ],
          ""defaultValue"": ""ThirdChoice"",
	    }
    }
}
";

        private const string TemplateConfigQuotelessLiteralsEnabled = @"
{
    ""identity"": ""test.template"",
    ""symbols"": {
	    ""ChoiceParam"": {
	      ""type"": ""parameter"",
	      ""description"": ""sample switch"",
	      ""datatype"": ""choice"",
          ""enableQuotelessLiterals"": true,
	      ""choices"": [
		    {
		      ""choice"": ""FirstChoice"",
		      ""description"": ""First Sample Choice""
		    },
		    {
		      ""choice"": ""SecondChoice"",
		      ""description"": ""Second Sample Choice""
		    },
		    {
		      ""choice"": ""ThirdChoice"",
		      ""description"": ""Third Sample Choice""
		    }
	      ],
          ""defaultValue"": ""ThirdChoice"",
	    }
    }
}
";

        [Theory]
        [InlineData(TemplateConfigQuotelessLiteralsNotEnabled, "UNKNOWN")]
        [InlineData(TemplateConfigQuotelessLiteralsEnabled, "SECOND")]
        public async void CreateAsyncTest_ConditionWithUnquotedChoiceLiteral(string templateConfig, string expectedResult)
        {
            //
            // Template content preparation
            //

            string sourceSnippet = @"
//#if( ChoiceParam == FirstChoice )
FIRST
//#elseif (ChoiceParam == SecondChoice )
SECOND
//#elseif (ChoiceParam == ThirdChoice )
THIRD
//#else
UNKNOWN
//#endif
";

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>();
            // template.json
            templateSourceFiles.Add(TestFileSystemHelper.DefaultConfigRelativePath, templateConfig);

            //content
            templateSourceFiles.Add("sourcFile", sourceSnippet);

            //
            // Dependencies preparation and mounting
            //

            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(environment);

            TestFileSystemHelper.WriteTemplateSource(environment, sourceBasePath, templateSourceFiles);
            IMountPoint? sourceMountPoint = TestFileSystemHelper.CreateMountPoint(environment, sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            SimpleConfigModel configModel = SimpleConfigModel.FromJObject(JObject.Parse(templateConfig));
            RunnableProjectConfig runnableConfig = new RunnableProjectConfig(environment, rpg, configModel, sourceMountPoint.FileInfo(TestFileSystemHelper.DefaultConfigRelativePath));
            ParameterSetData parametersData = new ParameterSetData(
                runnableConfig,
                new Dictionary<string, string?>() { { "ChoiceParam", "SecondChoice" } });
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await rpg.CreateAsync(environment, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Veryfying the outputs
            //

            string resultContent = environment.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourcFile")).Trim();
            Assert.Equal(expectedResult, resultContent);
        }

        [Fact]
        public async void CreateAsyncTest_MultiChoiceParamReplacingAndCondition()
        {
            //
            // Template content preparation
            //

            string templateConfig = @"
{
    ""identity"": ""test.template"",
    ""symbols"": {	
	    ""ChoiceParam"": {
	      ""type"": ""parameter"",
	      ""description"": ""sample switch"",
	      ""datatype"": ""choice"",
          ""allowMultipleValues"": true,
          ""enableQuotelessLiterals"": true,
	      ""choices"": [
		    {
		      ""choice"": ""FirstChoice"",
		      ""description"": ""First Sample Choice""
		    },
		    {
		      ""choice"": ""SecondChoice"",
		      ""description"": ""Second Sample Choice""
		    },
		    {
		      ""choice"": ""ThirdChoice"",
		      ""description"": ""Third Sample Choice""
		    }
	      ],
          ""defaultValue"": ""ThirdChoice"",
          ""replaces"": ""REPLACE_VALUE""
        }
    }
}
";

            string sourceSnippet = @"
MultiChoiceValue: REPLACE_VALUE
//#if( ChoiceParam == FirstChoice )
FIRST
//#endif
//#if (ChoiceParam == SecondChoice )
SECOND
//#endif
//#if (ChoiceParam == ThirdChoice )
THIRD
//#endif
";

            string expectedSnippet = @"
MultiChoiceValue: SecondChoice|ThirdChoice
SECOND
THIRD
";

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>();
            // template.json
            templateSourceFiles.Add(TestFileSystemHelper.DefaultConfigRelativePath, templateConfig);

            //content
            templateSourceFiles.Add("sourcFile", sourceSnippet);

            //
            // Dependencies preparation and mounting
            //

            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(environment);

            TestFileSystemHelper.WriteTemplateSource(environment, sourceBasePath, templateSourceFiles);
            IMountPoint? sourceMountPoint = TestFileSystemHelper.CreateMountPoint(environment, sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            SimpleConfigModel configModel = SimpleConfigModel.FromJObject(JObject.Parse(templateConfig));
            RunnableProjectConfig runnableConfig = new RunnableProjectConfig(environment, rpg, configModel, sourceMountPoint.FileInfo(TestFileSystemHelper.DefaultConfigRelativePath));
            ParameterSetData parametersData = new ParameterSetData(
                runnableConfig,
                new Dictionary<string, object?>() { { "ChoiceParam", new MultiValueParameter(new[] { "SecondChoice", "ThirdChoice" }) } });
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await rpg.CreateAsync(environment, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Veryfying the outputs
            //

            string resultContent = environment.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourcFile"));
            Assert.Equal(expectedSnippet, resultContent);
        }

        [Fact]
        public async void CreateAsyncTest_MultiChoiceParamAndConditionMacro()
        {
            //
            // Template content preparation
            //

            string templateConfig = @"
{
  ""$schema"": ""https://json.schemastore.org/template.json"",
  ""author"": ""Test Asset"",
  ""classifications"": [
    ""Test Asset""
  ],
  ""name"": ""MultiSelect.Template"",
  ""generatorVersions"": ""[1.0.0.0-*)"",
  ""tags"": {
    ""type"": ""project"",
    ""language"": ""C#""
  },
  ""groupIdentity"": ""MultiSelect.Template"",
  ""precedence"": ""100"",
  ""identity"": ""MultiSelect.Template"",
  ""shortName"": ""MultiSelect.Template"",
  ""sourceName"": ""bar"",
  ""symbols"": {
    ""Platform"": {
      ""type"": ""parameter"",
      ""description"": ""The target platform for the project."",
      ""datatype"": ""choice"",
      ""allowMultipleValues"": true,
      ""enableQuotelessLiterals"": true,
      ""choices"": [
        {
          ""choice"": ""Windows"",
          ""description"": ""Windows Desktop""
        },
        {
          ""choice"": ""WindowsPhone"",
          ""description"": ""Windows Phone""
        },
        {
          ""choice"": ""MacOS"",
          ""description"": ""Macintosh computers""
        },
        {
          ""choice"": ""iOS"",
          ""description"": ""iOS mobile""
        },
        {
          ""choice"": ""android"",
          ""description"": ""android mobile""
        },
        {
          ""choice"": ""nix"",
          ""description"": ""Linux distributions""
        }
      ],
      ""defaultValue"": ""MacOS|iOS""
    },
    ""IsMobile"": {
      ""type"": ""computed"",
      ""value"": ""((Platform == android || Platform == iOS || Platform == WindowsPhone) && Platform != Windows && Platform != MacOS && Platform != nix)""
    },
    ""IsAndroidOnly"": {
      ""type"": ""computed"",
      ""value"": ""(Platform == android && Platform != iOS && Platform != WindowsPhone && Platform != Windows && Platform != MacOS && Platform != nix)""
    },
    ""joinedRename"": {
      ""type"": ""generated"",
      ""generator"": ""join"",
      ""replaces"": ""SupportedPlatforms"",
      ""parameters"": {
        ""symbols"": [
          {
            ""type"": ""ref"",
            ""value"": ""Platform""
          }
        ],
        ""separator"": "", "",
        ""removeEmptyValues"": true
      }
    }
  }
}
";

            string sourceSnippet = @"
//#if IsAndroidOnly
This renders for android only
//#elseif IsMobile
This renders for rest of mobile platforms
//#else
This renders for desktop platforms
//#endif
Console.WriteLine(""Hello, World!"");

// Plats: SupportedPlatforms
";

            string expectedSnippet = @"
This renders for rest of mobile platforms
Console.WriteLine(""Hello, World!"");

// Plats: android, iOS
";

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>();
            // template.json
            templateSourceFiles.Add(TestFileSystemHelper.DefaultConfigRelativePath, templateConfig);

            //content
            templateSourceFiles.Add("sourcFile", sourceSnippet);

            //
            // Dependencies preparation and mounting
            //

            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(environment);

            TestFileSystemHelper.WriteTemplateSource(environment, sourceBasePath, templateSourceFiles);
            IMountPoint? sourceMountPoint = TestFileSystemHelper.CreateMountPoint(environment, sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            SimpleConfigModel configModel = SimpleConfigModel.FromJObject(JObject.Parse(templateConfig));
            RunnableProjectConfig runnableConfig = new RunnableProjectConfig(environment, rpg, configModel, sourceMountPoint.FileInfo(TestFileSystemHelper.DefaultConfigRelativePath));
            ParameterSetData parametersData = new ParameterSetData(
                runnableConfig,
                new Dictionary<string, object?>() { { "Platform", new MultiValueParameter(new[] { "android", "iOS" }) } });
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await rpg.CreateAsync(environment, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Veryfying the outputs
            //

            string resultContent = environment.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourcFile"));
            Assert.Equal(expectedSnippet, resultContent);
        }

        [Fact]
        public async void CreateAsyncTest_MultiChoiceParamJoining()
        {
            //
            // Template content preparation
            //

            string templateConfig = @"
{
  ""identity"": ""test.template"",
  ""symbols"": {
    ""Platform"": {
      ""type"": ""parameter"",
      ""description"": ""The target framework for the project."",
      ""datatype"": ""choice"",
      ""allowMultipleValues"": true,
      ""choices"": [
        {
          ""choice"": ""Windows"",
          ""description"": ""Windows Desktop""
        },
        {
          ""choice"": ""WindowsPhone"",
          ""description"": ""Windows Phone""
        },
        {
          ""choice"": ""MacOS"",
          ""description"": ""Macintosh computers""
        },
        {
          ""choice"": ""iOS"",
          ""description"": ""iOS mobile""
        },
        {
          ""choice"": ""android"",
          ""description"": ""android mobile""
        },
        {
          ""choice"": ""nix"",
          ""description"": ""Linux distributions""
        }
      ],
      ""defaultValue"": ""MacOS|iOS""
    },
    ""joinedRename"": {
      ""type"": ""generated"",
      ""generator"": ""join"",
      ""replaces"": ""SupportedPlatforms"",
      ""parameters"": {
        ""symbols"": [
          {
            ""type"": ""ref"",
            ""value"": ""Platform""
          }
        ],
        ""separator"": "", "",
        ""removeEmptyValues"": true,
      }
    }
  }
}
";

            string sourceSnippet = @"
// This file is generated for platfrom: SupportedPlatforms
";

            string expectedSnippet = @"
// This file is generated for platfrom: MacOS, iOS
";

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>();
            // template.json
            templateSourceFiles.Add(TestFileSystemHelper.DefaultConfigRelativePath, templateConfig);

            //content
            templateSourceFiles.Add("sourcFile", sourceSnippet);

            //
            // Dependencies preparation and mounting
            //

            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(environment);

            TestFileSystemHelper.WriteTemplateSource(environment, sourceBasePath, templateSourceFiles);
            IMountPoint? sourceMountPoint = TestFileSystemHelper.CreateMountPoint(environment, sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            SimpleConfigModel configModel = SimpleConfigModel.FromJObject(JObject.Parse(templateConfig));
            RunnableProjectConfig runnableConfig = new RunnableProjectConfig(environment, rpg, configModel, sourceMountPoint.FileInfo(TestFileSystemHelper.DefaultConfigRelativePath));
            ParameterSetData parametersData = new ParameterSetData(
                runnableConfig,
                new Dictionary<string, object?>() { { "Platform", new MultiValueParameter(new[] { "MacOS", "iOS" }) } });
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await rpg.CreateAsync(environment, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Veryfying the outputs
            //

            string resultContent = environment.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourcFile"));
            Assert.Equal(expectedSnippet, resultContent);
        }
    }
}
