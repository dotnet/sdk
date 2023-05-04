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
    }
}

#endif
