// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class TemplateCreatorTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public TemplateCreatorTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        private const string TemplateConfigQuotelessLiteralsEnabled = @"
{
    ""identity"": ""test.template"",
    ""name"": ""tst"",
    ""shortName"": ""tst"",
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
          ""defaultIfOptionWithoutValue"": ""SecondChoice""
	    }
    }
}
";

        [Theory]
        // basic choice
        [InlineData("FirstChoice", "FIRST", false)]
        // nonexistent choice
        [InlineData("Invalid", "UNKNOWN", true)]
        // value not set - default used
        [InlineData(null, "SECOND", false)]
        // explicit unset
        [InlineData("", "UNKNOWN", false)]
        public async void InstantiateAsync_ParamsProperlyHonored(string? parameterValue, string expectedOutput, bool instantiateShouldFail)
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
            templateSourceFiles.Add(TestFileSystemHelper.DefaultConfigRelativePath, TemplateConfigQuotelessLiteralsEnabled);

            //content
            templateSourceFiles.Add("sourceFile", sourceSnippet);

            //
            // Dependencies preparation and mounting
            //

            IEngineEnvironmentSettings environment = _engineEnvironmentSettings;
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);

            TestFileSystemHelper.WriteTemplateSource(environment, sourceBasePath, templateSourceFiles);
            IMountPoint? sourceMountPoint = TestFileSystemHelper.CreateMountPoint(environment, sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            // cannot use SimpleConfigModel dirrectly - due to missing easy way of creating ParameterSymbols
            SimpleConfigModel configModel = SimpleConfigModel.FromJObject(JObject.Parse(TemplateConfigQuotelessLiteralsEnabled));
            var runnableConfig = new RunnableProjectConfig(environment, rpg, configModel, sourceMountPoint.FileInfo(TestFileSystemHelper.DefaultConfigRelativePath));

            TemplateCreator creator = new TemplateCreator(_engineEnvironmentSettings);

            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);

            IReadOnlyDictionary<string, string?> parameters = new Dictionary<string, string?>()
            {
                { "ChoiceParam", parameterValue }
            };

            var res = await creator.InstantiateAsync(
                templateInfo: runnableConfig,
                name: "tst",
                fallbackName: "tst2",
                inputParameters: parameters,
                outputPath: targetDir);

            if (instantiateShouldFail)
            {
                Assert.NotNull(res.ErrorMessage);
                Assert.Null(res.OutputBaseDirectory);
            }
            else
            {
                Assert.Null(res.ErrorMessage);
                Assert.NotNull(res.OutputBaseDirectory);
                string resultContent = _engineEnvironmentSettings.Host.FileSystem
                    .ReadAllText(Path.Combine(res.OutputBaseDirectory!, "sourceFile")).Trim();
                Assert.Equal(expectedOutput, resultContent);
            }
        }
    }
}
