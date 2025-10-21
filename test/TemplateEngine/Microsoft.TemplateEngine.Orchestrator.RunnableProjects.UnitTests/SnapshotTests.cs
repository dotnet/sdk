// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Authoring.TemplateApiVerifier;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests
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
        public Task TestGeneratedSymbolWithRefToDerivedSymbol()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithGeneratedSymbolWithRefToDerivedSymbol");
            var templateParams = new Dictionary<string, string?>()
            {
                { "NugetToolName", "nuget" }
            };
            string workingDir = TestUtils.CreateTemporaryFolder();

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateGenSymWithRefToDerivedSym")
                {
                    TemplatePath = templateLocation,
                    OutputDirectory = workingDir,
                    DoNotAppendTemplateArgsToScenarioName = true,
                    DoNotPrependTemplateNameToScenarioName = true,
                    SnapshotsDirectory = "Approvals"
                }
                .WithInstantiationThroughTemplateCreatorApi(templateParams);

            VerificationEngine engine = new VerificationEngine(_log);
            return engine.Execute(options);
        }

        [Fact]
        public Task TestGeneratedSymbolWithRefToDerivedSymbol_DifferentOrder()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithGenSymbolWithRefToDerivedSymbol_DifferentOrder");
            var templateParams = new Dictionary<string, string?>()
            {
                { "NugetToolName", "nuget" }
            };
            string workingDir = TestUtils.CreateTemporaryFolder();

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateGenSymWithRefToDerivedSym_DiffOrder")
                {
                    TemplatePath = templateLocation,
                    OutputDirectory = workingDir,
                    DoNotAppendTemplateArgsToScenarioName = true,
                    DoNotPrependTemplateNameToScenarioName = true,
                    SnapshotsDirectory = "Approvals"
                }
                .WithInstantiationThroughTemplateCreatorApi(templateParams);

            VerificationEngine engine = new VerificationEngine(_log);
            return engine.Execute(options);
        }

        [Fact]
        public Task TestCoalesce_EmptyStringForMultiChoices()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithMultipleChoicesAndCoalesce");
            var templateParams = new Dictionary<string, string?>()
    {
        { "tests", string.Empty }
    };
            string workingDir = TestUtils.CreateTemporaryFolder();

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateWithMultipleChoicesAndCoalesce")
                {
                    TemplatePath = templateLocation,
                    OutputDirectory = workingDir,
                    DoNotAppendTemplateArgsToScenarioName = true,
                    DoNotPrependTemplateNameToScenarioName = true,
                    SnapshotsDirectory = "Approvals"
                }
                .WithInstantiationThroughTemplateCreatorApi(templateParams);

            VerificationEngine engine = new VerificationEngine(_log);
            return engine.Execute(options);
        }

        [Fact]
        public Task TestSingleSelectionForMultiChoices()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithMultipleChoicesAndCoalesce");
            var templateParams = new Dictionary<string, string?>()
            {
                { "tests", "unit" }
            };
            string workingDir = TestUtils.CreateTemporaryFolder();

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateWithMultipleChoicesAndCoalesce")
                {
                    TemplatePath = templateLocation,
                    OutputDirectory = workingDir,
                    DoNotAppendTemplateArgsToScenarioName = true,
                    DoNotPrependTemplateNameToScenarioName = true,
                    SnapshotsDirectory = "Approvals"
                }
                .WithInstantiationThroughTemplateCreatorApi(templateParams);

            VerificationEngine engine = new VerificationEngine(_log);
            return engine.Execute(options);
        }

        [Fact]
        public Task TestTemplateWithBrokenGeneratedInComputed()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithBrokenGeneratedInComputed");
            var templateParams = new Dictionary<string, string?>()
            {
                { "navigation", "regions" }
            };
            string workingDir = TestUtils.CreateTemporaryFolder();

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateWithBrokenGeneratedInComputed")
                {
                    TemplatePath = templateLocation,
                    OutputDirectory = workingDir,
                    DoNotAppendTemplateArgsToScenarioName = true,
                    DoNotPrependTemplateNameToScenarioName = true,
                    VerifyCommandOutput = true,
                    SnapshotsDirectory = "Approvals"
                }
                .WithInstantiationThroughTemplateCreatorApi(templateParams);

            VerificationEngine engine = new VerificationEngine(_log);
            return engine.Execute(options);
        }

        [Fact]
        public Task TestTemplateWithComputedInGenerated()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithComputedInGenerated");
            string workingDir = TestUtils.CreateTemporaryFolder();

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateWithComputedInGenerated")
                {
                    TemplatePath = templateLocation,
                    OutputDirectory = workingDir,
                    DoNotAppendTemplateArgsToScenarioName = true,
                    DoNotPrependTemplateNameToScenarioName = true,
                    VerifyCommandOutput = true,
                    SnapshotsDirectory = "Approvals"
                }
                .WithInstantiationThroughTemplateCreatorApi(
                    new Dictionary<string, string?>()
                    {
                        { "preset", "recommended" }
                    });

            VerificationEngine engine = new VerificationEngine(_log);
            return engine.Execute(options);
        }

        [Fact]
        public Task TestTemplateWithComputedInDerivedThroughGenerated()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithComputedInDerivedThroughGenerated");
            string workingDir = TestUtils.CreateTemporaryFolder();

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateWithComputedInDerivedThroughGenerated")
                {
                    TemplatePath = templateLocation,
                    OutputDirectory = workingDir,
                    DoNotAppendTemplateArgsToScenarioName = true,
                    DoNotPrependTemplateNameToScenarioName = true,
                    VerifyCommandOutput = true,
                    SnapshotsDirectory = "Approvals"
                }
                .WithInstantiationThroughTemplateCreatorApi(new Dictionary<string, string?>());

            VerificationEngine engine = new VerificationEngine(_log);
            return engine.Execute(options);
        }

        [Fact]
        public Task TestTemplateWithGeneratedInComputed()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithGeneratedInComputed");
            string workingDir = TestUtils.CreateTemporaryFolder();

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateWithGeneratedInComputed")
                {
                    TemplatePath = templateLocation,
                    OutputDirectory = workingDir,
                    DoNotAppendTemplateArgsToScenarioName = true,
                    DoNotPrependTemplateNameToScenarioName = true,
                    VerifyCommandOutput = true,
                    SnapshotsDirectory = "Approvals"
                }
                .WithInstantiationThroughTemplateCreatorApi(
                    new Dictionary<string, string?>()
                    {
                        { "navigation",  "regions" },
                        { "dependencyInjection", "true" }
                    });

            VerificationEngine engine = new VerificationEngine(_log);
            return engine.Execute(options);
        }

        [Fact]
        public Task TestTemplateWithGeneratedSwitchInComputed()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithGeneratedSwitchInComputed");
            string workingDir = TestUtils.CreateTemporaryFolder();

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateWithGeneratedSwitchInComputed")
                {
                    TemplatePath = templateLocation,
                    OutputDirectory = workingDir,
                    DoNotAppendTemplateArgsToScenarioName = true,
                    DoNotPrependTemplateNameToScenarioName = true,
                    VerifyCommandOutput = true,
                    SnapshotsDirectory = "Approvals"
                }
                .WithInstantiationThroughTemplateCreatorApi(
                    new Dictionary<string, string?>()
                    {
                        { "navigation",  "regions" },
                        { "dependencyInjection", "true" }
                    });

            VerificationEngine engine = new VerificationEngine(_log);
            return engine.Execute(options);
        }

        [Fact]
        public Task TestTemplateWithCircleDependencyInMacros()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithCircleDependencyInMacros");

            string workingDir = TestUtils.CreateTemporaryFolder();

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateWithCircleDependencyInMacros")
                {
                    TemplatePath = templateLocation,
                    OutputDirectory = workingDir,
                    DoNotAppendTemplateArgsToScenarioName = true,
                    DoNotPrependTemplateNameToScenarioName = true,
                    VerifyCommandOutput = true,
                    IsCommandExpectedToFail = true,
                    SnapshotsDirectory = "Approvals"
                }
                .WithInstantiationThroughTemplateCreatorApi(new Dictionary<string, string?>());

            VerificationEngine engine = new VerificationEngine(_log);
            return engine.Execute(options);
        }

        [Fact]
        public Task TestSelectionForMultiChoicesWhenThereAreMultiplePartialMatchesAndOnePreciseMatch()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithMultipleChoicesAndPartialMatches");
            var templateParams = new Dictionary<string, string?>()
            {
                // There are multiple choices for the parameter that overlap: "aab", "aac", "aa".
                // We want to ensure that "aa" can be selected, because it is a precise match,
                // even if there are more than one choices that start with "aa", and even if "aa",
                // is not listed first in the list of choices.
                { "tests", "aa" }
            };
            string workingDir = TestUtils.CreateTemporaryFolder();

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateWithMultipleChoicesAndPartialMatches")
                {
                    TemplatePath = templateLocation,
                    OutputDirectory = workingDir,
                    DoNotAppendTemplateArgsToScenarioName = true,
                    DoNotPrependTemplateNameToScenarioName = true,
                    SnapshotsDirectory = "Approvals"
                }
                .WithInstantiationThroughTemplateCreatorApi(templateParams);

            VerificationEngine engine = new VerificationEngine(_log);
            return engine.Execute(options);
        }
    }
}

#endif

