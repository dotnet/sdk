// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Authoring.TemplateApiVerifier;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    [TestClass]
    public class SnapshotTests : TestBase
    {
        private TestContext _testContext = null!;

        public TestContext TestContext
        {
            get => _testContext;
            set
            {
                _testContext = value;
                VerifyMSTest.Verifier.CurrentTestContext.Value = new VerifyMSTest.TestExecutionContext(value, GetType());
            }
        }

        private ILogger Log => new TestContextLogger(TestContext);

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            new VerifySettingsFixture();
        }

        [TestMethod]
        public Task PreferDefaultNameTest()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithPreferDefaultName");

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateWithPreferDefaultName")
                {
                    TemplatePath = templateLocation,
                    SnapshotsDirectory = ApprovalsDirectory,
                    DoNotPrependTemplateNameToScenarioName = true,
                    DoNotAppendTemplateArgsToScenarioName = true,
                    ScenarioName = "Basic"
                }
                .WithInstantiationThroughTemplateCreatorApi(new Dictionary<string, string?>());

            VerificationEngine engine = new VerificationEngine(Log);
            return engine.Execute(options, TestContext.CancellationToken);
        }

        [TestMethod]
        public Task TemplateWithOnlyIfStatementTest()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithOnlyIfStatement");

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateWithOnlyIfStatement")
                {
                    TemplatePath = templateLocation,
                    SnapshotsDirectory = ApprovalsDirectory,
                    DoNotPrependTemplateNameToScenarioName = true,
                    DoNotAppendTemplateArgsToScenarioName = true,
                    ScenarioName = "Basic"
                }
                .WithInstantiationThroughTemplateCreatorApi(new Dictionary<string, string?>() { { "default-port", "3332" } });

            VerificationEngine engine = new VerificationEngine(Log);
            return engine.Execute(options, TestContext.CancellationToken);
        }

        [TestMethod]
        public Task TemplateWithOnlyIfStatementTestForLocalhostTest()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithOnlyIfForLocalhost");

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateWithOnlyIfForLocalhost")
                {
                    TemplatePath = templateLocation,
                    SnapshotsDirectory = ApprovalsDirectory,
                    DoNotPrependTemplateNameToScenarioName = true,
                    DoNotAppendTemplateArgsToScenarioName = true,
                    ScenarioName = "Basic"
                }
                .WithInstantiationThroughTemplateCreatorApi(new Dictionary<string, string?>() { { "default-port", "3332" } });

            VerificationEngine engine = new VerificationEngine(Log);
            return engine.Execute(options, TestContext.CancellationToken);
        }

        [TestMethod]
        [DataRow("no options", null)]
        [DataRow("options without value", new[] { "A", null, "B", null, "C", null, "D", null })]
        [DataRow("option equals to false", new[] { "A", "false", "B", "false", "C", "false", "D", "false" })]
        [DataRow("option equals to true", new[] { "A", "true", "B", "true", "C", "true", "D", "true" })]
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
                SnapshotsDirectory = ApprovalsDirectory,
                OutputDirectory = workingDirectory,
                DoNotPrependTemplateNameToScenarioName = true,
                DoNotAppendTemplateArgsToScenarioName = true,
                ScenarioName = testName,
            }
            .WithInstantiationThroughTemplateCreatorApi(parameters);

            VerificationEngine engine = new(Log);
            await engine.Execute(options, TestContext.CancellationToken);
        }

        [TestMethod]
        [DataRow("DefaultIfOptionWithoutValue_NoValueDefaultsNotUsedIfSwitchesNotSpecified", "DefaultIfOptionWithoutValue", "TestAssets.DefaultIfOptionWithoutValue", null)]
        [DataRow("DefaultIfOptionWithoutValue_NoValueDefaultForChoiceParamIsUsed", "DefaultIfOptionWithoutValue", "TestAssets.DefaultIfOptionWithoutValue", new[] { "MyChoice", null })]
        [DataRow("DefaultIfOptionWithoutValue_NoValueDefaultForStringParamIsUsed", "DefaultIfOptionWithoutValue", "TestAssets.DefaultIfOptionWithoutValue", new[] { "MyString", null })]
        [DataRow("DefaultIfOptionWithoutValue_NoValueDefault_UserProvidedValuesAreIsUsed", "DefaultIfOptionWithoutValue", "TestAssets.DefaultIfOptionWithoutValue", new[] { "MyString", "UserString", "MyChoice", "OtherChoice" })]
        [DataRow("Renames_FileRenames", "TemplateWithRenames", "TestAssets.TemplateWithRenames", new[] { "foo", "baz", "testForms", "TestProject" })]
        [DataRow("Renames_SourceNameFileRenames", "TemplateWithSourceName", "TestAssets.TemplateWithSourceName", new[] { "name", "baz" })]
        [DataRow("Renames_NegativeFileRenames", "TemplateWithUnspecifiedSourceName", "TestAssets.TemplateWithUnspecifiedSourceName", new[] { "name", "baz" })]
        [DataRow("Renames_CustomSourcePathRename", "TemplateWithSourceNameAndCustomSourcePath", "TestAssets.TemplateWithSourceNameAndCustomSourcePath", new[] { "name", "bar" })]
        [DataRow("Renames_CustomTargetPathRename", "TemplateWithSourceNameAndCustomTargetPath", "TestAssets.TemplateWithSourceNameAndCustomTargetPath", new[] { "name", "bar" })]
        [DataRow("Renames_CustomSourceAndTargetPathRename", "TemplateWithSourceNameAndCustomSourceAndTargetPaths", "TestAssets.TemplateWithSourceNameAndCustomSourceAndTargetPaths", new[] { "name", "bar" })]
        [DataRow("Renames_SourcePathOutsideConfigRoot", "TemplateWithSourcePathOutsideConfigRoot", "TestAssets.TemplateWithSourcePathOutsideConfigRoot", new[] { "name", "baz" })]
        [DataRow("Renames_SourceNameInTargetPathGetsRenamed", "TemplateWithSourceNameInTargetPathGetsRenamed", "TestAssets.TemplateWithSourceNameInTargetPathGetsRenamed", new[] { "name", "baz" })]
        [DataRow("Renames_PlaceholderFiles", "TemplateWithPlaceholderFiles", "TestAssets.TemplateWithPlaceholderFiles", null)]
        [DataRow("Renames_DerivedSymbolFileRename", "TemplateWithDerivedSymbolFileRename", "TestAssets.TemplateWithDerivedSymbolFileRename", new[] { "name", "Last.Part.Is.For.Rename" })]
        [DataRow("Renames_MultipleRenamesOnSameFile", "TemplateWithMultipleRenamesOnSameFile", "TestAssets.TemplateWithMultipleRenamesOnSameFile", new[] { "fooRename", "base", "barRename", "ball" })]
        [DataRow("Renames_MultipleRenamesOnSameFileHandlesOverlap", "TemplateWithMultipleRenamesOnSameFileHandlesOverlap", "TestAssets.TemplateWithMultipleRenamesOnSameFileHandlesOverlap", new[] { "fooRename", "pin", "oobRename", "ball" })]
        [DataRow("Renames_MultipleRenamesOnSameFileHandlesInducedOverlap", "TemplateWithMultipleRenamesOnSameFileHandlesInducedOverlap", "TestAssets.TemplateWithMultipleRenamesOnSameFileHandlesInducedOverlap", new[] { "fooRename", "bar", "barRename", "baz" })]
        [DataRow("Renames_CaseSensitiveNameBasedRenames", "TemplateWithCaseSensitiveNameBasedRenames", "TestAssets.TemplateWithCaseSensitiveNameBasedRenames", new[] { "name", "NewName" })]
        [DataRow("Renames_JoinAndFolderRename", "TemplateWithJoinAndFolderRename", "TestAssets.TemplateWithJoinAndFolderRename", new[] { "name", "NewName", "product", "Office" })]
        [DataRow("KitchenSink_ConfigurationKitchenSink", "ConfigurationKitchenSink", "TestAssets.ConfigurationKitchenSink", new[] { "replaceThings", "Stuff", "replaceThere", "You" })]
        [DataRow("RegexMatch_RegexMatchMacroPositive", "TemplateWithRegexMatchMacro", "TestAssets.TemplateWithRegexMatchMacro", new[] { "name", "hello" })]
        [DataRow("RegexMatch_RegexMatchMacroNegative", "TemplateWithRegexMatchMacro", "TestAssets.TemplateWithRegexMatchMacro", new[] { "name", "there" })]
        [DataRow("TemplateWithTagsBasicTest", "TemplateWithTags", "TestAssets.TemplateWithTags", null)]
        [DataRow("ValueForms", "TemplateWithValueForms", "TestAssets.TemplateWithValueForms", new[] { "foo", "Test.Value6", "param1", "MyPascalTestValue", "param2", "myCamelTestValue", "param3", "my test text" })]
        [DataRow("ValueForms_DerivedSymbolWithValueForms", "TemplateWithDerivedSymbolWithValueForms", "TestAssets.TemplateWithDerivedSymbolWithValueForms", new[] { "n", "Test.AppSeven" })]
        public async Task LegacyTests(string scenarioName, string templateFolderName, string templateShortName, string?[]? parametersArray)
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string templateLocation = Path.Combine(TestTemplatesLocation, templateFolderName);
            Dictionary<string, string?> parameters = ConvertToParameters(parametersArray);
            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplatePath = templateLocation,
                SnapshotsDirectory = ApprovalsDirectory,
                OutputDirectory = workingDirectory,
                DoNotPrependTemplateNameToScenarioName = true,
                DoNotAppendTemplateArgsToScenarioName = false,
                ScenarioName = scenarioName,
            }
            .WithInstantiationThroughTemplateCreatorApi(parameters);

            VerificationEngine engine = new(Log);
            await engine.Execute(options, TestContext.CancellationToken);
        }

        [TestMethod]
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
                SnapshotsDirectory = ApprovalsDirectory,
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

            VerificationEngine engine = new(Log);
            await engine.Execute(options, TestContext.CancellationToken);
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
