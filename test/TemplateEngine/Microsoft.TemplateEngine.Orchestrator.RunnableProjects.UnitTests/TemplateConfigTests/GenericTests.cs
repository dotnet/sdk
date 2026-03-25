// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class GenericTests
    {
        private static readonly string TestTemplate = /*lang=json*/ """
            {
              "author": "Test Asset",
              "classifications": [ "Test Asset" ],
              "name": "TemplateWithSourceName",
              "generatorVersions": "[1.0.0.0-*)",
              "groupIdentity": "TestAssets.TemplateWithSourceName",
              "precedence": "100",
              "identity": "TestAssets.TemplateWithSourceName",
              "shortName": "TestAssets.TemplateWithSourceName",
              "sourceName": "bar",
              "primaryOutputs": [
                {
                  "path": "bar.cs"
                },
                {
                  "path": "bar/bar.cs"
                },
              ]
            }
            """;

        [Fact]
        public void CanReadTemplateFromString()
        {
            TemplateConfigModel templateConfigModel = TemplateConfigModel.FromString(TestTemplate);

            Assert.Equal("Test Asset", templateConfigModel.Author);
            Assert.Equal("TemplateWithSourceName", templateConfigModel.Name);
            Assert.Equal("bar", templateConfigModel.SourceName);
            Assert.Equal(2, templateConfigModel.PrimaryOutputs.Count);
            Assert.Equal(new[] { "bar.cs", "bar/bar.cs" }, templateConfigModel.PrimaryOutputs.Select(po => po.Path).OrderBy(po => po));
        }

        [Fact]
        public void CanReadTemplateFromStream()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestTemplate ?? string.Empty));
            TemplateConfigModel templateConfigModel = TemplateConfigModel.FromStream(stream);

            Assert.Equal("Test Asset", templateConfigModel.Author);
            Assert.Equal("TemplateWithSourceName", templateConfigModel.Name);
            Assert.Equal("bar", templateConfigModel.SourceName);
            Assert.Equal(2, templateConfigModel.PrimaryOutputs.Count);
            Assert.Equal(new[] { "bar.cs", "bar/bar.cs" }, templateConfigModel.PrimaryOutputs.Select(po => po.Path).OrderBy(po => po));
        }

        [Fact]
        public void CanReadTemplateWithDuplicateCaseInsensitiveSymbolKeys()
        {
            // Regression test: template.json with symbols that differ only by case
            // (e.g. "Empty" and "empty") should load without throwing.
            // See https://github.com/dotnet/templating/issues/10047
            string templateWithDuplicateKeys = /*lang=json*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithDuplicateKeys",
                  "identity": "TestAssets.TemplateWithDuplicateKeys",
                  "shortName": "dupkeys",
                  "symbols": {
                    "Empty": {
                      "type": "parameter",
                      "datatype": "bool",
                      "defaultValue": "false",
                      "description": "PascalCase variant"
                    },
                    "empty": {
                      "type": "parameter",
                      "datatype": "bool",
                      "defaultValue": "false",
                      "description": "lowercase variant"
                    }
                  }
                }
                """;

            var exception = Record.Exception(() => TemplateConfigModel.FromString(templateWithDuplicateKeys));
            Assert.Null(exception);

            TemplateConfigModel configModel = TemplateConfigModel.FromString(templateWithDuplicateKeys);
            Assert.Equal("TemplateWithDuplicateKeys", configModel.Name);
            // Both symbols should be accessible (last-in-wins for case-sensitive dict, both kept)
            Assert.NotEmpty(configModel.Symbols);
        }
    }
}
