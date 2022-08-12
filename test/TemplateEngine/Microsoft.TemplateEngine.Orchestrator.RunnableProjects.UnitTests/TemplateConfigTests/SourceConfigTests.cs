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

        [Fact(DisplayName = nameof(SourceConfigExcludesAreOverriddenByIncludes))]
        public void SourceConfigExcludesAreOverriddenByIncludes()
        {
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);
            TemplateConfigModel config = new TemplateConfigModel()
            {
                Identity = "test",
                Sources = new List<ExtendedFileSource>()
                {
                    new ExtendedFileSource()
                    {
                        Exclude = "**/*.config",
                        Modifiers = new List<SourceModifier>()
                        {
                            new SourceModifier()
                            {
                                Include = "core.config"

                            }
                        }
                    }
                }
            };

            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            // config
            templateSourceFiles.Add(TestFileSystemHelper.DefaultConfigRelativePath, config.ToJObject().ToString());
            // content
            templateSourceFiles.Add("core.config", null);
            templateSourceFiles.Add("full.config", null);
            TestTemplateSetup setup = new TestTemplateSetup(_engineEnvironmentSettings, sourceBasePath, templateSourceFiles, config);
            setup.WriteSource();

            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);
            setup.InstantiateTemplate(targetDir);

            Assert.True(_engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(targetDir, "core.config")));
            Assert.False(_engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(targetDir, "full.config")));
        }

        [Fact(DisplayName = nameof(CopyOnlyWithoutIncludeDoesntActuallyCopyFile))]
        public void CopyOnlyWithoutIncludeDoesntActuallyCopyFile()
        {
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);

            TemplateConfigModel config = new TemplateConfigModel()
            {
                Identity = "test",
                Sources = new List<ExtendedFileSource>()
                {
                    new ExtendedFileSource()
                    {
                        Include = "**/*.txt",
                        Modifiers = new List<SourceModifier>()
                        {
                            new SourceModifier()
                            {
                                CopyOnly = "copy.me"
                            }
                        }
                    }
                }
            };

            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            templateSourceFiles.Add(TestFileSystemHelper.DefaultConfigRelativePath, config.ToJObject().ToString());
            templateSourceFiles.Add("something.txt", null);
            templateSourceFiles.Add("copy.me", null);
            TestTemplateSetup setup = new TestTemplateSetup(_engineEnvironmentSettings, sourceBasePath, templateSourceFiles, config);
            setup.WriteSource();

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
            TemplateConfigModel config = new TemplateConfigModel()
            {
                Identity = "test",
                Sources = new List<ExtendedFileSource>()
                {
                    new ExtendedFileSource()
                    {
                        Include = "**/*.me",
                        Modifiers = new List<SourceModifier>()
                        {
                            new SourceModifier()
                            {
                                CopyOnly = "copy.me"
                            }
                        }
                    }
                }
            };

            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            templateSourceFiles.Add(TestFileSystemHelper.DefaultConfigRelativePath, config.ToJObject().ToString());
            templateSourceFiles.Add("copy.me", null);
            TestTemplateSetup setup = new TestTemplateSetup(_engineEnvironmentSettings, sourceBasePath, templateSourceFiles, config);
            setup.WriteSource();

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
            TemplateConfigModel config = new TemplateConfigModel()
            {
                Identity = "test",
                Sources = new List<ExtendedFileSource>()
                {
                    new ExtendedFileSource()
                    {
                        Include = "*copy.me",
                        Modifiers = new List<SourceModifier>()
                        {
                            new SourceModifier()
                            {
                                CopyOnly = "**/*.me"
                            }
                        }
                    }
                }
            };

            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            templateSourceFiles.Add(TestFileSystemHelper.DefaultConfigRelativePath, config.ToJObject().ToString());
            templateSourceFiles.Add("copy.me", null);
            TestTemplateSetup setup = new TestTemplateSetup(_engineEnvironmentSettings, sourceBasePath, templateSourceFiles, config);
            setup.WriteSource();

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
            TemplateConfigModel config = new TemplateConfigModel()
            {
                Identity = "test",
                Sources = new List<ExtendedFileSource>()
                {
                    new ExtendedFileSource()
                    {
                        Modifiers = new List<SourceModifier>()
                        {
                            new SourceModifier()
                            {
                                Exclude = "*.xyz",
                            },
                            new SourceModifier()
                            {
                                Include = "include.xyz"
                            }
                        }
                    }
                }
            };

            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            templateSourceFiles.Add(TestFileSystemHelper.DefaultConfigRelativePath, config.ToJObject().ToString());
            templateSourceFiles.Add("other.xyz", null);
            templateSourceFiles.Add("include.xyz", null);
            templateSourceFiles.Add("exclude.xyz", null);
            TestTemplateSetup setup = new TestTemplateSetup(_engineEnvironmentSettings, sourceBasePath, templateSourceFiles, config);
            setup.WriteSource();

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

            TemplateConfigModel config = new TemplateConfigModel()
            {
                Identity = "test",
                Sources = new List<ExtendedFileSource>()
                {
                    new ExtendedFileSource()
                    {
                        Modifiers = new List<SourceModifier>()
                        {
                            new SourceModifier()
                            {
                                Include = "*.xyz"
                            },
                            new SourceModifier()
                            {
                                Exclude = "exclude.xyz",
                            },
                        }
                    }
                }
            };

            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            templateSourceFiles.Add(TestFileSystemHelper.DefaultConfigRelativePath, config.ToJObject().ToString());
            templateSourceFiles.Add("other.xyz", null);
            templateSourceFiles.Add("include.xyz", null);
            templateSourceFiles.Add("exclude.xyz", null);
            TestTemplateSetup setup = new TestTemplateSetup(_engineEnvironmentSettings, sourceBasePath, templateSourceFiles, config);
            setup.WriteSource();

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
    }
}
