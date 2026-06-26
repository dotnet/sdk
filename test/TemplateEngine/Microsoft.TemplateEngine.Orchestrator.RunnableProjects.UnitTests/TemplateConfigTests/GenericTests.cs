// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    [TestClass]
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

        [TestMethod]
        public void CanReadTemplateFromString()
        {
            TemplateConfigModel templateConfigModel = TemplateConfigModel.FromString(TestTemplate);

            Assert.AreEqual("Test Asset", templateConfigModel.Author);
            Assert.AreEqual("TemplateWithSourceName", templateConfigModel.Name);
            Assert.AreEqual("bar", templateConfigModel.SourceName);
            Assert.HasCount(2, templateConfigModel.PrimaryOutputs);
            Assert.AreSequenceEqual(new[] { "bar.cs", "bar/bar.cs" }, templateConfigModel.PrimaryOutputs.Select(po => po.Path).OrderBy(po => po));
        }

        [TestMethod]
        public void CanReadTemplateFromStream()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestTemplate ?? string.Empty));
            TemplateConfigModel templateConfigModel = TemplateConfigModel.FromStream(stream);

            Assert.AreEqual("Test Asset", templateConfigModel.Author);
            Assert.AreEqual("TemplateWithSourceName", templateConfigModel.Name);
            Assert.AreEqual("bar", templateConfigModel.SourceName);
            Assert.HasCount(2, templateConfigModel.PrimaryOutputs);
            Assert.AreSequenceEqual(new[] { "bar.cs", "bar/bar.cs" }, templateConfigModel.PrimaryOutputs.Select(po => po.Path).OrderBy(po => po));
        }

        [TestMethod]
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

            Exception? exception = null;
            try
            {
                TemplateConfigModel.FromString(templateWithDuplicateKeys);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            Assert.IsNull(exception);

            TemplateConfigModel configModel = TemplateConfigModel.FromString(templateWithDuplicateKeys);
            Assert.AreEqual("TemplateWithDuplicateKeys", configModel.Name);
            // Both symbols should be accessible (last-in-wins for case-sensitive dict, both kept)
            Assert.IsNotEmpty(configModel.Symbols);
        }

        [TestMethod]
        public void CanReadTemplateWithExactDuplicateKeys()
        {
            // Regression test: template.json with exact duplicate property keys
            // (e.g. two "defaultName" entries) should load without throwing.
            // Old NUnit templates from .NET 7 SDK have this issue.
            // See https://github.com/dotnet/sdk/issues/54160
            string templateWithExactDuplicates = /*lang=json*/ """
                {
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "TemplateWithExactDuplicates",
                  "identity": "TestAssets.TemplateWithExactDuplicates",
                  "shortName": "exactdup",
                  "defaultName": "FirstValue",
                  "symbols": {
                    "Framework": {
                      "type": "parameter",
                      "datatype": "choice",
                      "choices": [
                        { "choice": "net9.0", "description": "Target net9.0" }
                      ],
                      "defaultValue": "net9.0"
                    }
                  },
                  "defaultName": "SecondValue"
                }
                """;

            Exception? exception = null;
            try
            {
                TemplateConfigModel.FromString(templateWithExactDuplicates);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            Assert.IsNull(exception);

            TemplateConfigModel configModel = TemplateConfigModel.FromString(templateWithExactDuplicates);
            Assert.AreEqual("TemplateWithExactDuplicates", configModel.Name);
            // Last-wins semantics: "SecondValue" should be the final defaultName
            Assert.AreEqual("SecondValue", configModel.DefaultName);
        }
    }
}
