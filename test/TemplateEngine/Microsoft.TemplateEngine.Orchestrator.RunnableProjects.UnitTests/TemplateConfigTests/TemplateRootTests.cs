// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class TemplateRootTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;
        public TemplateRootTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        private static string TemplateConfigWithSourcePlaceholder
        {
            get
            {
                string templateJsonString = @"
{{
  ""author"": ""Microsoft"",
  ""classifications"": [""Test""],
  ""name"": ""Test Template"",
  ""generatorVersions"": ""[1.0.0.0-*)"",
  ""groupIdentity"": ""Testing.TemplateRoot"",
  ""identity"": ""Testing.Template.Root.CSharp"",
  ""shortName"": ""templateRootTest"",
  ""sourceName"": ""Company.ConsoleApplication1"",
  ""preferNameDirectory"": true,
  ""sources"": [
      {{
        ""source"": ""{0}""
      }}
  ]
}}";
                return templateJsonString;
            }
        }

        private static string BasicTemplateConfig
        {
            get
            {
                string templateJsonString = @"
{
  ""author"": ""Microsoft"",
  ""classifications"": [""Test""],
  ""name"": ""Test Template"",
  ""generatorVersions"": ""[1.0.0.0-*)"",
  ""groupIdentity"": ""Testing.TemplateRoot"",
  ""identity"": ""Testing.Template.Root.CSharp"",
  ""shortName"": ""templateRootTest"",
  ""sourceName"": ""Company.ConsoleApplication1"",
  ""preferNameDirectory"": true,
}";
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
            string sourcePath = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);
            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            templateSourceFiles.Add(pathToTemplateJson, BasicTemplateConfig);
            TestTemplateSetup setup = new TestTemplateSetup(_engineEnvironmentSettings, sourcePath, templateSourceFiles);
            setup.WriteSource();

            RunnableProjectGenerator generator = new RunnableProjectGenerator();

            IFile templateFile = setup.FileInfoForSourceFile(pathToTemplateJson);
            JObject srcObject = generator.ReadJObjectFromIFile(templateFile);
            SimpleConfigModel templateModel = SimpleConfigModel.FromJObject(templateFile.MountPoint.EnvironmentSettings, srcObject);
            RunnableProjectTemplate runnableProjectTemplate = new RunnableProjectTemplate(srcObject, generator, templateFile, templateModel, null, null);

            bool allPathsAreValid = generator.AreAllTemplatePathsValid(templateModel, runnableProjectTemplate);
            Assert.Equal(shouldAllPathsBeValid, allPathsAreValid);
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
            const string pathToTemplateConfig = ".template.config/template.json";
            string sourcePath = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);
            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();

            string templateConfig = string.Format(TemplateConfigWithSourcePlaceholder, source);
            templateSourceFiles.Add(pathToTemplateConfig, templateConfig);
            templateSourceFiles.Add("things/stuff/_._", "");    // directories under the root - valid source locations.
            TestTemplateSetup setup = new TestTemplateSetup(_engineEnvironmentSettings, sourcePath, templateSourceFiles);
            setup.WriteSource();

            RunnableProjectGenerator generator = new RunnableProjectGenerator();

            IFile templateFile = setup.FileInfoForSourceFile(pathToTemplateConfig);
            JObject srcObject = generator.ReadJObjectFromIFile(templateFile);
            SimpleConfigModel templateModel = SimpleConfigModel.FromJObject(templateFile.MountPoint.EnvironmentSettings, srcObject);
            RunnableProjectTemplate runnableProjectTemplate = new RunnableProjectTemplate(srcObject, generator, templateFile, templateModel, null, null);

            bool allPathsAreValid = generator.AreAllTemplatePathsValid(templateModel, runnableProjectTemplate);
            Assert.Equal(shouldAllPathsBeValid, allPathsAreValid);
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
            const string pathFromMountPointRootToTemplateRoot = "MountRoot/Stuff/TemplateRoot/";
            string pathToTemplateConfig = pathFromMountPointRootToTemplateRoot + ".template.config/template.json";

            string sourcePath = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);
            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();

            string templateConfig = string.Format(TemplateConfigWithSourcePlaceholder, source);
            templateSourceFiles.Add(pathToTemplateConfig, templateConfig);

            string sampleContentDir = pathFromMountPointRootToTemplateRoot + "things/stuff/_._";
            templateSourceFiles.Add(sampleContentDir, "");    // directories under the template root - valid source locations.
            templateSourceFiles.Add("ExistingDir/_._", "");
            templateSourceFiles.Add("MountRoot/Subdir/_._", "");
            TestTemplateSetup setup = new TestTemplateSetup(_engineEnvironmentSettings, sourcePath, templateSourceFiles);
            setup.WriteSource();

            RunnableProjectGenerator generator = new RunnableProjectGenerator();

            IFile templateFile = setup.FileInfoForSourceFile(pathToTemplateConfig);
            JObject srcObject = generator.ReadJObjectFromIFile(templateFile);
            SimpleConfigModel templateModel = SimpleConfigModel.FromJObject(templateFile.MountPoint.EnvironmentSettings, srcObject);
            RunnableProjectTemplate runnableProjectTemplate = new RunnableProjectTemplate(srcObject, generator, templateFile, templateModel, null, null);

            bool allPathsAreValid = generator.AreAllTemplatePathsValid(templateModel, runnableProjectTemplate);
            Assert.Equal(shouldAllPathsBeValid, allPathsAreValid);
        }
    }
}
