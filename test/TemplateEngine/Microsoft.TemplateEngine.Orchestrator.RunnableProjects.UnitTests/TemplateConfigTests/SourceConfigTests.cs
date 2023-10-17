// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.Serialization;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class SourceConfigTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public SourceConfigTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: false);
        }

        [Fact]
        public async Task SourceConfigExcludesAreOverriddenByIncludes()
        {
            string sourceBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            TemplateConfigModel config = new("test")
            {
                Name = "test",
                ShortNameList = new[] { "test" },
                Sources = new List<ExtendedFileSource>()
                {
                    new ExtendedFileSource()
                    {
                        Exclude = new[] { "**/*.config" },
                        Modifiers = new List<SourceModifier>()
                        {
                            new SourceModifier()
                            {
                                Include = new[] { "core.config" }
                            }
                        }
                    }
                }
            };

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // config
                { TestFileSystemUtils.DefaultConfigRelativePath, config.ToJsonString() },
                // content
                { "core.config", null },
                { "full.config", null }
            };

            _engineEnvironmentSettings.WriteTemplateSource(sourceBasePath, templateSourceFiles);
            string targetDir = _engineEnvironmentSettings.GetTempVirtualizedPath();

            RunnableProjectGenerator generator = new();

            using IMountPoint mountPoint = _engineEnvironmentSettings.MountPath(sourceBasePath);
            IFile? templateConfigFile = mountPoint.FileInfo(TestFileSystemUtils.DefaultConfigRelativePath);
            Assert.NotNull(templateConfigFile);

            using ITemplate template = new RunnableProjectConfig(_engineEnvironmentSettings, generator, templateConfigFile);
            ParameterSetData parameters = new(template);

            ICreationResult result = await (generator as IGenerator).CreateAsync(_engineEnvironmentSettings, template, parameters, targetDir, default);
            Assert.True(_engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(targetDir, "core.config")));
            Assert.False(_engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(targetDir, "full.config")));
        }

        [Fact]
        public async Task CopyOnlyWithoutIncludeDoesntActuallyCopyFile()
        {
            string sourceBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            TemplateConfigModel config = new("test")
            {
                Name = "test",
                ShortNameList = new[] { "test" },
                Sources = new List<ExtendedFileSource>()
                {
                    new ExtendedFileSource()
                    {
                        Include = new[] { "**/*.txt" },
                        Modifiers = new List<SourceModifier>()
                        {
                            new SourceModifier()
                            {
                                CopyOnly = new[] { "copy.me" },
                            }
                        }
                    }
                }
            };

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { TestFileSystemUtils.DefaultConfigRelativePath, config.ToJsonString() },
                { "something.txt", null },
                { "copy.me", null }
            };
            _engineEnvironmentSettings.WriteTemplateSource(sourceBasePath, templateSourceFiles);
            string targetDir = _engineEnvironmentSettings.GetTempVirtualizedPath();

            RunnableProjectGenerator generator = new();

            using IMountPoint mountPoint = _engineEnvironmentSettings.MountPath(sourceBasePath);
            IFile? templateConfigFile = mountPoint.FileInfo(TestFileSystemUtils.DefaultConfigRelativePath);
            Assert.NotNull(templateConfigFile);

            using ITemplate template = new RunnableProjectConfig(_engineEnvironmentSettings, generator, templateConfigFile);
            ParameterSetData parameters = new(template);

            ICreationEffects result = await (generator as IGenerator).GetCreationEffectsAsync(_engineEnvironmentSettings, template, parameters, targetDir, default);
            IEnumerable<IFileChange2> changes = result.FileChanges.Cast<IFileChange2>();
            Assert.All(result.FileChanges.Cast<IFileChange2>(), c => c.SourceRelativePath.StartsWith("./"));

            Assert.Single(result.FileChanges);

            Assert.Equal(ChangeKind.Create, result.FileChanges.Single().ChangeKind);
            Assert.True(string.Equals(result.FileChanges.Single().TargetRelativePath, "./something.txt"), "didn't copy the correct file");
        }

        [Fact]
        public async Task CopyOnlyWithParentIncludeActuallyCopiesFile()
        {
            string sourceBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            TemplateConfigModel config = new TemplateConfigModel("test")
            {
                Name = "test",
                ShortNameList = new[] { "test" },
                Sources = new List<ExtendedFileSource>()
                {
                    new ExtendedFileSource()
                    {
                        Include = new[] { "**/*.me" },
                        Modifiers = new List<SourceModifier>()
                        {
                            new SourceModifier()
                            {
                                CopyOnly = new[] { "copy.me" }
                            }
                        }
                    }
                }
            };

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { TestFileSystemUtils.DefaultConfigRelativePath, config.ToJsonString() },
                { "copy.me", null }
            };
            _engineEnvironmentSettings.WriteTemplateSource(sourceBasePath, templateSourceFiles);

            string targetDir = _engineEnvironmentSettings.GetTempVirtualizedPath();
            RunnableProjectGenerator generator = new();

            using IMountPoint mountPoint = _engineEnvironmentSettings.MountPath(sourceBasePath);
            IFile? templateConfigFile = mountPoint.FileInfo(TestFileSystemUtils.DefaultConfigRelativePath);
            Assert.NotNull(templateConfigFile);

            using ITemplate template = new RunnableProjectConfig(_engineEnvironmentSettings, generator, templateConfigFile);
            ParameterSetData parameters = new(template);

            ICreationEffects result = await (generator as IGenerator).GetCreationEffectsAsync(_engineEnvironmentSettings, template, parameters, targetDir, default);
            IEnumerable<IFileChange2> changes = result.FileChanges.Cast<IFileChange2>();
            Assert.All(result.FileChanges.Cast<IFileChange2>(), c => c.SourceRelativePath.StartsWith("./"));

            Assert.Single(result.FileChanges);
            Assert.Equal(ChangeKind.Create, result.FileChanges.Single().ChangeKind);
            Assert.True(string.Equals(result.FileChanges.Single().TargetRelativePath, "./copy.me"), "didn't copy the correct file");
        }

        [Fact]
        public async Task CopyOnlyWithWildcardAndParentIncludeActuallyCopiesFile()
        {
            string sourceBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            TemplateConfigModel config = new TemplateConfigModel("test")
            {
                Name = "test",
                ShortNameList = new[] { "test" },
                Sources = new List<ExtendedFileSource>()
                {
                    new ExtendedFileSource()
                    {
                        Include = new[] { "*copy.me" },
                        Modifiers = new List<SourceModifier>()
                        {
                            new SourceModifier()
                            {
                                CopyOnly = new[] { "**/*.me" }
                            }
                        }
                    }
                }
            };

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { TestFileSystemUtils.DefaultConfigRelativePath, config.ToJsonString() },
                { "copy.me", null }
            };
            _engineEnvironmentSettings.WriteTemplateSource(sourceBasePath, templateSourceFiles);

            string targetDir = _engineEnvironmentSettings.GetTempVirtualizedPath();
            RunnableProjectGenerator generator = new();

            using IMountPoint mountPoint = _engineEnvironmentSettings.MountPath(sourceBasePath);
            IFile? templateConfigFile = mountPoint.FileInfo(TestFileSystemUtils.DefaultConfigRelativePath);
            Assert.NotNull(templateConfigFile);

            using ITemplate template = new RunnableProjectConfig(_engineEnvironmentSettings, generator, templateConfigFile);
            ParameterSetData parameters = new(template);

            ICreationEffects result = await (generator as IGenerator).GetCreationEffectsAsync(_engineEnvironmentSettings, template, parameters, targetDir, default);
            IEnumerable<IFileChange2> changes = result.FileChanges.Cast<IFileChange2>();
            Assert.All(result.FileChanges.Cast<IFileChange2>(), c => c.SourceRelativePath.StartsWith("./"));

            Assert.Single(result.FileChanges);
            Assert.Equal(ChangeKind.Create, result.FileChanges.Single().ChangeKind);
            Assert.True(string.Equals(result.FileChanges.Single().TargetRelativePath, "./copy.me"), "didn't copy the correct file");
        }

        [Fact]
        public async Task IncludeModifierOverridesPreviousExcludeModifierTemplateTest()
        {
            string sourceBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            TemplateConfigModel config = new TemplateConfigModel("test")
            {
                Name = "test",
                ShortNameList = new[] { "test" },
                Sources = new List<ExtendedFileSource>()
                {
                    new ExtendedFileSource()
                    {
                        Modifiers = new List<SourceModifier>()
                        {
                            new SourceModifier()
                            {
                                Exclude = new[] { "*.xyz" }
                            },
                            new SourceModifier()
                            {
                                Include = new[] { "include.xyz" }
                            }
                        }
                    }
                }
            };

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { TestFileSystemUtils.DefaultConfigRelativePath, config.ToJsonString() },
                { "other.xyz", null },
                { "include.xyz", null },
                { "exclude.xyz", null }
            };
            _engineEnvironmentSettings.WriteTemplateSource(sourceBasePath, templateSourceFiles);

            string targetDir = _engineEnvironmentSettings.GetTempVirtualizedPath();
            RunnableProjectGenerator generator = new();

            using IMountPoint mountPoint = _engineEnvironmentSettings.MountPath(sourceBasePath);
            IFile? templateConfigFile = mountPoint.FileInfo(TestFileSystemUtils.DefaultConfigRelativePath);
            Assert.NotNull(templateConfigFile);

            using ITemplate template = new RunnableProjectConfig(_engineEnvironmentSettings, generator, templateConfigFile);
            ParameterSetData parameters = new(template);

            ICreationEffects result = await (generator as IGenerator).GetCreationEffectsAsync(_engineEnvironmentSettings, template, parameters, targetDir, default);
            IEnumerable<IFileChange2> changes = result.FileChanges.Cast<IFileChange2>();
            Assert.All(result.FileChanges.Cast<IFileChange2>(), c => c.SourceRelativePath.StartsWith("./"));

            Assert.Single(result.FileChanges);
            Assert.Equal(ChangeKind.Create, result.FileChanges.Single().ChangeKind);
            Assert.True(string.Equals(result.FileChanges.Single().TargetRelativePath, "./include.xyz"), "include modifier didn't properly override exclude modifier");
        }

        [Fact]
        public async Task ExcludeModifierOverridesPreviousIncludeModifierTemplateTest()
        {
            string sourceBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            TemplateConfigModel config = new TemplateConfigModel("test")
            {
                Name = "test",
                ShortNameList = new[] { "test" },
                Sources = new List<ExtendedFileSource>()
                {
                    new ExtendedFileSource()
                    {
                        Modifiers = new List<SourceModifier>()
                        {
                            new SourceModifier()
                            {
                                Include = new[] { "*.xyz" }
                            },
                            new SourceModifier()
                            {
                                Exclude = new[] { "exclude.xyz" }
                            },
                        }
                    }
                }
            };

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                { TestFileSystemUtils.DefaultConfigRelativePath, config.ToJsonString() },
                { "other.xyz", null },
                { "include.xyz", null },
                { "exclude.xyz", null }
            };
            _engineEnvironmentSettings.WriteTemplateSource(sourceBasePath, templateSourceFiles);

            string targetDir = _engineEnvironmentSettings.GetTempVirtualizedPath();
            RunnableProjectGenerator generator = new();

            using IMountPoint mountPoint = _engineEnvironmentSettings.MountPath(sourceBasePath);
            IFile? templateConfigFile = mountPoint.FileInfo(TestFileSystemUtils.DefaultConfigRelativePath);
            Assert.NotNull(templateConfigFile);

            using ITemplate template = new RunnableProjectConfig(_engineEnvironmentSettings, generator, templateConfigFile);
            ParameterSetData parameters = new(template);

            ICreationEffects result = await (generator as IGenerator).GetCreationEffectsAsync(_engineEnvironmentSettings, template, parameters, targetDir, default);
            IEnumerable<IFileChange2> changes = result.FileChanges.Cast<IFileChange2>();
            Assert.All(result.FileChanges.Cast<IFileChange2>(), c => c.SourceRelativePath.StartsWith("./"));

            Assert.Equal(2, result.FileChanges.Count);
            IFileChange2 includeXyzChangeInfo = changes.Single(x => string.Equals(x.TargetRelativePath, "./include.xyz"));
            Assert.Equal(ChangeKind.Create, includeXyzChangeInfo.ChangeKind);

            IFileChange2 otherXyzChangeInfo = changes.Single(x => string.Equals(x.TargetRelativePath, "./other.xyz"));
            Assert.Equal(ChangeKind.Create, otherXyzChangeInfo.ChangeKind);
        }
    }
}
