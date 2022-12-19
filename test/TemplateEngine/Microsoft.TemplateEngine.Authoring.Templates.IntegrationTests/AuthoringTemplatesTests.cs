// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Authoring.TemplateApiVerifier;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.Authoring.Templates.Tests
{
    public class AuthoringTemplatesTests : TestBase
    {
        private readonly ILogger _log;

        public AuthoringTemplatesTests(ITestOutputHelper log)
        {
            _log = new XunitLoggerProvider(log).CreateLogger("TestRun");
        }

        [Fact]
        public async Task TemplateJsonTest()
        {
            string workingDir = TestUtils.CreateTemporaryFolder();
            string templateShortName = "template.json";

            //get the template location
            string templateLocation = Path.Combine(TemplateFeedLocation, "Microsoft.TemplateEngine.Authoring.Templates");

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplatePath = templateLocation,
                SnapshotsDirectory = "Snapshots",
                OutputDirectory = workingDir,
                DoNotPrependCallerMethodNameToScenarioName = true,
                ScenarioName = "Basic",
            }
            .WithInstantiationThroughTemplateCreatorApi(new Dictionary<string, string?>());
            VerificationEngine engine = new VerificationEngine(_log);
            await engine.Execute(options);
        }

        [Fact]
        public async Task TemplateJsonTest_WithParameters()
        {
            string workingDir = TestUtils.CreateTemporaryFolder();
            string templateShortName = "template.json";

            //get the template location
            string templateLocation = Path.Combine(TemplateFeedLocation, "Microsoft.TemplateEngine.Authoring.Templates");

            var templateParams = new Dictionary<string, string?>()
            {
                { "TemplateName", "MyTemplate" },
                { "TemplateShortName", "mytemplate" },
                { "TemplateIdentity", "My.Template.Identity" },
            };

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplatePath = templateLocation,
                SnapshotsDirectory = "Snapshots",
                OutputDirectory = workingDir,
                DoNotPrependCallerMethodNameToScenarioName = true,
                ScenarioName = "WithParams",
            }
                .WithInstantiationThroughTemplateCreatorApi(templateParams);

            VerificationEngine engine = new VerificationEngine(_log);
            await engine.Execute(options);
        }

        [Fact]
        public async Task TemplateJsonTest_NoConfigFolder()
        {
            string workingDir = TestUtils.CreateTemporaryFolder();
            string templateShortName = "template.json";

            //get the template location
            string templateLocation = Path.Combine(TemplateFeedLocation, "Microsoft.TemplateEngine.Authoring.Templates");

            var templateParams = new Dictionary<string, string?>()
            {
                { "CreateTemplateConfigFolder", "false" }
            };

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplatePath = templateLocation,
                SnapshotsDirectory = "Snapshots",
                OutputDirectory = workingDir,
                DoNotPrependCallerMethodNameToScenarioName = true,
                ScenarioName = "NoConfigFolder",
            }
            .WithInstantiationThroughTemplateCreatorApi(templateParams);

            VerificationEngine engine = new VerificationEngine(_log);
            await engine.Execute(options);
        }

        [Fact]
        public async Task TemplatePackageTest()
        {
            string workingDir = TestUtils.CreateTemporaryFolder();
            string templateShortName = "templatepack";

            //get the template location
            string templateLocation = Path.Combine(TemplateFeedLocation, "Microsoft.TemplateEngine.Authoring.Templates");

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplatePath = templateLocation,
                SnapshotsDirectory = "Snapshots",
                OutputDirectory = workingDir,
                DoNotPrependCallerMethodNameToScenarioName = true,
                ScenarioName = "Basic",
            }
            .WithInstantiationThroughTemplateCreatorApi(new Dictionary<string, string?>());
            VerificationEngine engine = new VerificationEngine(_log);
            await engine.Execute(options);
        }

        [Fact]
        public async Task TemplatePackageTest_WithName()
        {
            string workingDir = TestUtils.CreateTemporaryFolder();
            string templateShortName = "templatepack";

            //get the template location
            string templateLocation = Path.Combine(TemplateFeedLocation, "Microsoft.TemplateEngine.Authoring.Templates");

            var templateParams = new Dictionary<string, string?>()
            {
                { "name", "MyTemplatePackage" },
            };

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplatePath = templateLocation,
                SnapshotsDirectory = "Snapshots",
                OutputDirectory = workingDir,
                DoNotPrependCallerMethodNameToScenarioName = true,
                ScenarioName = "WithName",
            }
            .WithInstantiationThroughTemplateCreatorApi(templateParams);

            VerificationEngine engine = new VerificationEngine(_log);
            await engine.Execute(options);
        }

        [Fact]
        public async Task TemplatePackageTest_NoMSBuildTasks()
        {
            string workingDir = TestUtils.CreateTemporaryFolder();
            string templateShortName = "templatepack";

            //get the template location
            string templateLocation = Path.Combine(TemplateFeedLocation, "Microsoft.TemplateEngine.Authoring.Templates");

            var templateParams = new Dictionary<string, string?>()
            {
                { "EnableMSBuildTasks", "false" },
            };

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplatePath = templateLocation,
                SnapshotsDirectory = "Snapshots",
                OutputDirectory = workingDir,
                DoNotPrependCallerMethodNameToScenarioName = true,
                ScenarioName = "NoMSBuildTasks",
            }
            .WithInstantiationThroughTemplateCreatorApi(templateParams);

            VerificationEngine engine = new VerificationEngine(_log);
            await engine.Execute(options);
        }

        [Fact]
        public async Task TemplateJsonTest_CLI()
        {
            string workingDir = TestUtils.CreateTemporaryFolder();
            string templateShortName = "template.json";

            //get the template location
            string templateLocation = Path.Combine(TemplateFeedLocation, "Microsoft.TemplateEngine.Authoring.Templates");

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplatePath = templateLocation,
                SnapshotsDirectory = "Snapshots",
                OutputDirectory = workingDir,
                DoNotPrependCallerMethodNameToScenarioName = true,
                VerifyCommandOutput = true,
                ScenarioName = "CLI",
            };
            VerificationEngine engine = new VerificationEngine(_log);
            await engine.Execute(options);
        }

        [Fact]
        public async Task TemplatePackageTest_CLI()
        {
            string workingDir = TestUtils.CreateTemporaryFolder();
            string templateShortName = "templatepack";

            //get the template location
            string templateLocation = Path.Combine(TemplateFeedLocation, "Microsoft.TemplateEngine.Authoring.Templates");

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplatePath = templateLocation,
                SnapshotsDirectory = "Snapshots",
                OutputDirectory = workingDir,
                DoNotPrependCallerMethodNameToScenarioName = true,
                VerifyCommandOutput = true,
                ScenarioName = "CLI",
            };
            VerificationEngine engine = new VerificationEngine(_log);
            await engine.Execute(options);
        }
    }
}
