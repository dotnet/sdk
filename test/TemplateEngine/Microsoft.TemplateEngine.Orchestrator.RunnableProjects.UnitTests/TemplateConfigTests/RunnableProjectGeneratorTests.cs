using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class RunnableProjectTemplateTests : TestBase
    {
        [Fact(DisplayName = nameof(SetConfigTimestampUtc))]
        public void SetConfigTimestampUtc()
        {
            string templateJson = @"
{
  ""name"": ""TestTemplate"",
  ""identity"": ""TestTemplate"",
  ""shortName"": ""testt"",
  ""symbols"": {
    ""mySymbol"": {
      ""type"": ""parameter"",
      ""replaces"": ""whatever"",
      ""forms"": {
        ""global"": [ ""fakeName"" ],
      }
    }
  }
}
";
            var pathToTemplateJson = "templateSource/.template.config/template.json";
            string sourcePath = FileSystemHelpers.GetNewVirtualizedPath(EngineEnvironmentSettings);
            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            templateSourceFiles.Add(pathToTemplateJson, templateJson);
            TestTemplateSetup setup = new TestTemplateSetup(EngineEnvironmentSettings, sourcePath, templateSourceFiles);
            setup.WriteSource();

            RunnableProjectGenerator generator = new RunnableProjectGenerator();
            var templateFile = setup.InfoForSourceFile(pathToTemplateJson);
            generator.TryGetTemplateFromConfigInfo(templateFile, out ITemplate template, null, null);

            var templateWithTimestamp = Assert.IsAssignableFrom<ITemplateWithTimestamp>(template);
            Assert.NotNull(templateWithTimestamp.ConfigTimestampUtc);
        }
    }
}
