// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class TemplateRootTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        public TemplateRootTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
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

        [Theory(DisplayName = nameof(CheckTemplateRootRelativeToInstallPath))]
        [InlineData("template.json", false)]
        [InlineData(".template.config/template.json", true)]
        [InlineData("content/.template.config/template.json", true)]
        [InlineData("src/content/.template.config/template.json", true)]
        public void CheckTemplateRootRelativeToInstallPath(string pathToTemplateJson, bool shouldAllPathsBeValid)
        {
            RunnableProjectGenerator generator = new();
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            string sourcePath = environmentSettings.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { pathToTemplateJson, BasicTemplateConfig }
            };
            environmentSettings.WriteTemplateSource(sourcePath, templateSourceFiles);

            using IMountPoint mountPoint = environmentSettings.MountPath(sourcePath);
            IFile? templateConfigFile = mountPoint.FileInfo(pathToTemplateJson);
            Assert.NotNull(templateConfigFile);

            if (shouldAllPathsBeValid)
            {
                RunnableProjectConfig templateModel = new RunnableProjectConfig(environmentSettings, generator, templateConfigFile);
                Assert.Empty(templateModel.ValidateTemplateSourcePaths());
            }
            else
            {
                TemplateAuthoringException e = Assert.Throws<TemplateAuthoringException>(() => new RunnableProjectConfig(environmentSettings, generator, templateConfigFile));
                Assert.Equal("The template root is outside the specified install source location.", e.Message);
            }
        }

        // Tests source paths when the mount point root is the same as the template root.
        [Theory(DisplayName = nameof(CheckTemplateSourcesRelativeToTemplateRoot))]
        [InlineData(true, "things/")]
        [InlineData(true, "things/stuff/")]
        [InlineData(true, "./")]
        [InlineData(false, "../", "Source location '../' is outside the specified install source location.")] // outside the mount point, combining throws and is caught.
        [InlineData(false, "foo/", "Source 'foo/' in template does not exist.")] // not valid because the path doesn't exist under the root.
        public void CheckTemplateSourcesRelativeToTemplateRoot(bool shouldAllPathsBeValid, string source, string? errorMessage = null)
        {
            string templateConfig = string.Format(TemplateConfigWithSourcePlaceholder, source);
            RunnableProjectGenerator generator = new();
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

            const string pathToTemplateConfig = ".template.config/template.json";
            string sourcePath = environmentSettings.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { pathToTemplateConfig, templateConfig },
                { "things/stuff/_._", string.Empty } // directories under the root - valid source locations.
            };
            environmentSettings.WriteTemplateSource(sourcePath, templateSourceFiles);

            using IMountPoint mountPoint = environmentSettings.MountPath(sourcePath);
            IFile? templateConfigFile = mountPoint.FileInfo(pathToTemplateConfig);
            Assert.NotNull(templateConfigFile);

            if (shouldAllPathsBeValid)
            {
                RunnableProjectConfig templateModel = new RunnableProjectConfig(environmentSettings, generator, templateConfigFile);
                Assert.Empty(templateModel.ValidateTemplateSourcePaths());
            }
            else
            {
                _ = Assert.Throws<TemplateValidationException>(() => new RunnableProjectConfig(environmentSettings, generator, templateConfigFile));
                string actual = Assert.Single(loggedMessages, m => m.Level == LogLevel.Error).Message;
                Assert.NotNull(errorMessage);
                Assert.Contains(errorMessage, actual);
            }
        }

        [Theory(DisplayName = nameof(CheckTemplateSourcesRelativeToTemplateRootMultipleDirsUnderMountPoint))]
        [InlineData(true, "things/")]
        [InlineData(true, "things/stuff/")]
        [InlineData(true, "./")]
        [InlineData(true, "../")] // outside the template root, but in the mount point
        [InlineData(true, "../../")] // outside the template root, but in the mount point
        [InlineData(true, "../../../")] // outside the template root, but at the mount point root
        [InlineData(false, "../../../../", "Source location '../../../../' is outside the specified install source location.")] // outside the mount point
        [InlineData(false, "foo/", "Source 'foo/' in template does not exist.")] // not valid because the path doesn't exist under the root
        [InlineData(false, "../../../Other/", "Source '../../../Other/' in template does not exist.")] // doesn't exist
        [InlineData(false, "../../../../Other/", "Source location '../../../../Other/' is outside the specified install source location.")] // outside the mount point
        [InlineData(true, "../../../MountRoot/")]
        [InlineData(false, "../../../MountRoot/Other", "Source '../../../MountRoot/Other' in template does not exist.")] // directory doesn't exist
        [InlineData(true, "../../../ExistingDir/")]
        [InlineData(true, "../../../MountRoot/Subdir")]
        public void CheckTemplateSourcesRelativeToTemplateRootMultipleDirsUnderMountPoint(bool shouldAllPathsBeValid, string source, string? errorMessage = null)
        {
            string templateConfig = string.Format(TemplateConfigWithSourcePlaceholder, source);
            RunnableProjectGenerator generator = new();
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

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

            if (shouldAllPathsBeValid)
            {
                RunnableProjectConfig templateModel = new RunnableProjectConfig(environmentSettings, generator, templateConfigFile);
                Assert.Empty(templateModel.ValidateTemplateSourcePaths());
            }
            else
            {
                _ = Assert.Throws<TemplateValidationException>(() => new RunnableProjectConfig(environmentSettings, generator, templateConfigFile));
                string actual = Assert.Single(loggedMessages, m => m.Level == LogLevel.Error).Message;
                Assert.NotNull(errorMessage);
                Assert.Contains(errorMessage, actual);
            }
        }
    }
}
