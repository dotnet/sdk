// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Authoring.TemplateApiVerifier;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    [Collection("Verify Tests")]
    public class SnapshotTests : TestBase
    {
        private readonly ILogger _log;

        public SnapshotTests(ITestOutputHelper log)
        {
            _log = new XunitLoggerProvider(log).CreateLogger("TestRun");
        }

        [Fact]
        public Task PreferDefaultNameTest()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithPreferDefaultName");

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateWithPreferDefaultName")
                {
                    TemplatePath = templateLocation,
                    SnapshotsDirectory = "Approvals",
                    DoNotPrependTemplateNameToScenarioName = true,
                    DoNotAppendTemplateArgsToScenarioName = true,
                    ScenarioName = "Basic"
                }
                .WithInstantiationThroughTemplateCreatorApi(new Dictionary<string, string?>());

            VerificationEngine engine = new VerificationEngine(_log);
            return engine.Execute(options);
        }

        [Fact]
        public Task TemplateWithOnlyIfStatementTest()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithOnlyIfStatement");

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateWithOnlyIfStatement")
                {
                    TemplatePath = templateLocation,
                    SnapshotsDirectory = "Approvals",
                    DoNotPrependTemplateNameToScenarioName = true,
                    DoNotAppendTemplateArgsToScenarioName = true,
                    ScenarioName = "Basic"
                }
                .WithInstantiationThroughTemplateCreatorApi(new Dictionary<string, string?>() { { "default-port", "3332" } });

            VerificationEngine engine = new VerificationEngine(_log);
            return engine.Execute(options);
        }

        [Fact]
        public Task TemplateWithOnlyIfStatementTestForLocalhostTest()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithOnlyIfForLocalhost");

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateWithOnlyIfForLocalhost")
                {
                    TemplatePath = templateLocation,
                    SnapshotsDirectory = "Approvals",
                    DoNotPrependTemplateNameToScenarioName = true,
                    DoNotAppendTemplateArgsToScenarioName = true,
                    ScenarioName = "Basic"
                }
                .WithInstantiationThroughTemplateCreatorApi(new Dictionary<string, string?>() { { "default-port", "3332" } });

            VerificationEngine engine = new VerificationEngine(_log);
            return engine.Execute(options);
        }

        [Theory]
        [InlineData("no options", null)]
        [InlineData("options without value", new[] { "A", null, "B", null, "C", null, "D", null })]
        [InlineData("option equals to false", new[] { "A", "false", "B", "false", "C", "false", "D", "false" })]
        [InlineData("option equals to true", new[] { "A", "true", "B", "true", "C", "true", "D", "true" })]
        public async Task BooleanConditionsTest(string testName, string?[]? parametersArray)
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string templateShortName = "TestAssets.TemplateWithBooleanParameters";

            //get the template location
            string templateLocation = Path.Combine(TestTemplatesLocation, "TemplateWithBooleanParameters");

            Dictionary<string, string?> parameters = ConvertToParameters(parametersArray);

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplatePath = templateLocation,
                SnapshotsDirectory = "Approvals",
                OutputDirectory = workingDirectory,
                DoNotPrependTemplateNameToScenarioName = true,
                DoNotAppendTemplateArgsToScenarioName = true,
                ScenarioName = testName,
            }
            .WithInstantiationThroughTemplateCreatorApi(parameters);

            VerificationEngine engine = new(_log);
            await engine.Execute(options);
        }

        [Theory]
        [InlineData("DefaultIfOptionWithoutValue_NoValueDefaultsNotUsedIfSwitchesNotSpecified", "DefaultIfOptionWithoutValue", "TestAssets.DefaultIfOptionWithoutValue", null)]
        [InlineData("DefaultIfOptionWithoutValue_NoValueDefaultForChoiceParamIsUsed", "DefaultIfOptionWithoutValue", "TestAssets.DefaultIfOptionWithoutValue", new[] { "MyChoice", null })]
        [InlineData("DefaultIfOptionWithoutValue_NoValueDefaultForStringParamIsUsed", "DefaultIfOptionWithoutValue", "TestAssets.DefaultIfOptionWithoutValue", new[] { "MyString", null })]
        [InlineData("DefaultIfOptionWithoutValue_NoValueDefault_UserProvidedValuesAreIsUsed", "DefaultIfOptionWithoutValue", "TestAssets.DefaultIfOptionWithoutValue", new[] { "MyString", "UserString", "MyChoice", "OtherChoice" })]
        [InlineData("Renames_FileRenames", "TemplateWithRenames", "TestAssets.TemplateWithRenames", new[] { "foo", "baz", "testForms", "TestProject" })]
        [InlineData("Renames_SourceNameFileRenames", "TemplateWithSourceName", "TestAssets.TemplateWithSourceName", new[] { "name", "baz" })]
        [InlineData("Renames_NegativeFileRenames", "TemplateWithUnspecifiedSourceName", "TestAssets.TemplateWithUnspecifiedSourceName", new[] { "name", "baz" })]
        [InlineData("Renames_CustomSourcePathRename", "TemplateWithSourceNameAndCustomSourcePath", "TestAssets.TemplateWithSourceNameAndCustomSourcePath", new[] { "name", "bar" })]
        [InlineData("Renames_CustomTargetPathRename", "TemplateWithSourceNameAndCustomTargetPath", "TestAssets.TemplateWithSourceNameAndCustomTargetPath", new[] { "name", "bar" })]
        [InlineData("Renames_CustomSourceAndTargetPathRename", "TemplateWithSourceNameAndCustomSourceAndTargetPaths", "TestAssets.TemplateWithSourceNameAndCustomSourceAndTargetPaths", new[] { "name", "bar" })]
        [InlineData("Renames_SourcePathOutsideConfigRoot", "TemplateWithSourcePathOutsideConfigRoot", "TestAssets.TemplateWithSourcePathOutsideConfigRoot", new[] { "name", "baz" })]
        [InlineData("Renames_SourceNameInTargetPathGetsRenamed", "TemplateWithSourceNameInTargetPathGetsRenamed", "TestAssets.TemplateWithSourceNameInTargetPathGetsRenamed", new[] { "name", "baz" })]
        [InlineData("Renames_PlaceholderFiles", "TemplateWithPlaceholderFiles", "TestAssets.TemplateWithPlaceholderFiles", null)]
        [InlineData("Renames_DerivedSymbolFileRename", "TemplateWithDerivedSymbolFileRename", "TestAssets.TemplateWithDerivedSymbolFileRename", new[] { "name", "Last.Part.Is.For.Rename" })]
        [InlineData("Renames_MultipleRenamesOnSameFile", "TemplateWithMultipleRenamesOnSameFile", "TestAssets.TemplateWithMultipleRenamesOnSameFile", new[] { "fooRename", "base", "barRename", "ball" })]
        [InlineData("Renames_MultipleRenamesOnSameFileHandlesOverlap", "TemplateWithMultipleRenamesOnSameFileHandlesOverlap", "TestAssets.TemplateWithMultipleRenamesOnSameFileHandlesOverlap", new[] { "fooRename", "pin", "oobRename", "ball" })]
        [InlineData("Renames_MultipleRenamesOnSameFileHandlesInducedOverlap", "TemplateWithMultipleRenamesOnSameFileHandlesInducedOverlap", "TestAssets.TemplateWithMultipleRenamesOnSameFileHandlesInducedOverlap", new[] { "fooRename", "bar", "barRename", "baz" })]
        [InlineData("Renames_CaseSensitiveNameBasedRenames", "TemplateWithCaseSensitiveNameBasedRenames", "TestAssets.TemplateWithCaseSensitiveNameBasedRenames", new[] { "name", "NewName" })]
        [InlineData("Renames_JoinAndFolderRename", "TemplateWithJoinAndFolderRename", "TestAssets.TemplateWithJoinAndFolderRename", new[] { "name", "NewName", "product", "Office" })]
        [InlineData("KitchenSink_ConfigurationKitchenSink", "ConfigurationKitchenSink", "TestAssets.ConfigurationKitchenSink", new[] { "replaceThings", "Stuff", "replaceThere", "You" })]
        [InlineData("RegexMatch_RegexMatchMacroPositive", "TemplateWithRegexMatchMacro", "TestAssets.TemplateWithRegexMatchMacro", new[] { "name", "hello" })]
        [InlineData("RegexMatch_RegexMatchMacroNegative", "TemplateWithRegexMatchMacro", "TestAssets.TemplateWithRegexMatchMacro", new[] { "name", "there" })]
        [InlineData("TemplateWithTagsBasicTest", "TemplateWithTags", "TestAssets.TemplateWithTags", null)]
        [InlineData("ValueForms", "TemplateWithValueForms", "TestAssets.TemplateWithValueForms", new[] { "foo", "Test.Value6", "param1", "MyPascalTestValue", "param2", "myCamelTestValue", "param3", "my test text" })]
        [InlineData("ValueForms_DerivedSymbolWithValueForms", "TemplateWithDerivedSymbolWithValueForms", "TestAssets.TemplateWithDerivedSymbolWithValueForms", new[] { "n", "Test.AppSeven" })]
        public async Task LegacyTests(string scenarioName, string templateFolderName, string templateShortName, string?[]? parametersArray)
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string templateLocation = Path.Combine(TestTemplatesLocation, templateFolderName);
            Dictionary<string, string?> parameters = ConvertToParameters(parametersArray);
            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplatePath = templateLocation,
                SnapshotsDirectory = "Approvals",
                OutputDirectory = workingDirectory,
                DoNotPrependTemplateNameToScenarioName = true,
                DoNotAppendTemplateArgsToScenarioName = false,
                ScenarioName = scenarioName,
            }
            .WithInstantiationThroughTemplateCreatorApi(parameters);

            VerificationEngine engine = new(_log);
            await engine.Execute(options);
        }

        [Fact]
        public async Task LegacyTest_PortsAndCoalesceRenames()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string templateLocation = Path.Combine(TestTemplatesLocation, "TemplateWithPortsAndCoalesce");
            Dictionary<string, string?> parameters = new()
            {
                { "userPort2", "9999" }
            };
            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: "TestAssets.TemplateWithPortsAndCoalesce")
            {
                TemplatePath = templateLocation,
                SnapshotsDirectory = "Approvals",
                OutputDirectory = workingDirectory,
                DoNotPrependTemplateNameToScenarioName = true,
                DoNotAppendTemplateArgsToScenarioName = false,
                ScenarioName = "PortsAndCoalesceRenames",
            }
            .WithCustomDirectoryVerifier(
                async (contentDir, contentFetcher) =>
                {
                    await foreach (var (filePath, content) in contentFetcher.Value)
                    {
                        if (Path.GetFileName(filePath).Equals("bar.cs"))
                        {
                            Assert.DoesNotContain("The port is 1234", content);
                            Assert.Contains("The port is 9999", content);
                        }
                    }
                })
            .WithInstantiationThroughTemplateCreatorApi(parameters);

            VerificationEngine engine = new(_log);
            await engine.Execute(options);
        }

        private Dictionary<string, string?> ConvertToParameters(string?[]? parametersArray)
        {
            Dictionary<string, string?> parameters = new();
            if (parametersArray != null)
            {
                if (parametersArray.Length % 2 != 0)
                {
                    throw new ArgumentException($"{nameof(parametersArray)} should contain even number of elements");
                }
                for (int i = 0; i < parametersArray.Length; i += 2)
                {
                    if (parametersArray[i] == null)
                    {
                        throw new ArgumentException($"Even elements of {nameof(parametersArray)} should be parameter names and should not be null.");
                    }
                    parameters[parametersArray[i]!] = parametersArray[i + 1];
                }
            }

            return parameters;
        }
    }
}

#endif
