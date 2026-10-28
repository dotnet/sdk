// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests
{
    [TestClass]
    public class ValidationTests : TestBase
    {
        public TestContext TestContext { get; set; } = null!;

        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        [TestMethod]
        public async Task ValidateAllTestTemplates()
        {
            string[] exceptions = new[] { "MissingConfigTest" };

            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            using IMountPoint sourceMountPoint = environmentSettings.MountPath(TestTemplatesLocation);
            RunnableProjectGenerator generator = new();
            IReadOnlyList<ScannedTemplateInfo> loadedTemplates = await generator.GetTemplatesFromMountPointInternalAsync(sourceMountPoint, TestContext.CancellationToken);

            IEnumerable<ScannedTemplateInfo> filteredTemplates = loadedTemplates.Where(t => !exceptions.Contains(t.ConfigurationModel.Identity));

            Assert.IsTrue(filteredTemplates.All(t => t.IsValid));
        }

        [TestMethod]
        public async Task ValidateAllSampleTemplates()
        {
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            using IMountPoint sourceMountPoint = environmentSettings.MountPath(SampleTemplatesLocation);
            RunnableProjectGenerator generator = new();
            IReadOnlyList<ScannedTemplateInfo> loadedTemplates = await generator.GetTemplatesFromMountPointInternalAsync(sourceMountPoint, TestContext.CancellationToken);
            Assert.IsTrue(loadedTemplates.All(t => t.IsValid));
        }

        [TestMethod]
        public async Task ValidateInvalidTemplate()
        {
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            using IMountPoint sourceMountPoint = environmentSettings.MountPath(GetTestTemplateLocation("Invalid/MissingMandatoryConfig"));
            RunnableProjectGenerator generator = new();
            IReadOnlyList<ScannedTemplateInfo> loadedTemplates = await generator.GetTemplatesFromMountPointInternalAsync(sourceMountPoint, TestContext.CancellationToken);

            ScannedTemplateInfo loadedTemplate = Assert.ContainsSingle(loadedTemplates);
            Assert.IsFalse(loadedTemplate.IsValid);
            Assert.HasCount(2, loadedTemplate.ValidationErrors.Where(ve => ve.Severity == IValidationEntry.SeverityLevel.Error));
        }
    }
}
