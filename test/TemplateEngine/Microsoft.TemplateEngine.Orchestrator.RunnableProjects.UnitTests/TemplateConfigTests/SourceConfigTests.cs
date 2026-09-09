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
    [TestClass]
    public class SourceConfigTests
    {
        public TestContext TestContext { get; set; } = null!;

        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public SourceConfigTests()
        {
            _engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: false);
        }

        [TestMethod]
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
            Assert.IsNotNull(templateConfigFile);

            using ITemplate template = new RunnableProjectConfig(_engineEnvironmentSettings, generator, templateConfigFile);
            ParameterSetData parameters = new(template);

            await (generator as IGenerator).CreateAsync(_engineEnvironmentSettings, template, parameters, targetDir, TestContext.CancellationToken);
            Assert.IsTrue(_engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(targetDir, "core.config")));
            Assert.IsFalse(_engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(targetDir, "full.config")));
        }

        [TestMethod]
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
            Assert.IsNotNull(templateConfigFile);

            using ITemplate template = new RunnableProjectConfig(_engineEnvironmentSettings, generator, templateConfigFile);
            ParameterSetData parameters = new(template);

            ICreationEffects result = await (generator as IGenerator).GetCreationEffectsAsync(_engineEnvironmentSettings, template, parameters, targetDir, TestContext.CancellationToken);
            foreach (var c in result.FileChanges.Cast<IFileChange2>())
            {
                Assert.StartsWith("./", c.SourceRelativePath);
            }

            Assert.ContainsSingle(result.FileChanges);

            Assert.AreEqual(ChangeKind.Create, result.FileChanges.Single().ChangeKind);
            Assert.IsTrue(string.Equals(result.FileChanges.Single().TargetRelativePath, "./something.txt"), "didn't copy the correct file");
        }

        [TestMethod]
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
            Assert.IsNotNull(templateConfigFile);

            using ITemplate template = new RunnableProjectConfig(_engineEnvironmentSettings, generator, templateConfigFile);
            ParameterSetData parameters = new(template);

            ICreationEffects result = await (generator as IGenerator).GetCreationEffectsAsync(_engineEnvironmentSettings, template, parameters, targetDir, TestContext.CancellationToken);
            foreach (var c in result.FileChanges.Cast<IFileChange2>())
            {
                Assert.StartsWith("./", c.SourceRelativePath);
            }

            Assert.ContainsSingle(result.FileChanges);
            Assert.AreEqual(ChangeKind.Create, result.FileChanges.Single().ChangeKind);
            Assert.IsTrue(string.Equals(result.FileChanges.Single().TargetRelativePath, "./copy.me"), "didn't copy the correct file");
        }

        [TestMethod]
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
            Assert.IsNotNull(templateConfigFile);

            using ITemplate template = new RunnableProjectConfig(_engineEnvironmentSettings, generator, templateConfigFile);
            ParameterSetData parameters = new(template);

            ICreationEffects result = await (generator as IGenerator).GetCreationEffectsAsync(_engineEnvironmentSettings, template, parameters, targetDir, TestContext.CancellationToken);
            foreach (var c in result.FileChanges.Cast<IFileChange2>())
            {
                Assert.StartsWith("./", c.SourceRelativePath);
            }

            Assert.ContainsSingle(result.FileChanges);
            Assert.AreEqual(ChangeKind.Create, result.FileChanges.Single().ChangeKind);
            Assert.IsTrue(string.Equals(result.FileChanges.Single().TargetRelativePath, "./copy.me"), "didn't copy the correct file");
        }

        [TestMethod]
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
            Assert.IsNotNull(templateConfigFile);

            using ITemplate template = new RunnableProjectConfig(_engineEnvironmentSettings, generator, templateConfigFile);
            ParameterSetData parameters = new(template);

            ICreationEffects result = await (generator as IGenerator).GetCreationEffectsAsync(_engineEnvironmentSettings, template, parameters, targetDir, TestContext.CancellationToken);
            foreach (var c in result.FileChanges.Cast<IFileChange2>())
            {
                Assert.StartsWith("./", c.SourceRelativePath);
            }

            Assert.ContainsSingle(result.FileChanges);
            Assert.AreEqual(ChangeKind.Create, result.FileChanges.Single().ChangeKind);
            Assert.IsTrue(string.Equals(result.FileChanges.Single().TargetRelativePath, "./include.xyz"), "include modifier didn't properly override exclude modifier");
        }

        [TestMethod]
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
            Assert.IsNotNull(templateConfigFile);

            using ITemplate template = new RunnableProjectConfig(_engineEnvironmentSettings, generator, templateConfigFile);
            ParameterSetData parameters = new(template);

            ICreationEffects result = await (generator as IGenerator).GetCreationEffectsAsync(_engineEnvironmentSettings, template, parameters, targetDir, TestContext.CancellationToken);
            IEnumerable<IFileChange2> changes = result.FileChanges.Cast<IFileChange2>();
            foreach (var c in result.FileChanges.Cast<IFileChange2>())
            {
                Assert.StartsWith("./", c.SourceRelativePath);
            }

            Assert.HasCount(2, result.FileChanges);
            IFileChange2 includeXyzChangeInfo = changes.Single(x => string.Equals(x.TargetRelativePath, "./include.xyz"));
            Assert.AreEqual(ChangeKind.Create, includeXyzChangeInfo.ChangeKind);

            IFileChange2 otherXyzChangeInfo = changes.Single(x => string.Equals(x.TargetRelativePath, "./other.xyz"));
            Assert.AreEqual(ChangeKind.Create, otherXyzChangeInfo.ChangeKind);
        }
    }
}
