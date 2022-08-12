// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.IO;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class GenericTests
    {
        private static readonly string TestTemplate = "{\r\n  \"author\": \"Test Asset\",\r\n  \"classifications\": [ \"Test Asset\" ],\r\n  \"name\": \"TemplateWithSourceName\",\r\n  \"generatorVersions\": \"[1.0.0.0-*)\",\r\n  \"groupIdentity\": \"TestAssets.TemplateWithSourceName\",\r\n  \"precedence\": \"100\",\r\n  \"identity\": \"TestAssets.TemplateWithSourceName\",\r\n  \"shortName\": \"TestAssets.TemplateWithSourceName\",\r\n  \"sourceName\": \"bar\",\r\n  \"primaryOutputs\": [\r\n    {\r\n      \"path\": \"bar.cs\"\r\n    },\r\n    {\r\n      \"path\": \"bar/bar.cs\"\r\n    },\r\n  ]\r\n}";

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
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestTemplate ?? ""));
            TemplateConfigModel templateConfigModel = TemplateConfigModel.FromStream(stream);

            Assert.Equal("Test Asset", templateConfigModel.Author);
            Assert.Equal("TemplateWithSourceName", templateConfigModel.Name);
            Assert.Equal("bar", templateConfigModel.SourceName);
            Assert.Equal(2, templateConfigModel.PrimaryOutputs.Count);
            Assert.Equal(new[] { "bar.cs", "bar/bar.cs" }, templateConfigModel.PrimaryOutputs.Select(po => po.Path).OrderBy(po => po));
        }
    }
}
