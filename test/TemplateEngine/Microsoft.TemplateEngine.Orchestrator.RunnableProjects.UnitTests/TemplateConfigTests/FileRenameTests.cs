// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;
using static Microsoft.TemplateEngine.Orchestrator.RunnableProjects.RunnableProjectGenerator;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class FileRenameTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private EnvironmentSettingsHelper _environmentSettingsHelper;

        public FileRenameTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        [Fact(DisplayName = nameof(SourceRenameIsCaseSensitive))]
        public void SourceRenameIsCaseSensitive()
        {
            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);

            TestTemplateSetup setup = SetupSourceRenameIsCaseSensitveTestTemplate(environment, sourceBasePath);
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(environment);
            IReadOnlyDictionary<string, IReadOnlyList<IFileChange2>> allChanges = setup.GetFileChanges(targetDir);

            Assert.Equal(1, allChanges.Count);  // one source had changes
            Assert.True(allChanges.TryGetValue("./", out IReadOnlyList<IFileChange2>? changes), "No changes for source './'");

            Assert.Equal(2, changes!.Count);
            Assert.Equal(1, changes.Count(x => string.Equals(x.TargetRelativePath, "YesNewName.txt", StringComparison.Ordinal)));
            Assert.Equal(1, changes.Count(x => string.Equals(x.TargetRelativePath, "dontrenameme.txt", StringComparison.Ordinal)));
            Assert.Equal(0, changes.Count(x => string.Equals(x.TargetRelativePath, "NoNewName.txt", StringComparison.Ordinal)));
        }

        [Fact(DisplayName = nameof(SourceModifierRenameIsCaseSensitive))]
        public void SourceModifierRenameIsCaseSensitive()
        {
            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);

            TestTemplateSetup setup = SetupSourceModifierRenameIsCaseSensitiveTestTemplate(environment, sourceBasePath);
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(environment);
            IReadOnlyDictionary<string, IReadOnlyList<IFileChange2>> allChanges = setup.GetFileChanges(targetDir);

            Assert.Equal(1, allChanges.Count);  // one source had changes
            Assert.True(allChanges.TryGetValue("./", out IReadOnlyList<IFileChange2>? changes), "No changes for source './'");

            Assert.Equal(2, changes!.Count);
            Assert.Equal(1, changes.Count(x => string.Equals(x.TargetRelativePath, "YesNewName.txt", StringComparison.Ordinal)));
            Assert.Equal(1, changes.Count(x => string.Equals(x.TargetRelativePath, "dontrenameme.txt", StringComparison.Ordinal)));
            Assert.Equal(0, changes.Count(x => string.Equals(x.TargetRelativePath, "NoNewName.txt", StringComparison.Ordinal)));
        }

        [Fact(DisplayName = nameof(CanReadFilenameReplacementConfig))]
        public void CanReadFilenameReplacementConfig()
        {
            string configContent = @"
{
  ""symbols"": {
	""testparam"": {
      ""type"": ""parameter"",
      ""datatype"": ""string"",
	  ""fileRename"": ""testparamfilereplacement""
    },
    ""testgenerated"": {
      ""type"": ""generated"",
      ""generator"": ""casing"",
      ""parameters"": {
        ""source"": ""name"",
        ""toLower"": true
      },
	  ""fileRename"": ""testgeneratedfilereplacement""
    },
    ""testgenerated2"": {
      ""type"": ""generated"",
      ""generator"": ""casing"",
      ""parameters"": {
        ""source"": ""name"",
        ""toLower"": true
      },
	  ""replace"": ""testgeneratedreplacement""
    },
  }
}
";
            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();
            JObject configJson = JObject.Parse(configContent);
            SimpleConfigModel config = SimpleConfigModel.FromJObject(environment, configJson);
            Assert.Equal(2, config.SymbolFilenameReplacements.Count);
            Assert.Equal("testparamfilereplacement", config.SymbolFilenameReplacements.Single(x => x.VariableName.Contains("testparam")).OriginalValue.Value);
            Assert.Equal("testgeneratedfilereplacement", config.SymbolFilenameReplacements.Single(x => x.VariableName == "testgenerated").OriginalValue.Value);
        }

        [Fact(DisplayName = nameof(CanReadFilenameReplacementConfigWithForms))]
        public void CanReadFilenameReplacementConfigWithForms()
        {
            string configContent = @"
{
  ""symbols"": {
	""testparam"": {
      ""type"": ""parameter"",
      ""datatype"": ""string"",
	  ""fileRename"": ""TestParamFileReplacement"",
      ""forms"": {
        ""global"" : [ ""identity"", ""lc"", ""uc""]
      }
    },
    ""testgenerated"": {
      ""type"": ""generated"",
      ""generator"": ""casing"",
      ""parameters"": {
        ""source"": ""name"",
        ""toLower"": true
      },
	  ""fileRename"": ""testgeneratedfilereplacement""
    },
    ""testgenerated2"": {
      ""type"": ""generated"",
      ""generator"": ""casing"",
      ""parameters"": {
        ""source"": ""name"",
        ""toLower"": true
      },
	  ""replace"": ""testgeneratedreplacement""
    },
  },
  ""forms"": {
    ""lc"": {
        ""identifier"": ""lowercase""
    },
    ""uc"": {
        ""identifier"": ""uppercase""
    }
  }
}";
            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();
            JObject configJson = JObject.Parse(configContent);
            SimpleConfigModel config = SimpleConfigModel.FromJObject(environment, configJson);
            Assert.Equal(4, config.SymbolFilenameReplacements.Count);
            Assert.Equal(3, config.SymbolFilenameReplacements.Count(x => x.VariableName.Contains("testparam")));
            Assert.Equal("TestParamFileReplacement", config.SymbolFilenameReplacements.Single(x => x.VariableName == "testparam{-VALUE-FORMS-}identity").OriginalValue.Value);
            Assert.Equal("TESTPARAMFILEREPLACEMENT", config.SymbolFilenameReplacements.Single(x => x.VariableName == "testparam{-VALUE-FORMS-}uc").OriginalValue.Value);
            Assert.Equal("testparamfilereplacement", config.SymbolFilenameReplacements.Single(x => x.VariableName == "testparam{-VALUE-FORMS-}lc").OriginalValue.Value);
            Assert.Equal("testgeneratedfilereplacement", config.SymbolFilenameReplacements.Single(x => x.VariableName == "testgenerated").OriginalValue.Value);
        }

        [Fact(DisplayName = nameof(CanGenerateFileRenamesForSymbolBasedRenames))]
        public void CanGenerateFileRenamesForSymbolBasedRenames()
        {
            //environment
            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();

            //simulate template files
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>();
            // template.json
            templateSourceFiles.Add(TemplateConfigTestHelpers.DefaultConfigRelativePath, string.Empty);
            // content
            templateSourceFiles.Add("Replace1_file.txt", null);
            templateSourceFiles.Add("Replace2_file.txt", null);
            TestTemplateSetup setup = new TestTemplateSetup(environment, sourceBasePath, templateSourceFiles);
            setup.WriteSource();

            //get target directory
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(environment);

            //prepare parameters
            ParameterSet parameters = new ParameterSet(SimpleConfigModel.FromJObject(environment, JObject.Parse("{}")));
            Parameter nameParameter = new Parameter()
            {
                Name = "name"
            };
            Parameter testParameter = new Parameter()
            {
                Name = "test"
            };
            parameters.AddParameter(nameParameter);
            parameters.AddParameter(testParameter);
            parameters.ResolvedValues[nameParameter] = "testName";
            parameters.ResolvedValues[testParameter] = "Replace1Value";

            //prepare renames configuration
            List<IReplacementTokens> symbolBasedRenames = new List<IReplacementTokens>();
            symbolBasedRenames.Add(new ReplacementTokens("test", TokenConfig.FromValue("Replace1")));

            IReadOnlyDictionary<string, string> allChanges = setup.GetRenames("./", targetDir, parameters, symbolBasedRenames);
            Assert.Equal(1, allChanges.Count);
            Assert.Equal("Replace1Value_file.txt", allChanges["Replace1_file.txt"]);
        }

        [Fact(DisplayName = nameof(CanGenerateFileRenamesForSymbolBasedRenames))]
        public void CanGenerateFileRenamesForSymbolBasedRenames_NonString()
        {
            //environment
            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();

            //simulate template files
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>();
            // template.json
            templateSourceFiles.Add(TemplateConfigTestHelpers.DefaultConfigRelativePath, string.Empty);
            // content
            templateSourceFiles.Add("date_name.txt", null);
            templateSourceFiles.Add("other_name.txt", null);
            TestTemplateSetup setup = new TestTemplateSetup(environment, sourceBasePath, templateSourceFiles);
            setup.WriteSource();

            //get target directory
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(environment);

            //prepare parameters
            ParameterSet parameters = new ParameterSet(SimpleConfigModel.FromJObject(environment, JObject.Parse("{}")));
            Parameter nameParameter = new Parameter()
            {
                Name = "name"
            };
            Parameter intDateParameter = new Parameter()
            {
                Name = "date"
            };
            Parameter otherParameter = new Parameter()
            {
                Name = "other"
            };
            parameters.AddParameter(nameParameter);
            parameters.AddParameter(intDateParameter);
            parameters.AddParameter(otherParameter);
            parameters.ResolvedValues[nameParameter] = "testName";
            parameters.ResolvedValues[intDateParameter] = 20210429;
            parameters.ResolvedValues[otherParameter] = new TestParameterValueClass { A = "foo", B = "bar" };

            //prepare renames configuration
            List<IReplacementTokens> symbolBasedRenames = new List<IReplacementTokens>();
            symbolBasedRenames.Add(new ReplacementTokens("date", TokenConfig.FromValue("date")));
            symbolBasedRenames.Add(new ReplacementTokens("other", TokenConfig.FromValue("other")));
            symbolBasedRenames.Add(new ReplacementTokens("name", TokenConfig.FromValue("name")));

            IReadOnlyDictionary<string, string> allChanges = setup.GetRenames("./", targetDir, parameters, symbolBasedRenames);
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
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>();
            // template.json
            templateSourceFiles.Add(TemplateConfigTestHelpers.DefaultConfigRelativePath, string.Empty);
            // content
            templateSourceFiles.Add("Replace1_file.txt", null);
            templateSourceFiles.Add("replace2_file.txt", null);
            templateSourceFiles.Add("REPLACE3_file.txt", null);
            TestTemplateSetup setup = new TestTemplateSetup(environment, sourceBasePath, templateSourceFiles);
            setup.WriteSource();

            //get target directory
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(environment);

            //prepare parameters
            ParameterSet parameters = new ParameterSet(SimpleConfigModel.FromJObject(environment, JObject.Parse("{}")));
            Parameter nameParameter = new Parameter()
            {
                Name = "name"
            };
            Parameter testParameterIdentity = new Parameter()
            {
                Name = "test{-VALUE-FORMS-}identity"
            };
            Parameter testParameterUC = new Parameter()
            {
                Name = "test{-VALUE-FORMS-}uc"
            };
            Parameter testParameterLC = new Parameter()
            {
                Name = "test{-VALUE-FORMS-}lc"
            };
            parameters.AddParameter(nameParameter);
            parameters.AddParameter(testParameterIdentity);
            parameters.AddParameter(testParameterUC);
            parameters.AddParameter(testParameterLC);
            parameters.ResolvedValues[nameParameter] = "testName";
            parameters.ResolvedValues[testParameterIdentity] = "TestProject";
            parameters.ResolvedValues[testParameterUC] = "TESTPROJECT";
            parameters.ResolvedValues[testParameterLC] = "testproject";

            //prepare renames configuration
            List<IReplacementTokens> symbolBasedRenames = new List<IReplacementTokens>();
            symbolBasedRenames.Add(new ReplacementTokens("test{-VALUE-FORMS-}identity", TokenConfig.FromValue("Replace")));
            symbolBasedRenames.Add(new ReplacementTokens("test{-VALUE-FORMS-}uc", TokenConfig.FromValue("REPLACE")));
            symbolBasedRenames.Add(new ReplacementTokens("test{-VALUE-FORMS-}lc", TokenConfig.FromValue("replace")));

            IReadOnlyDictionary<string, string> allChanges = setup.GetRenames("./", targetDir, parameters, symbolBasedRenames);
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
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>();
            // template.json
            templateSourceFiles.Add(TemplateConfigTestHelpers.DefaultConfigRelativePath, string.Empty);
            // content
            templateSourceFiles.Add("replace1_file.txt", null);
            templateSourceFiles.Add("replace2_file.txt", null);
            TestTemplateSetup setup = new TestTemplateSetup(environment, sourceBasePath, templateSourceFiles);
            setup.WriteSource();

            //get target directory
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(environment);

            //prepare parameters
            ParameterSet parameters = new ParameterSet(SimpleConfigModel.FromJObject(environment, JObject.Parse("{}")));
            Parameter nameParameter = new Parameter()
            {
                Name = "name"
            };
            Parameter testParameterIdentity = new Parameter()
            {
                Name = "test{-VALUE-FORMS-}identity"
            };
            Parameter testParameterLC = new Parameter()
            {
                Name = "test{-VALUE-FORMS-}lc"
            };
            parameters.AddParameter(nameParameter);
            parameters.AddParameter(testParameterIdentity);
            parameters.AddParameter(testParameterLC);
            parameters.ResolvedValues[nameParameter] = "testName";
            parameters.ResolvedValues[testParameterIdentity] = "testproject";
            parameters.ResolvedValues[testParameterLC] = "testproject";

            //prepare renames configuration
            List<IReplacementTokens> symbolBasedRenames = new List<IReplacementTokens>();
            symbolBasedRenames.Add(new ReplacementTokens("test{-VALUE-FORMS-}identity", TokenConfig.FromValue("replace")));
            symbolBasedRenames.Add(new ReplacementTokens("test{-VALUE-FORMS-}lc", TokenConfig.FromValue("replace")));

            IReadOnlyDictionary<string, string> allChanges = setup.GetRenames("./", targetDir, parameters, symbolBasedRenames);
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
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>();
            // template.json
            templateSourceFiles.Add(TemplateConfigTestHelpers.DefaultConfigRelativePath, string.Empty);
            // content
            templateSourceFiles.Add("Replace1_file.txt", null);
            templateSourceFiles.Add("Replace2_file.txt", null);
            TestTemplateSetup setup = new TestTemplateSetup(environment, sourceBasePath, templateSourceFiles);
            setup.WriteSource();

            //get target directory
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(environment);

            //prepare parameters
            ParameterSet parameters = new ParameterSet(SimpleConfigModel.FromJObject(environment, JObject.Parse("{}")));
            Parameter nameParameter = new Parameter()
            {
                Name = "name"
            };
            Parameter testParameter = new Parameter()
            {
                Name = "test"
            };
            parameters.AddParameter(nameParameter);
            parameters.AddParameter(testParameter);
            parameters.ResolvedValues[nameParameter] = "testName";
            parameters.ResolvedValues[testParameter] = "ReplaceValue";

            //prepare renames configuration
            List<IReplacementTokens> symbolBasedRenames = new List<IReplacementTokens>();
            symbolBasedRenames.Add(new ReplacementTokens("test", TokenConfig.FromValue("Replace")));

            IReadOnlyDictionary<string, string> allChanges = setup.GetRenames("./", targetDir, parameters, symbolBasedRenames);
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
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);
            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>();
            // template.json
            templateSourceFiles.Add(TemplateConfigTestHelpers.DefaultConfigRelativePath, string.Empty);
            // content
            templateSourceFiles.Add(@"Replace_dir/Replace_file.txt", null);
            TestTemplateSetup setup = new TestTemplateSetup(environment, sourceBasePath, templateSourceFiles);
            setup.WriteSource();

            //get target directory
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(environment);

            //prepare parameters
            ParameterSet parameters = new ParameterSet(SimpleConfigModel.FromJObject(environment, JObject.Parse("{}")));
            Parameter nameParameter = new Parameter()
            {
                Name = "name"
            };
            Parameter testParameter = new Parameter()
            {
                Name = "test"
            };
            parameters.AddParameter(nameParameter);
            parameters.AddParameter(testParameter);
            parameters.ResolvedValues[nameParameter] = "testName";
            parameters.ResolvedValues[testParameter] = "ReplaceValue";

            //prepare renames configuration
            List<IReplacementTokens> symbolBasedRenames = new List<IReplacementTokens>();
            symbolBasedRenames.Add(new ReplacementTokens("test", TokenConfig.FromValue("Replace")));

            IReadOnlyDictionary<string, string> allChanges = setup.GetRenames("./", targetDir, parameters, symbolBasedRenames);
            Assert.Equal(2, allChanges.Count);
            Assert.Equal(@"ReplaceValue_dir", allChanges[@"Replace_dir"]);
            Assert.Equal(@"ReplaceValue_dir/ReplaceValue_file.txt", allChanges[@"Replace_dir/Replace_file.txt"]);
        }

        private static TestTemplateSetup SetupSourceRenameIsCaseSensitveTestTemplate(IEngineEnvironmentSettings environment, string basePath)
        {
            string sourceConfig = @"
{
  ""sources"": [
    {
      ""rename"": {
        ""RenameMe.txt"": ""YesNewName.txt"",
        ""DontRenameMe.txt"": ""NoNewName.txt""
      },
    }
  ]
}";

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>();
            // template.json
            templateSourceFiles.Add(TemplateConfigTestHelpers.DefaultConfigRelativePath, sourceConfig);
            // content
            templateSourceFiles.Add("RenameMe.txt", null);
            templateSourceFiles.Add("dontrenameme.txt", null);
            TestTemplateSetup setup = new TestTemplateSetup(environment, basePath, templateSourceFiles);
            setup.WriteSource();
            return setup;
        }

        private static TestTemplateSetup SetupSourceModifierRenameIsCaseSensitiveTestTemplate(IEngineEnvironmentSettings environment, string basePath)
        {
            string sourceConfig = @"
{
  ""sources"": [
    {
      ""modifiers"": [
        {
          ""rename"": {
            ""RenameMe.txt"": ""YesNewName.txt"",
            ""DontRenameMe.txt"": ""NoNewName.txt"",
          }
        }
      ]
    }
  ]
}
";

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>();
            // template.json
            templateSourceFiles.Add(TemplateConfigTestHelpers.DefaultConfigRelativePath, sourceConfig);
            // content
            templateSourceFiles.Add("RenameMe.txt", null);
            templateSourceFiles.Add("dontrenameme.txt", null);
            TestTemplateSetup setup = new TestTemplateSetup(environment, basePath, templateSourceFiles);
            setup.WriteSource();
            return setup;
        }

        private class TestParameterValueClass
        {
            public string? A { get; set; }

            public string? B { get; set; }

            public override string ToString() => $"{A}-{B}";
        }
    }
}
