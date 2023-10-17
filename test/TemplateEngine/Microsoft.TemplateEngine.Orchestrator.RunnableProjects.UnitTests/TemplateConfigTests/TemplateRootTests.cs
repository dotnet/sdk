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
    public class TemplateRootTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly EnvironmentSettingsHelper _engineEnvironmentSettingsHelper;

        public TemplateRootTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettingsHelper = environmentSettingsHelper;
        }

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

        [Fact]
        public void TemplateJsonCannotBeInMountPointRoot()
        {
            string pathToTemplateJson = "template.json";
            string expectedErrorMessage = "The template root is outside the specified install source location.";

            IEngineEnvironmentSettings environmentSettings = _engineEnvironmentSettingsHelper.CreateEnvironment(virtualize: true);
            RunnableProjectGenerator generator = new();

            string sourcePath = environmentSettings.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { pathToTemplateJson, BasicTemplateConfig }
            };
            environmentSettings.WriteTemplateSource(sourcePath, templateSourceFiles);

            using IMountPoint mountPoint = environmentSettings.MountPath(sourcePath);
            IFile? templateConfigFile = mountPoint.FileInfo(pathToTemplateJson);
            Assert.NotNull(templateConfigFile);

            TemplateAuthoringException e = Assert.Throws<TemplateAuthoringException>(() => new RunnableProjectConfig(environmentSettings, generator, templateConfigFile));
            Assert.Equal(expectedErrorMessage, e.Message);
        }

        [Theory]
        [InlineData(".template.config/template.json")]
        [InlineData("content/.template.config/template.json")]
        [InlineData("src/content/.template.config/template.json")]
        public async Task CheckTemplateRootRelativeToInstallPath(string pathToTemplateJson)
        {
            IEngineEnvironmentSettings environmentSettings = _engineEnvironmentSettingsHelper.CreateEnvironment(virtualize: true);
            RunnableProjectGenerator generator = new();

            string sourcePath = environmentSettings.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { pathToTemplateJson, BasicTemplateConfig }
            };
            environmentSettings.WriteTemplateSource(sourcePath, templateSourceFiles);

            using IMountPoint mountPoint = environmentSettings.MountPath(sourcePath);
            IFile? templateConfigFile = mountPoint.FileInfo(pathToTemplateJson);
            Assert.NotNull(templateConfigFile);
            using RunnableProjectConfig templateModel = new(environmentSettings, generator, templateConfigFile);
            await templateModel.ValidateAsync(ValidationScope.Instantiation, default);

            Assert.True(templateModel.IsValid);
            Assert.Empty(templateModel.ValidationErrors.Where(e => e.Severity is IValidationEntry.SeverityLevel.Error or IValidationEntry.SeverityLevel.Warning));
        }

        // Tests source paths when the mount point root is the same as the template root.
        [Theory(DisplayName = nameof(CheckTemplateSourcesRelativeToTemplateRoot))]
        [InlineData(true, "things/")]
        [InlineData(true, "things/stuff/")]
        [InlineData(true, "./")]
        [InlineData(false, "../", "Source location '../' is outside the specified install source location.")] // outside the mount point, combining throws and is caught.
        [InlineData(false, "foo/", "Source 'foo/' in template does not exist.")] // not valid because the path doesn't exist under the root.
        public async Task CheckTemplateSourcesRelativeToTemplateRoot(bool shouldAllPathsBeValid, string source, string? expectedErrorMessage = null)
        {
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = _engineEnvironmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });
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
            Assert.NotNull(templateConfigFile);

            using RunnableProjectConfig templateModel = new RunnableProjectConfig(environmentSettings, generator, templateConfigFile);
            await templateModel.ValidateAsync(ValidationScope.Instantiation, default);

            if (shouldAllPathsBeValid)
            {
                Assert.True(templateModel.IsValid);
                Assert.Empty(templateModel.ValidationErrors.Where(e => e.Severity is IValidationEntry.SeverityLevel.Error or IValidationEntry.SeverityLevel.Warning));
            }
            else
            {
                Assert.NotNull(expectedErrorMessage);
                Assert.False(templateModel.IsValid);
                IValidationEntry validationError = Assert.Single(templateModel.ValidationErrors, e => e.Severity is IValidationEntry.SeverityLevel.Error or IValidationEntry.SeverityLevel.Warning);
                Assert.Equal("MV012", validationError.Code);
                Assert.Equal(expectedErrorMessage, validationError.ErrorMessage);
            }
        }

        [Theory(DisplayName = nameof(CheckTemplateSourcesRelativeToTemplateRootMultipleDirsUnderMountPoint))]
        [InlineData(true, "things/")]
        [InlineData(true, "things/stuff/")]
        [InlineData(true, "./")]
        [InlineData(true, "../")] // outside the template root, but in the mount point
        [InlineData(true, "../../")] // outside the template root, but in the mount point
        [InlineData(true, "../../../")] // outside the template root, but at the mount point root
        [InlineData(false, "../../../../", "Source location '../../../../' is outside the specified install source location.")]
        [InlineData(false, "foo/", "Source 'foo/' in template does not exist.")]
        [InlineData(false, "../../../Other/", "Source '../../../Other/' in template does not exist.")]
        [InlineData(false, "../../../../Other/", "Source location '../../../../Other/' is outside the specified install source location.")]
        [InlineData(true, "../../../MountRoot/")]
        [InlineData(false, "../../../MountRoot/Other", "Source '../../../MountRoot/Other' in template does not exist.")]
        [InlineData(true, "../../../ExistingDir/")]
        [InlineData(true, "../../../MountRoot/Subdir")]
        public async Task CheckTemplateSourcesRelativeToTemplateRootMultipleDirsUnderMountPoint(bool shouldAllPathsBeValid, string source, string? expectedErrorMessage = null)
        {
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = _engineEnvironmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

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
            Assert.NotNull(templateConfigFile);

            using RunnableProjectConfig templateModel = new RunnableProjectConfig(environmentSettings, generator, templateConfigFile);
            await templateModel.ValidateAsync(ValidationScope.Instantiation, default);

            if (shouldAllPathsBeValid)
            {
                Assert.True(templateModel.IsValid);
                Assert.Empty(templateModel.ValidationErrors.Where(e => e.Severity is IValidationEntry.SeverityLevel.Error or IValidationEntry.SeverityLevel.Warning));
            }
            else
            {
                Assert.NotNull(expectedErrorMessage);
                Assert.False(templateModel.IsValid);
                IValidationEntry validationError = Assert.Single(templateModel.ValidationErrors, e => e.Severity is IValidationEntry.SeverityLevel.Error or IValidationEntry.SeverityLevel.Warning);
                Assert.Equal("MV012", validationError.Code);
                Assert.Equal(expectedErrorMessage, validationError.ErrorMessage);
            }
        }
    }
}
