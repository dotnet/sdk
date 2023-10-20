// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class FileRenameTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        public FileRenameTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        [Fact]
        public async Task SourceRenameIsCaseSensitive()
        {
            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();
            string sourceBasePath = environment.GetTempVirtualizedPath();

            string sourceConfig = /*lang=json*/ """
                {
                  "identity": "test",
                  "name": "test",
                  "shortname": "test",
                  "sources": [
                    {
                      "rename": {
                        "RenameMe.txt": "YesNewName.txt",
                        "DontRenameMe.txt": "NoNewName.txt"
                      },
                    }
                  ]
                }
                """;

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, sourceConfig },
                // content
                { "RenameMe.txt", null },
                { "dontrenameme.txt", null }
            };
            environment.WriteTemplateSource(sourceBasePath, templateSourceFiles);

            string targetDir = environment.GetTempVirtualizedPath();
            RunnableProjectGenerator generator = new();

            using IMountPoint mountPoint = environment.MountPath(sourceBasePath);
            IFile? templateConfigFile = mountPoint.FileInfo(TestFileSystemUtils.DefaultConfigRelativePath);
            Assert.NotNull(templateConfigFile);

            using ITemplate template = new RunnableProjectConfig(environment, generator, templateConfigFile);
            ParameterSetData parameters = new(template);

            ICreationEffects result = await (generator as IGenerator).GetCreationEffectsAsync(environment, template, parameters, targetDir, default);
            IEnumerable<IFileChange2> changes = result.FileChanges.Cast<IFileChange2>();

            Assert.Equal(2, result.FileChanges.Count);
            Assert.All(result.FileChanges.Cast<IFileChange2>(), c => c.SourceRelativePath.StartsWith("./"));

            Dictionary<string, string> dict = changes.ToDictionary(c => c.SourceRelativePath, c => c.TargetRelativePath);
            Assert.Equal("./YesNewName.txt", dict["./RenameMe.txt"]);
            Assert.Equal("./dontrenameme.txt", dict["./dontrenameme.txt"]);
        }

        [Fact]
        public async Task SourceModifierRenameIsCaseSensitive()
        {
            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();
            string sourceBasePath = environment.GetTempVirtualizedPath();

            string sourceConfig = /*lang=json*/ """
                {
                  "identity": "test",
                  "name": "test",
                  "shortname": "test",
                  "sources": [
                    {
                      "modifiers": [
                        {
                          "rename": {
                            "RenameMe.txt": "YesNewName.txt",
                            "DontRenameMe.txt": "NoNewName.txt",
                          }
                        }
                      ]
                    }
                  ]
                }
                """;

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, sourceConfig },
                // content
                { "RenameMe.txt", null },
                { "dontrenameme.txt", null }
            };
            environment.WriteTemplateSource(sourceBasePath, templateSourceFiles);

            string targetDir = environment.GetTempVirtualizedPath();
            RunnableProjectGenerator generator = new();

            using IMountPoint mountPoint = environment.MountPath(sourceBasePath);
            IFile? templateConfigFile = mountPoint.FileInfo(TestFileSystemUtils.DefaultConfigRelativePath);
            Assert.NotNull(templateConfigFile);

            using ITemplate template = new RunnableProjectConfig(environment, generator, templateConfigFile);
            ParameterSetData parameters = new(template);

            ICreationEffects result = await (generator as IGenerator).GetCreationEffectsAsync(environment, template, parameters, targetDir, default);
            IEnumerable<IFileChange2> changes = result.FileChanges.Cast<IFileChange2>();

            Assert.Equal(2, result.FileChanges.Count);
            Assert.All(result.FileChanges.Cast<IFileChange2>(), c => c.SourceRelativePath.StartsWith("./"));

            Dictionary<string, string> dict = changes.ToDictionary(c => c.SourceRelativePath, c => c.TargetRelativePath);
            Assert.Equal("./YesNewName.txt", dict["./RenameMe.txt"]);
            Assert.Equal("./dontrenameme.txt", dict["./dontrenameme.txt"]);
        }

        [Fact(DisplayName = nameof(CanReadFilenameReplacementConfig))]
        public void CanReadFilenameReplacementConfig()
        {
            string configContent = /*lang=json*/ """
            {
              "identity": "test",
              "name": "test",
              "shortName": "test",
              "symbols": {
                "testparam": {
                  "type": "parameter",
                  "datatype": "string",
                  "fileRename": "testparamfilereplacement"
                },
                "testgenerated": {
                  "type": "generated",
                  "generator": "casing",
                  "parameters": {
                    "source": "name",
                    "toLower": true
                  },
                  "fileRename": "testgeneratedfilereplacement"
                },
                "testgenerated2": {
                  "type": "generated",
                  "generator": "casing",
                  "parameters": {
                    "source": "name",
                    "toLower": true
                  },
                  "replace": "testgeneratedreplacement"
                },
              }
            }
            """;
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.Parse(configContent));
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment();

            string sourceBasePath = environmentSettings.GetTempVirtualizedPath();
            using IMountPoint mountPoint = environmentSettings.MountPath(sourceBasePath);
            using RunnableProjectConfig runnableConfig = new(environmentSettings, A.Fake<IGenerator>(), configModel, mountPoint.Root);

            Assert.Equal(2, runnableConfig.SymbolFilenameReplacements.Count);
            Assert.Equal("testparamfilereplacement", runnableConfig.SymbolFilenameReplacements.Single(x => x.VariableName.Contains("testparam")).OriginalValue.Value);
            Assert.Equal("testgeneratedfilereplacement", runnableConfig.SymbolFilenameReplacements.Single(x => x.VariableName == "testgenerated").OriginalValue.Value);
        }

        [Fact(DisplayName = nameof(CanReadFilenameReplacementConfigWithForms))]
        public void CanReadFilenameReplacementConfigWithForms()
        {
            string configContent = /*lang=json*/ """
            {
              "identity": "test",
              "name": "test",
              "shortName": "test",
              "symbols": {
                "testparam": {
                  "type": "parameter",
                  "datatype": "string",
                  "fileRename": "TestParamFileReplacement",
                  "forms": {
                    "global" : [ "identity", "lc", "uc"]
                  }
                },
                "testgenerated": {
                  "type": "generated",
                  "generator": "casing",
                  "parameters": {
                    "source": "name",
                    "toLower": true
                  },
                  "fileRename": "testgeneratedfilereplacement"
                },
                "testgenerated2": {
                  "type": "generated",
                  "generator": "casing",
                  "parameters": {
                    "source": "name",
                    "toLower": true
                  },
                  "replace": "testgeneratedreplacement"
                },
              },
              "forms": {
                "lc": {
                    "identifier": "lowercase"
                },
                "uc": {
                    "identifier": "uppercase"
                }
              }
            }
            """;
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.Parse(configContent));
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment();

            string sourceBasePath = environmentSettings.GetTempVirtualizedPath();
            using IMountPoint mountPoint = environmentSettings.MountPath(sourceBasePath);

            using RunnableProjectConfig runnableConfig = new(environmentSettings, A.Fake<IGenerator>(), configModel, mountPoint.Root);

            Assert.Equal(4, runnableConfig.SymbolFilenameReplacements.Count);
            Assert.Equal(3, runnableConfig.SymbolFilenameReplacements.Count(x => x.VariableName.Contains("testparam")));
            Assert.Equal("TestParamFileReplacement", runnableConfig.SymbolFilenameReplacements.Single(x => x.VariableName == "testparam{-VALUE-FORMS-}identity").OriginalValue.Value);
            Assert.Equal("TESTPARAMFILEREPLACEMENT", runnableConfig.SymbolFilenameReplacements.Single(x => x.VariableName == "testparam{-VALUE-FORMS-}uc").OriginalValue.Value);
            Assert.Equal("testparamfilereplacement", runnableConfig.SymbolFilenameReplacements.Single(x => x.VariableName == "testparam{-VALUE-FORMS-}lc").OriginalValue.Value);
            Assert.Equal("testgeneratedfilereplacement", runnableConfig.SymbolFilenameReplacements.Single(x => x.VariableName == "testgenerated").OriginalValue.Value);
        }

        [Fact(DisplayName = nameof(CanGenerateFileRenamesForSymbolBasedRenames))]
        public void CanGenerateFileRenamesForSymbolBasedRenames()
        {
            //environment
            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();

            //simulate template files
            string sourceBasePath = environment.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, string.Empty },
                // content
                { "Replace1_file.txt", null },
                { "Replace2_file.txt", null }
            };
            environment.WriteTemplateSource(sourceBasePath, templateSourceFiles);

            //get target directory
            string targetDir = environment.GetTempVirtualizedPath();

            //prepare variables
            IVariableCollection variables = new VariableCollection
            {
                ["name"] = "testName",
                ["test"] = "Replace1Value"
            };

            //prepare renames configuration
            List<IReplacementTokens> symbolBasedRenames = new()
            {
                new ReplacementTokens("test", TokenConfig.FromValue("Replace1"))
            };

            using IMountPoint mountPoint = environment.MountPath(sourceBasePath);

            IReadOnlyDictionary<string, string> allChanges = FileRenameGenerator.AugmentFileRenames(
                environment,
                sourceBasePath,
                mountPoint.Root,
                sourceDirectory: "./",
                targetDirectory: ref targetDir,
                resolvedNameParamValue: variables["name"],
                variables: variables,
                fileRenames: new Dictionary<string, string>(),
                symbolBasedFileRenames: symbolBasedRenames);

            Assert.Single(allChanges);
            Assert.Equal("Replace1Value_file.txt", allChanges["Replace1_file.txt"]);
        }

        [Fact]
        public void CanGenerateFileRenamesForSymbolBasedRenames_NonString()
        {
            //environment
            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();

            //simulate template files
            string sourceBasePath = environment.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, string.Empty },
                // content
                { "date_name.txt", null },
                { "other_name.txt", null }
            };
            environment.WriteTemplateSource(sourceBasePath, templateSourceFiles);

            //get target directory
            string targetDir = environment.GetTempVirtualizedPath();

            //prepare variables
            IVariableCollection variables = new VariableCollection
            {
                ["name"] = "testName",
                ["date"] = 20210429,
                ["other"] = new TestParameterValueClass { A = "foo", B = "bar" }
            };

            //prepare renames configuration
            List<IReplacementTokens> symbolBasedRenames = new()
            {
                new ReplacementTokens("date", TokenConfig.FromValue("date")),
                new ReplacementTokens("other", TokenConfig.FromValue("other")),
                new ReplacementTokens("name", TokenConfig.FromValue("name"))
            };

            using IMountPoint mountPoint = environment.MountPath(sourceBasePath);

            IReadOnlyDictionary<string, string> allChanges = FileRenameGenerator.AugmentFileRenames(
                environment,
                sourceBasePath,
                mountPoint.Root,
                sourceDirectory: "./",
                targetDirectory: ref targetDir,
                resolvedNameParamValue: variables["name"],
                variables: variables,
                fileRenames: new Dictionary<string, string>(),
                symbolBasedFileRenames: symbolBasedRenames);

            Assert.Equal(2, allChanges.Count);
            Assert.Equal("20210429_testName.txt", allChanges["date_name.txt"]);
            Assert.Equal("foo-bar_testName.txt", allChanges["other_name.txt"]);
        }

        [Fact(DisplayName = nameof(CanGenerateFileRenamesForSymbolBasedRenames_Forms))]
        public void CanGenerateFileRenamesForSymbolBasedRenames_Forms()
        {
            //environment
            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();

            //simulate template files
            string sourceBasePath = environment.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, string.Empty },
                // content
                { "Replace1_file.txt", null },
                { "replace2_file.txt", null },
                { "REPLACE3_file.txt", null }
            };
            environment.WriteTemplateSource(sourceBasePath, templateSourceFiles);

            //get target directory
            string targetDir = environment.GetTempVirtualizedPath();

            //prepare variables
            IVariableCollection variables = new VariableCollection
            {
                ["name"] = "testName",
                ["test{-VALUE-FORMS-}identity"] = "TestProject",
                ["test{-VALUE-FORMS-}uc"] = "TESTPROJECT",
                ["test{-VALUE-FORMS-}lc"] = "testproject"
            };

            //prepare renames configuration
            List<IReplacementTokens> symbolBasedRenames = new()
            {
                new ReplacementTokens("test{-VALUE-FORMS-}identity", TokenConfig.FromValue("Replace")),
                new ReplacementTokens("test{-VALUE-FORMS-}uc", TokenConfig.FromValue("REPLACE")),
                new ReplacementTokens("test{-VALUE-FORMS-}lc", TokenConfig.FromValue("replace"))
            };

            using IMountPoint mountPoint = environment.MountPath(sourceBasePath);
            object resolvedNameValue = variables["name"];
            IReadOnlyDictionary<string, string> allChanges = FileRenameGenerator.AugmentFileRenames(
                environment,
                sourceBasePath,
                mountPoint.Root,
                sourceDirectory: "./",
                targetDirectory: ref targetDir,
                resolvedNameParamValue: variables["name"],
                variables: variables,
                fileRenames: new Dictionary<string, string>(),
                symbolBasedFileRenames: symbolBasedRenames);

            Assert.Equal(3, allChanges.Count);
            Assert.Equal("TestProject1_file.txt", allChanges["Replace1_file.txt"]);
            Assert.Equal("TESTPROJECT3_file.txt", allChanges["REPLACE3_file.txt"]);
            Assert.Equal("testproject2_file.txt", allChanges["replace2_file.txt"]);
        }

        [Fact(DisplayName = nameof(CanGenerateFileRenamesForSymbolBasedRenames_WhenFormsResultInSameValue))]
        public void CanGenerateFileRenamesForSymbolBasedRenames_WhenFormsResultInSameValue()
        {
            //environment
            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();

            //simulate template files
            string sourceBasePath = environment.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, string.Empty },
                // content
                { "replace1_file.txt", null },
                { "replace2_file.txt", null }
            };
            environment.WriteTemplateSource(sourceBasePath, templateSourceFiles);

            //get target directory
            string targetDir = environment.GetTempVirtualizedPath();

            //prepare variables
            IVariableCollection variables = new VariableCollection
            {
                ["name"] = "testName",
                ["test{-VALUE-FORMS-}identity"] = "testproject",
                ["test{-VALUE-FORMS-}lc"] = "testproject"
            };

            //prepare renames configuration
            List<IReplacementTokens> symbolBasedRenames = new()
            {
                new ReplacementTokens("test{-VALUE-FORMS-}identity", TokenConfig.FromValue("replace")),
                new ReplacementTokens("test{-VALUE-FORMS-}lc", TokenConfig.FromValue("replace"))
            };

            using IMountPoint mountPoint = environment.MountPath(sourceBasePath);

            IReadOnlyDictionary<string, string> allChanges = FileRenameGenerator.AugmentFileRenames(
                environment,
                sourceBasePath,
                mountPoint.Root,
                sourceDirectory: "./",
                targetDirectory: ref targetDir,
                resolvedNameParamValue: variables["name"],
                variables: variables,
                fileRenames: new Dictionary<string, string>(),
                symbolBasedFileRenames: symbolBasedRenames);

            Assert.Equal(2, allChanges.Count);
            Assert.Equal("testproject1_file.txt", allChanges["replace1_file.txt"]);
            Assert.Equal("testproject2_file.txt", allChanges["replace2_file.txt"]);
        }

        [Fact(DisplayName = nameof(CanGenerateFileRenamesForSymbolBasedRenames_Multiple))]
        public void CanGenerateFileRenamesForSymbolBasedRenames_Multiple()
        {
            //environment
            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();

            //simulate template files
            string sourceBasePath = environment.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, string.Empty },
                // content
                { "Replace1_file.txt", null },
                { "Replace2_file.txt", null }
            };
            environment.WriteTemplateSource(sourceBasePath, templateSourceFiles);

            //get target directory
            string targetDir = environment.GetTempVirtualizedPath();

            //prepare variables
            IVariableCollection variables = new VariableCollection
            {
                ["name"] = "testName",
                ["test"] = "ReplaceValue"
            };

            //prepare renames configuration
            List<IReplacementTokens> symbolBasedRenames = new()
            {
                new ReplacementTokens("test", TokenConfig.FromValue("Replace"))
            };

            using IMountPoint mountPoint = environment.MountPath(sourceBasePath);
            IReadOnlyDictionary<string, string> allChanges = FileRenameGenerator.AugmentFileRenames(
                environment,
                sourceBasePath,
                mountPoint.Root,
                sourceDirectory: "./",
                targetDirectory: ref targetDir,
                resolvedNameParamValue: variables["name"],
                variables: variables,
                fileRenames: new Dictionary<string, string>(),
                symbolBasedFileRenames: symbolBasedRenames);

            Assert.Equal(2, allChanges.Count);
            Assert.Equal("ReplaceValue1_file.txt", allChanges["Replace1_file.txt"]);
            Assert.Equal("ReplaceValue2_file.txt", allChanges["Replace2_file.txt"]);
        }

        [Fact(DisplayName = nameof(CanGenerateFileRenamesForSymbolBasedRenames_DirectoryRename))]
        public void CanGenerateFileRenamesForSymbolBasedRenames_DirectoryRename()
        {
            //environment
            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();

            //simulate template files
            string sourceBasePath = environment.GetTempVirtualizedPath();
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, string.Empty },
                // content
                { @"Replace_dir/Replace_file.txt", null }
            };
            environment.WriteTemplateSource(sourceBasePath, templateSourceFiles);

            //get target directory
            string targetDir = environment.GetTempVirtualizedPath();

            //prepare variables
            IVariableCollection variables = new VariableCollection
            {
                ["name"] = "testName",
                ["test"] = "ReplaceValue"
            };

            //prepare renames configuration
            List<IReplacementTokens> symbolBasedRenames = new()
            {
                new ReplacementTokens("test", TokenConfig.FromValue("Replace"))
            };

            using IMountPoint mountPoint = environment.MountPath(sourceBasePath);
            IReadOnlyDictionary<string, string> allChanges = FileRenameGenerator.AugmentFileRenames(
                environment,
                sourceBasePath,
                mountPoint.Root,
                sourceDirectory: "./",
                targetDirectory: ref targetDir,
                resolvedNameParamValue: variables["name"],
                variables: variables,
                fileRenames: new Dictionary<string, string>(),
                symbolBasedFileRenames: symbolBasedRenames);

            Assert.Equal(2, allChanges.Count);
            Assert.Equal(@"ReplaceValue_dir", allChanges[@"Replace_dir"]);
            Assert.Equal(@"ReplaceValue_dir/ReplaceValue_file.txt", allChanges[@"Replace_dir/Replace_file.txt"]);
        }

        private class TestParameterValueClass
        {
            public string? A { get; set; }

            public string? B { get; set; }

            public override string ToString() => $"{A}-{B}";
        }
    }
}
