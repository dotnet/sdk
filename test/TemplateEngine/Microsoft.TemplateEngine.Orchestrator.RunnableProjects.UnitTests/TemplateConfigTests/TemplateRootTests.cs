// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    [TestClass]
    public class TemplateRootTests
    {
        public TestContext TestContext { get; set; } = null!;

        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        private static string TemplateConfigWithSourcePlaceholder
        {
            get
            {
                string templateJsonString = """
                {{
                  "author": "Microsoft",
                  "classifications": ["Test"],
                  "name": "Test Template",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "Testing.TemplateRoot",
                  "identity": "Testing.Template.Root.CSharp",
                  "shortName": "templateRootTest",
                  "sourceName": "Company.ConsoleApplication1",
                  "preferNameDirectory": true,
                  "sources": [
                      {{
                        "source": "{0}"
                      }}
                  ]
                }}
                """;
                return templateJsonString;
            }
        }

        private static string BasicTemplateConfig
        {
            get
            {
                string templateJsonString = /*lang=json*/ """
                {
                  "author": "Microsoft",
                  "classifications": ["Test"],
                  "name": "Test Template",
                  "generatorVersions": "[1.0.0.0-*)",
                  "groupIdentity": "Testing.TemplateRoot",
                  "identity": "Testing.Template.Root.CSharp",
                  "shortName": "templateRootTest",
                  "sourceName": "Company.ConsoleApplication1",
                  "preferNameDirectory": true,
                }
                """;
                return templateJsonString;
            }
        }

        [TestMethod]
        public void TemplateJsonCannotBeInMountPointRoot()
        {
            string pathToTemplateJson = "template.json";
            string expectedErrorMessage = "The template root is outside the specified install source location.";

            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            RunnableProjectGenerator generator = new();

            string sourcePath = environmentSettings.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { pathToTemplateJson, BasicTemplateConfig }
            };
            environmentSettings.WriteTemplateSource(sourcePath, templateSourceFiles);

            using IMountPoint mountPoint = environmentSettings.MountPath(sourcePath);
            IFile? templateConfigFile = mountPoint.FileInfo(pathToTemplateJson);
            Assert.IsNotNull(templateConfigFile);

            TemplateAuthoringException e = Assert.ThrowsExactly<TemplateAuthoringException>(() => new RunnableProjectConfig(environmentSettings, generator, templateConfigFile));
            Assert.AreEqual(expectedErrorMessage, e.Message);
        }

        [TestMethod]
        [DataRow(".template.config/template.json")]
        [DataRow("content/.template.config/template.json")]
        [DataRow("src/content/.template.config/template.json")]
        public async Task CheckTemplateRootRelativeToInstallPath(string pathToTemplateJson)
        {
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            RunnableProjectGenerator generator = new();

            string sourcePath = environmentSettings.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { pathToTemplateJson, BasicTemplateConfig }
            };
            environmentSettings.WriteTemplateSource(sourcePath, templateSourceFiles);

            using IMountPoint mountPoint = environmentSettings.MountPath(sourcePath);
            IFile? templateConfigFile = mountPoint.FileInfo(pathToTemplateJson);
            Assert.IsNotNull(templateConfigFile);
            using RunnableProjectConfig templateModel = new(environmentSettings, generator, templateConfigFile);
            await templateModel.ValidateAsync(ValidationScope.Instantiation, TestContext.CancellationToken);

            Assert.IsTrue(templateModel.IsValid);
            Assert.IsEmpty(templateModel.ValidationErrors.Where(e => e is { Severity: IValidationEntry.SeverityLevel.Error } or { Severity: IValidationEntry.SeverityLevel.Warning }));
        }

        // Tests source paths when the mount point root is the same as the template root.
        [TestMethod]
        [DataRow(true, "things/")]
        [DataRow(true, "things/stuff/")]
        [DataRow(true, "./")]
        [DataRow(false, "../", "Source location '../' is outside the specified install source location.")] // outside the mount point, combining throws and is caught.
        [DataRow(false, "foo/", "Source 'foo/' in template does not exist.")] // not valid because the path doesn't exist under the root.
        public async Task CheckTemplateSourcesRelativeToTemplateRoot(bool shouldAllPathsBeValid, string source, string? expectedErrorMessage = null)
        {
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });
            string templateConfig = string.Format(TemplateConfigWithSourcePlaceholder, source);
            RunnableProjectGenerator generator = new();

            const string pathToTemplateConfig = ".template.config/template.json";
            string sourcePath = environmentSettings.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { pathToTemplateConfig, templateConfig },
                // directories under the root - valid source locations.
                { "things/stuff/_._", string.Empty }
            };

            environmentSettings.WriteTemplateSource(sourcePath, templateSourceFiles);

            using IMountPoint mountPoint = environmentSettings.MountPath(sourcePath);
            IFile? templateConfigFile = mountPoint.FileInfo(pathToTemplateConfig);
            Assert.IsNotNull(templateConfigFile);

            using RunnableProjectConfig templateModel = new RunnableProjectConfig(environmentSettings, generator, templateConfigFile);
            await templateModel.ValidateAsync(ValidationScope.Instantiation, TestContext.CancellationToken);

            if (shouldAllPathsBeValid)
            {
                Assert.IsTrue(templateModel.IsValid);
                Assert.IsEmpty(templateModel.ValidationErrors.Where(e => e is { Severity: IValidationEntry.SeverityLevel.Error } or { Severity: IValidationEntry.SeverityLevel.Warning }));
            }
            else
            {
                Assert.IsNotNull(expectedErrorMessage);
                Assert.IsFalse(templateModel.IsValid);
                IValidationEntry validationError = Assert.ContainsSingle(templateModel.ValidationErrors.Where(e => e.Severity is IValidationEntry.SeverityLevel.Error or IValidationEntry.SeverityLevel.Warning));
                Assert.AreEqual("MV012", validationError.Code);
                Assert.AreEqual(expectedErrorMessage, validationError.ErrorMessage);
            }
        }

        [TestMethod]
        [DataRow(true, "things/")]
        [DataRow(true, "things/stuff/")]
        [DataRow(true, "./")]
        [DataRow(true, "../")] // outside the template root, but in the mount point
        [DataRow(true, "../../")] // outside the template root, but in the mount point
        [DataRow(true, "../../../")] // outside the template root, but at the mount point root
        [DataRow(false, "../../../../", "Source location '../../../../' is outside the specified install source location.")]
        [DataRow(false, "foo/", "Source 'foo/' in template does not exist.")]
        [DataRow(false, "../../../Other/", "Source '../../../Other/' in template does not exist.")]
        [DataRow(false, "../../../../Other/", "Source location '../../../../Other/' is outside the specified install source location.")]
        [DataRow(true, "../../../MountRoot/")]
        [DataRow(false, "../../../MountRoot/Other", "Source '../../../MountRoot/Other' in template does not exist.")]
        [DataRow(true, "../../../ExistingDir/")]
        [DataRow(true, "../../../MountRoot/Subdir")]
        public async Task CheckTemplateSourcesRelativeToTemplateRootMultipleDirsUnderMountPoint(bool shouldAllPathsBeValid, string source, string? expectedErrorMessage = null)
        {
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

            string templateConfig = string.Format(TemplateConfigWithSourcePlaceholder, source);
            RunnableProjectGenerator generator = new RunnableProjectGenerator();

            const string pathFromMountPointRootToTemplateRoot = "MountRoot/Stuff/TemplateRoot/";
            string pathToTemplateConfig = pathFromMountPointRootToTemplateRoot + ".template.config/template.json";

            string sourcePath = environmentSettings.GetTempVirtualizedPath();
            string sampleContentDir = pathFromMountPointRootToTemplateRoot + "things/stuff/_._";
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { pathToTemplateConfig, templateConfig },
                { sampleContentDir, string.Empty },    // directories under the template root - valid source locations.
                { "ExistingDir/_._", string.Empty },
                { "MountRoot/Subdir/_._", string.Empty }
            };

            environmentSettings.WriteTemplateSource(sourcePath, templateSourceFiles);

            using IMountPoint mountPoint = environmentSettings.MountPath(sourcePath);
            IFile? templateConfigFile = mountPoint.FileInfo(pathToTemplateConfig);
            Assert.IsNotNull(templateConfigFile);

            using RunnableProjectConfig templateModel = new RunnableProjectConfig(environmentSettings, generator, templateConfigFile);
            await templateModel.ValidateAsync(ValidationScope.Instantiation, TestContext.CancellationToken);

            if (shouldAllPathsBeValid)
            {
                Assert.IsTrue(templateModel.IsValid);
                Assert.IsEmpty(templateModel.ValidationErrors.Where(e => e is { Severity: IValidationEntry.SeverityLevel.Error } or { Severity: IValidationEntry.SeverityLevel.Warning }));
            }
            else
            {
                Assert.IsNotNull(expectedErrorMessage);
                Assert.IsFalse(templateModel.IsValid);
                IValidationEntry validationError = Assert.ContainsSingle(templateModel.ValidationErrors.Where(e => e.Severity is IValidationEntry.SeverityLevel.Error or IValidationEntry.SeverityLevel.Warning));
                Assert.AreEqual("MV012", validationError.Code);
                Assert.AreEqual(expectedErrorMessage, validationError.ErrorMessage);
            }
        }
    }
}
