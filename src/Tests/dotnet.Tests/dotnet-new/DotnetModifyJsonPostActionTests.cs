// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.DotNet.Tools.New.PostActionProcessors;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.DotNet.Cli.New.Tests
{
    public class DotnetModifyJsonPostActionTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public DotnetModifyJsonPostActionTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [Fact]
        public void ModifyJsonCanAddSimplePropertyToExistingJsonFile()
        {
            // Arrange
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            string jsonFilePath = CreateJsonFile(targetBasePath, "jsonfile.json", @"{""person"":{""name"":""bob""}}");

            IPostAction postAction = new MockPostAction()
            {
                ActionId = DotnetModifyJsonPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>
                {
                    ["jsonFileName"] = "jsonfile.json",
                    ["parentProperty"] = "person",
                    ["newJsonPropertyName"] = "lastName",
                    ["newJsonPropertyValue"] = "Watson"
                }
            };

            DotnetModifyJsonPostActionProcessor processor = new DotnetModifyJsonPostActionProcessor();

            bool result = processor.Process(
                _engineEnvironmentSettings,
                postAction,
                new MockCreationEffects(),
                new MockCreationResult(),
                targetBasePath);

            Assert.True(result);

            JsonNode modifiedJsonContent = JsonNode.Parse(_engineEnvironmentSettings.Host.FileSystem.ReadAllText(jsonFilePath));

            Assert.NotNull(modifiedJsonContent["person"]["lastName"]);
            Assert.Equal("Watson", modifiedJsonContent["person"]["lastName"].ToString());
        }

        private string CreateJsonFile(string targetBasePath, string fileName, string jsonContent)
        {
            string jsonFileFullPath = Path.Combine(targetBasePath, fileName);
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(jsonFileFullPath, jsonContent);

            return jsonFileFullPath;
        }
    }
}
