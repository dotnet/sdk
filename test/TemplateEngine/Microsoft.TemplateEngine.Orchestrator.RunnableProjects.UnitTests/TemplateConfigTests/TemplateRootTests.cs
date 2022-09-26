// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class TemplateRootTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public TemplateRootTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
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
            TemplateConfigModel baseConfig = TemplateConfigModel.FromJObject(JObject.Parse(BasicTemplateConfig));
            RunnableProjectGenerator generator = new();

            string sourcePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { pathToTemplateJson, BasicTemplateConfig }
            };
            _engineEnvironmentSettings.WriteTemplateSource(sourcePath, templateSourceFiles);

            using IMountPoint mountPoint = _engineEnvironmentSettings.MountPath(sourcePath);
            IFile? templateConfigFile = mountPoint.FileInfo(pathToTemplateJson);
            Assert.NotNull(templateConfigFile);
            RunnableProjectConfig templateModel = new RunnableProjectConfig(_engineEnvironmentSettings, generator, baseConfig, templateConfigFile);

            if (shouldAllPathsBeValid)
            {
                Assert.Empty(templateModel.ValidateTemplateSourcePaths());
            }
            else
            {
                Assert.NotEmpty(templateModel.ValidateTemplateSourcePaths());
            }
        }

        // Tests source paths when the mount point root is the same as the template root.
        [Theory(DisplayName = nameof(CheckTemplateSourcesRelativeToTemplateRoot))]
        [InlineData(true, "things/")]
        [InlineData(true, "things/stuff/")]
        [InlineData(true, "./")]
        [InlineData(false, "../")] // outside the mount point, combining throws and is caught.
        [InlineData(false, "foo/")] // not valid because the path doesn't exist under the root.
        public void CheckTemplateSourcesRelativeToTemplateRoot(bool shouldAllPathsBeValid, string source)
        {
            string templateConfig = string.Format(TemplateConfigWithSourcePlaceholder, source);
            TemplateConfigModel baseConfig = TemplateConfigModel.FromJObject(JObject.Parse(templateConfig));
            RunnableProjectGenerator generator = new RunnableProjectGenerator();

            const string pathToTemplateConfig = ".template.config/template.json";
            string sourcePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { pathToTemplateConfig, templateConfig },
                { "things/stuff/_._", string.Empty } // directories under the root - valid source locations.
            };
            _engineEnvironmentSettings.WriteTemplateSource(sourcePath, templateSourceFiles);

            using IMountPoint mountPoint = _engineEnvironmentSettings.MountPath(sourcePath);
            IFile? templateConfigFile = mountPoint.FileInfo(pathToTemplateConfig);
            Assert.NotNull(templateConfigFile);

            RunnableProjectConfig templateModel = new RunnableProjectConfig(_engineEnvironmentSettings, generator, baseConfig, templateConfigFile);

            if (shouldAllPathsBeValid)
            {
                Assert.Empty(templateModel.ValidateTemplateSourcePaths());
            }
            else
            {
                Assert.NotEmpty(templateModel.ValidateTemplateSourcePaths());
            }
        }

        [Theory(DisplayName = nameof(CheckTemplateSourcesRelativeToTemplateRootMultipleDirsUnderMountPoint))]
        [InlineData(true, "things/")]
        [InlineData(true, "things/stuff/")]
        [InlineData(true, "./")]
        [InlineData(true, "../")] // outside the template root, but in the mount point
        [InlineData(true, "../../")] // outside the template root, but in the mount point
        [InlineData(true, "../../../")] // outside the template root, but at the mount point root
        [InlineData(false, "../../../../")] // outside the mount point
        [InlineData(false, "foo/")] // not valid because the path doesn't exist under the root
        [InlineData(false, "../../../Other/")] // doesn't exist
        [InlineData(false, "../../../../Other/")] // outside the mount point
        [InlineData(true, "../../../MountRoot/")]
        [InlineData(false, "../../../MountRoot/Other")] // directory doesn't exist
        [InlineData(true, "../../../ExistingDir/")]
        [InlineData(true, "../../../MountRoot/Subdir")]
        public void CheckTemplateSourcesRelativeToTemplateRootMultipleDirsUnderMountPoint(bool shouldAllPathsBeValid, string source)
        {
            string templateConfig = string.Format(TemplateConfigWithSourcePlaceholder, source);
            TemplateConfigModel baseConfig = TemplateConfigModel.FromJObject(JObject.Parse(templateConfig));
            RunnableProjectGenerator generator = new RunnableProjectGenerator();

            const string pathFromMountPointRootToTemplateRoot = "MountRoot/Stuff/TemplateRoot/";
            string pathToTemplateConfig = pathFromMountPointRootToTemplateRoot + ".template.config/template.json";

            string sourcePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            string sampleContentDir = pathFromMountPointRootToTemplateRoot + "things/stuff/_._";

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { pathToTemplateConfig, templateConfig },
                { sampleContentDir, string.Empty },    // directories under the template root - valid source locations.
                { "ExistingDir/_._", string.Empty },
                { "MountRoot/Subdir/_._", string.Empty }
            };

            _engineEnvironmentSettings.WriteTemplateSource(sourcePath, templateSourceFiles);

            using IMountPoint mountPoint = _engineEnvironmentSettings.MountPath(sourcePath);
            IFile? templateConfigFile = mountPoint.FileInfo(pathToTemplateConfig);
            Assert.NotNull(templateConfigFile);

            RunnableProjectConfig templateModel = new RunnableProjectConfig(_engineEnvironmentSettings, generator, baseConfig, templateConfigFile);

            if (shouldAllPathsBeValid)
            {
                Assert.Empty(templateModel.ValidateTemplateSourcePaths());
            }
            else
            {
                Assert.NotEmpty(templateModel.ValidateTemplateSourcePaths());
            }
        }
    }
}
