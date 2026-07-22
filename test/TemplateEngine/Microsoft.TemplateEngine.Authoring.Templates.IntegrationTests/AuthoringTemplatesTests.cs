// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Authoring.TemplateApiVerifier;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;

namespace Microsoft.TemplateEngine.Authoring.Templates.Tests
{
    [TestClass]
    public class AuthoringTemplatesTests : TestBase
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
            // The shared TemplateVerifier engine is compiled against the xUnit Verify adapter; route its directory
            // verification to the MSTest adapter so the ambient MSTest test context is used under MTP.
            VerificationEngine.DirectoryVerifier = VerifyMSTest.Verifier.VerifyDirectory;
        }

        [TestMethod]
        public async Task TemplateJsonTest()
        {
            string workingDir = TestUtils.CreateTemporaryFolder();
            string templateShortName = "template.json";

            //get the template location
            string templateLocation = Path.Combine(TemplateFeedLocation, "Microsoft.TemplateEngine.Authoring.Templates");

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplatePath = templateLocation,
                SnapshotsDirectory = SnapshotsDirectory,
                OutputDirectory = workingDir,
                DoNotPrependCallerMethodNameToScenarioName = true,
                ScenarioName = "Basic",
            }
            .WithInstantiationThroughTemplateCreatorApi(new Dictionary<string, string?>());
            VerificationEngine engine = new VerificationEngine(Log);
            await engine.Execute(options, TestContext.Current!.CancellationToken);
        }

        [TestMethod]
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
                SnapshotsDirectory = SnapshotsDirectory,
                OutputDirectory = workingDir,
                DoNotPrependCallerMethodNameToScenarioName = true,
                ScenarioName = "WithParams",
            }
                .WithInstantiationThroughTemplateCreatorApi(templateParams);

            VerificationEngine engine = new VerificationEngine(Log);
            await engine.Execute(options, TestContext.Current!.CancellationToken);
        }

        [TestMethod]
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
                SnapshotsDirectory = SnapshotsDirectory,
                OutputDirectory = workingDir,
                DoNotPrependCallerMethodNameToScenarioName = true,
                ScenarioName = "NoConfigFolder",
            }
            .WithInstantiationThroughTemplateCreatorApi(templateParams);

            VerificationEngine engine = new VerificationEngine(Log);
            await engine.Execute(options, TestContext.Current!.CancellationToken);
        }

        [TestMethod]
        public async Task TemplatePackageTest()
        {
            string workingDir = TestUtils.CreateTemporaryFolder();
            string templateShortName = "templatepack";

            //get the template location
            string templateLocation = Path.Combine(TemplateFeedLocation, "Microsoft.TemplateEngine.Authoring.Templates");

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplatePath = templateLocation,
                SnapshotsDirectory = SnapshotsDirectory,
                OutputDirectory = workingDir,
                DoNotPrependCallerMethodNameToScenarioName = true,
                ScenarioName = "Basic",
            }
            .WithInstantiationThroughTemplateCreatorApi(new Dictionary<string, string?>());
            VerificationEngine engine = new VerificationEngine(Log);
            await engine.Execute(options, TestContext.Current!.CancellationToken);
        }

        [TestMethod]
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
                SnapshotsDirectory = SnapshotsDirectory,
                OutputDirectory = workingDir,
                DoNotPrependCallerMethodNameToScenarioName = true,
                ScenarioName = "WithName",
            }
            .WithInstantiationThroughTemplateCreatorApi(templateParams);

            VerificationEngine engine = new VerificationEngine(Log);
            await engine.Execute(options, TestContext.Current!.CancellationToken);
        }

        [TestMethod]
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
                SnapshotsDirectory = SnapshotsDirectory,
                OutputDirectory = workingDir,
                DoNotPrependCallerMethodNameToScenarioName = true,
                ScenarioName = "NoMSBuildTasks",
            }
            .WithInstantiationThroughTemplateCreatorApi(templateParams);

            VerificationEngine engine = new VerificationEngine(Log);
            await engine.Execute(options, TestContext.Current!.CancellationToken);
        }

        [TestMethod]
        public async Task TemplateJsonTest_CLI()
        {
            string workingDir = TestUtils.CreateTemporaryFolder();
            string templateShortName = "template.json";

            //get the template location
            string templateLocation = Path.Combine(TemplateFeedLocation, "Microsoft.TemplateEngine.Authoring.Templates");

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplatePath = templateLocation,
                SnapshotsDirectory = SnapshotsDirectory,
                OutputDirectory = workingDir,
                DoNotPrependCallerMethodNameToScenarioName = true,
                VerifyCommandOutput = true,
                ScenarioName = "CLI",
            };
            VerificationEngine engine = new VerificationEngine(Log);
            await engine.Execute(options, TestContext.Current!.CancellationToken);
        }

        [TestMethod]
        public async Task TemplatePackageTest_CLI()
        {
            string workingDir = TestUtils.CreateTemporaryFolder();
            string templateShortName = "templatepack";

            //get the template location
            string templateLocation = Path.Combine(TemplateFeedLocation, "Microsoft.TemplateEngine.Authoring.Templates");

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplatePath = templateLocation,
                SnapshotsDirectory = SnapshotsDirectory,
                OutputDirectory = workingDir,
                DoNotPrependCallerMethodNameToScenarioName = true,
                VerifyCommandOutput = true,
                ScenarioName = "CLI",
            };
            VerificationEngine engine = new VerificationEngine(Log);
            await engine.Execute(options, TestContext.Current!.CancellationToken);
        }
    }
}
