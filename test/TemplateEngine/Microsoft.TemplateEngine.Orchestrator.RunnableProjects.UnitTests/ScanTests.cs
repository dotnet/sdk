// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests
{
    [TestClass]
    public class ScanTests
    {
        public TestContext TestContext { get; set; } = null!;

        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        [TestMethod]
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
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string sourceBasePath = environmentSettings.GetTempVirtualizedPath();

            string templateConfigDir = Path.Combine(sourceBasePath, RunnableProjectGenerator.TemplateConfigDirectoryName);
            string filePath = Path.Combine(templateConfigDir, RunnableProjectGenerator.TemplateConfigFileName);
            environmentSettings.Host.FileSystem.CreateDirectory(templateConfigDir);
            environmentSettings.Host.FileSystem.WriteAllText(filePath, JsonSerializer.Serialize(jsonToBe));

            using IMountPoint mountPoint = environmentSettings.MountPath(sourceBasePath);
            RunnableProjectGenerator generator = new RunnableProjectGenerator();
            IReadOnlyList<IScanTemplateInfo>? templates = await (generator as IGenerator).GetTemplatesFromMountPointAsync(mountPoint, TestContext.CancellationToken);

            Assert.ContainsSingle(templates);
            var template = templates[0];
            Assert.AreSequenceEqual(new[] { jsonToBe.postActions[0].actionId, jsonToBe.postActions[1].actionId }, template.PostActions);
        }
    }
}
