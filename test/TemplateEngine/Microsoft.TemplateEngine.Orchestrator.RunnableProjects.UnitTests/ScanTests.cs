// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests
{
    public class ScanTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        public ScanTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        [Fact]
        public async Task CanReadPostActions()
        {
            var jsonToBe = new
            {
                name = "TestTemplate",
                identity = "id",
                shortName = "test",
                postActions = new[]
                {
                    new
                    {
                        actionId = Guid.NewGuid(),
                    },
                    new
                    {
                        actionId = Guid.NewGuid(),
                    },
                }
            };
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string sourceBasePath = environmentSettings.GetTempVirtualizedPath();

            string templateConfigDir = Path.Combine(sourceBasePath, RunnableProjectGenerator.TemplateConfigDirectoryName);
            string filePath = Path.Combine(templateConfigDir, RunnableProjectGenerator.TemplateConfigFileName);
            environmentSettings.Host.FileSystem.CreateDirectory(templateConfigDir);
            environmentSettings.Host.FileSystem.WriteAllText(filePath, JsonConvert.SerializeObject(jsonToBe));

            using IMountPoint mountPoint = environmentSettings.MountPath(sourceBasePath);
            RunnableProjectGenerator generator = new RunnableProjectGenerator();
            IReadOnlyList<IScanTemplateInfo>? templates = await (generator as IGenerator).GetTemplatesFromMountPointAsync(mountPoint, default);

            Assert.Single(templates);
            var template = templates[0];
            Assert.Equal(new[] { jsonToBe.postActions[0].actionId, jsonToBe.postActions[1].actionId }, template.PostActions);
        }
    }
}
