// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class SourceConfigTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;

        public SourceConfigTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: false);
        }

        private static string ExcludesWithIncludeModifierConfigText
        {
            get
            {
                string configString = @"
{
  ""sources"": [
    {
      ""exclude"": [ ""**/*.config"" ],
      ""modifiers"": [
        {
          ""include"": [""core.config""]
        }
      ]
    }
  ]
}";
                return configString;
            }
        }

        private static string CopyOnlyWithoutCorrespondingIncludeConfigText
        {
            get
            {
                string configString = @"
{
  ""sources"": [
    {
      ""include"": [ ""**/*.txt"" ],
      ""modifiers"": [
        {
          ""copyOnly"": [""copy.me""]
        },
      ]
    }
  ]
}";
                return configString;
            }
        }

        private static string CopyOnlyWithIncludeInParentConfigText
        {
            get
            {
                string configString = @"
{
  ""sources"": [
    {
      ""include"": [""**/*.me""],
      ""modifiers"": [
        {
          ""copyOnly"": [""copy.me""]
        }
      ]
    }
  ]
}
";
                return configString;
            }
        }

        private static string CopyOnlyWithWildcardAndParentIncludeConfigText
        {
            get
            {
                string configString = @"
{
  ""sources"": [
    {
      ""include"": [""*copy.me""],
      ""modifiers"": [
        {
          ""copyOnly"": [""**/*.me""]
        }
      ]
    }
  ]
}
";
                return configString;
            }
        }

        private static string IncludeModifierOverridesPreviousExcludeModifierConfigText
        {
            get
            {
                string configString = @"
{
  ""sources"": [
    {
      // use the default ICE
      ""modifiers"": [
        {
          ""exclude"": [""*.xyz""]
        },
        {
          ""include"": [""include.xyz""]
        }
      ]
    }
  ]
}
";
                return configString;
            }
        }

        private static string ExcludeModifierOverridesPreviousIncludeModifierConfigText
        {
            get
            {
                string configString = @"
{
  ""sources"": [
    {
      // use the default ICE
      ""modifiers"": [
        {
          ""include"": [""*.xyz""]
        },
        {
          ""exclude"": [""exclude.xyz""]
        }
      ]
    }
  ]
}
";
                return configString;
            }
        }

        [Fact(DisplayName = nameof(SourceConfigExcludesAreOverriddenByIncludes))]
        public void SourceConfigExcludesAreOverriddenByIncludes()
        {
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);

            TestTemplateSetup setup = SetupTwoFilesWithConfigExtensionTemplate(_engineEnvironmentSettings, sourceBasePath);
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);
            setup.InstantiateTemplate(targetDir);

            Assert.True(_engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(targetDir, "core.config")));
            Assert.False(_engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(targetDir, "full.config")));
        }

        [Fact(DisplayName = nameof(CopyOnlyWithoutIncludeDoesntActuallyCopyFile))]
        public void CopyOnlyWithoutIncludeDoesntActuallyCopyFile()
        {
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);

            TestTemplateSetup setup = SetupCopyOnlyWithoutCorrespondingIncludeTemplate(_engineEnvironmentSettings, sourceBasePath);
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);
            IReadOnlyDictionary<string, IReadOnlyList<IFileChange2>> allChanges = setup.GetFileChanges(targetDir);

            // one source, should cause one set of changes
            Assert.Equal(1, allChanges.Count);

            if (!allChanges.TryGetValue("./", out IReadOnlyList<IFileChange2> changes))
            {
                Assert.True(false, "no changes for source './'");
            }

            Assert.Equal(1, changes.Count);
            Assert.Equal(ChangeKind.Create, changes[0].ChangeKind);
            Assert.True(string.Equals(changes[0].TargetRelativePath, "something.txt"), "didn't copy the correct file");
        }

        [Fact(DisplayName = nameof(CopyOnlyWithParentIncludeActuallyCopiesFile))]
        public void CopyOnlyWithParentIncludeActuallyCopiesFile()
        {
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);

            TestTemplateSetup setup = SetupCopyOnlyWithParentInclude(_engineEnvironmentSettings, sourceBasePath);
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);
            IReadOnlyDictionary<string, IReadOnlyList<IFileChange2>> allChanges = setup.GetFileChanges(targetDir);

            Assert.Equal(1, allChanges.Count);

            if (!allChanges.TryGetValue("./", out IReadOnlyList<IFileChange2> changes))
            {
                Assert.True(false, "no changes for source './'");
            }

            Assert.Equal(1, changes.Count);
            Assert.Equal(ChangeKind.Create, changes[0].ChangeKind);
            Assert.True(string.Equals(changes[0].TargetRelativePath, "copy.me"), "didn't copy the correct file");
        }

        [Fact(DisplayName = nameof(CopyOnlyWithWildcardAndParentIncludeActuallyCopiesFile))]
        public void CopyOnlyWithWildcardAndParentIncludeActuallyCopiesFile()
        {
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);

            TestTemplateSetup setup = SetupCopyOnlyWithWildcardAndParentInclude(_engineEnvironmentSettings, sourceBasePath);
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);
            IReadOnlyDictionary<string, IReadOnlyList<IFileChange2>> allChanges = setup.GetFileChanges(targetDir);

            Assert.Equal(1, allChanges.Count);

            if (!allChanges.TryGetValue("./", out IReadOnlyList<IFileChange2> changes))
            {
                Assert.True(false, "no changes for source './'");
            }

            Assert.Equal(1, changes.Count);
            Assert.Equal(ChangeKind.Create, changes[0].ChangeKind);
            Assert.True(string.Equals(changes[0].TargetRelativePath, "copy.me"), "didn't copy the correct file");
        }

        [Fact(DisplayName = nameof(IncludeModifierOverridesPreviousExcludeModifierTemplateTest))]
        public void IncludeModifierOverridesPreviousExcludeModifierTemplateTest()
        {
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);

            TestTemplateSetup setup = SetupXYZFilesForModifierOverrideTestsTemplate(_engineEnvironmentSettings, sourceBasePath, IncludeModifierOverridesPreviousExcludeModifierConfigText);
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);
            IReadOnlyDictionary<string, IReadOnlyList<IFileChange2>> allChanges = setup.GetFileChanges(targetDir);

            Assert.Equal(1, allChanges.Count);

            if (!allChanges.TryGetValue("./", out IReadOnlyList<IFileChange2> changes))
            {
                Assert.True(false, "no changes for source './'");
            }

            Assert.Equal(1, changes.Count);
            Assert.Equal(ChangeKind.Create, changes[0].ChangeKind);
            Assert.True(string.Equals(changes[0].TargetRelativePath, "include.xyz"), "include modifier didn't properly override exclude modifier");
        }

        [Fact(DisplayName = nameof(ExcludeModifierOverridesPreviousIncludeModifierTemplateTest))]
        public void ExcludeModifierOverridesPreviousIncludeModifierTemplateTest()
        {
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);

            TestTemplateSetup setup = SetupXYZFilesForModifierOverrideTestsTemplate(_engineEnvironmentSettings, sourceBasePath, ExcludeModifierOverridesPreviousIncludeModifierConfigText);
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);
            IReadOnlyDictionary<string, IReadOnlyList<IFileChange2>> allChanges = setup.GetFileChanges(targetDir);

            Assert.Equal(1, allChanges.Count);

            if (!allChanges.TryGetValue("./", out IReadOnlyList<IFileChange2> changes))
            {
                Assert.True(false, "no changes for source './'");
            }

            Assert.Equal(2, changes.Count);

            IFileChange2 includeXyzChangeInfo = changes.FirstOrDefault(x => string.Equals(x.TargetRelativePath, "include.xyz"));
            Assert.NotNull(includeXyzChangeInfo);
            Assert.Equal(ChangeKind.Create, includeXyzChangeInfo.ChangeKind);

            IFileChange2 otherXyzChangeInfo = changes.FirstOrDefault(x => string.Equals(x.TargetRelativePath, "other.xyz"));
            Assert.NotNull(otherXyzChangeInfo);
            Assert.Equal(ChangeKind.Create, otherXyzChangeInfo.ChangeKind);
        }

        private static TestTemplateSetup SetupTwoFilesWithConfigExtensionTemplate(IEngineEnvironmentSettings environment, string basePath)
        {
            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            // config
            templateSourceFiles.Add(TemplateConfigTestHelpers.DefaultConfigRelativePath, ExcludesWithIncludeModifierConfigText);
            // content
            templateSourceFiles.Add("core.config", null);
            templateSourceFiles.Add("full.config", null);
            TestTemplateSetup setup = new TestTemplateSetup(environment, basePath, templateSourceFiles);
            setup.WriteSource();
            return setup;
        }

        private static TestTemplateSetup SetupCopyOnlyWithoutCorrespondingIncludeTemplate(IEngineEnvironmentSettings environment, string basePath)
        {
            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            templateSourceFiles.Add(TemplateConfigTestHelpers.DefaultConfigRelativePath, CopyOnlyWithoutCorrespondingIncludeConfigText);
            templateSourceFiles.Add("something.txt", null);
            templateSourceFiles.Add("copy.me", null);
            TestTemplateSetup setup = new TestTemplateSetup(environment, basePath, templateSourceFiles);
            setup.WriteSource();
            return setup;
        }

        private static TestTemplateSetup SetupCopyOnlyWithParentInclude(IEngineEnvironmentSettings environment, string basePath)
        {
            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            templateSourceFiles.Add(TemplateConfigTestHelpers.DefaultConfigRelativePath, CopyOnlyWithIncludeInParentConfigText);
            templateSourceFiles.Add("copy.me", null);
            TestTemplateSetup setup = new TestTemplateSetup(environment, basePath, templateSourceFiles);
            setup.WriteSource();
            return setup;
        }

        private static TestTemplateSetup SetupCopyOnlyWithWildcardAndParentInclude(IEngineEnvironmentSettings environment, string basePath)
        {
            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            templateSourceFiles.Add(TemplateConfigTestHelpers.DefaultConfigRelativePath, CopyOnlyWithWildcardAndParentIncludeConfigText);
            templateSourceFiles.Add("copy.me", null);
            TestTemplateSetup setup = new TestTemplateSetup(environment, basePath, templateSourceFiles);
            setup.WriteSource();
            return setup;
        }

        private static TestTemplateSetup SetupXYZFilesForModifierOverrideTestsTemplate(IEngineEnvironmentSettings environment, string basePath, string templateConfig)
        {
            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            templateSourceFiles.Add(TemplateConfigTestHelpers.DefaultConfigRelativePath, templateConfig);
            templateSourceFiles.Add("other.xyz", null);
            templateSourceFiles.Add("include.xyz", null);
            templateSourceFiles.Add("exclude.xyz", null);
            TestTemplateSetup setup = new TestTemplateSetup(environment, basePath, templateSourceFiles);
            setup.WriteSource();
            return setup;
        }
    }
}
