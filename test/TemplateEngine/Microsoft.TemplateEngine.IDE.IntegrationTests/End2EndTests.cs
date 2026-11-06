// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.TestHelper;
using ITemplateMatchInfo = Microsoft.TemplateEngine.Abstractions.TemplateFiltering.ITemplateMatchInfo;
using WellKnownSearchFilters = Microsoft.TemplateEngine.Utils.WellKnownSearchFilters;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    [TestClass]
    public class End2EndTests : BootstrapperTestBase
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

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            new VerifySettingsFixture();
        }
        [TestMethod]
        public async Task SourceNameFormsTest()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("SourceNameForms");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.SourceNameForms") }, cancellationToken: TestContext.CancellationToken);
            var result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "MyApp.1", output, new Dictionary<string, string?>(), cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(CreationResultStatus.Success, result.Status);

            string targetFile = Path.Combine(output, "myapp.12.cs");
            Assert.IsTrue(File.Exists(targetFile));
            string targetFile2 = Path.Combine(output, "MyApp.1.cs");
            Assert.IsTrue(File.Exists(targetFile2));

            await Verifier.Verify(File.ReadAllText(targetFile2));
        }

        [TestMethod]
        public async Task ValueForms_DerivedSymbolTest()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("ValueForms/DerivedSymbol");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.ValueForms.DerivedSymbol") }, cancellationToken: TestContext.CancellationToken);
            var result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "Real.Web.App", output, new Dictionary<string, string?>(), cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(CreationResultStatus.Success, result.Status);

            string targetFile = Path.Combine(output, "Real.Web.App.txt");
            Assert.IsTrue(File.Exists(targetFile));

            await Verifier.Verify(File.ReadAllText(targetFile));
        }

        [TestMethod]
        [Ignore("https://github.com/dotnet/templating/issues/5115")]
        public async Task ValueForms_DerivedSymbolFromGeneratedSymbolTest()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("ValueForms/DerivedSymbolFromGeneratedSymbol");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.ValueForms.DerivedSymbolFromGeneratedSymbol") }, cancellationToken: TestContext.CancellationToken);
            var result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "Real.Web.App", output, new Dictionary<string, string?>(), cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(CreationResultStatus.Success, result.Status);

            string targetFile = Path.Combine(output, "Real.Web.App.txt");
            Assert.IsTrue(File.Exists(targetFile));

            await Verifier.Verify(File.ReadAllText(targetFile));
        }

        [TestMethod]
        public async Task PortAndCoalesceTest_WithFallbackInput()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithPortsAndCoalesce");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();

            IReadOnlyList<ITemplateMatchInfo> foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithPortsAndCoalesce") }, cancellationToken: TestContext.CancellationToken);
            ITemplateCreationResult result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "test-template", output, new Dictionary<string, string?>(), cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(CreationResultStatus.Success, result.Status);

            string targetFile = Path.Combine(output, "bar.cs");
            Assert.IsTrue(File.Exists(targetFile));
            string fileContent = File.ReadAllText(targetFile);
            Assert.IsTrue(Regex.Match(
                fileContent,
                """
                The port is (\d{4,5})
                The port is (\d{4,5})
                
                """).Success);
            Assert.AreNotEqual(
                """
                The port is 1234
                The port is 1235

                """,
                fileContent);
        }

        [TestMethod]
        public async Task PortAndCoalesceTest_WithUserInput()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithPortsAndCoalesce");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();
            Dictionary<string, string?> parameters = new()
            {
                { "userPort1", "4000" },
                { "userPort2", "3000" },
            };

            IReadOnlyList<ITemplateMatchInfo> foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithPortsAndCoalesce") }, cancellationToken: TestContext.CancellationToken);
            ITemplateCreationResult result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "test-template", output, parameters, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(CreationResultStatus.Success, result.Status);

            string targetFile = Path.Combine(output, "bar.cs");
            Assert.IsTrue(File.Exists(targetFile));
            string fileContent = File.ReadAllText(targetFile);
            Assert.AreEqual(
                """
                The port is 4000
                The port is 3000

                """,
                fileContent);
        }

        [TestMethod]
        public async Task PortAndCoalesceTest_WithUserInputEqualToDefaults()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithPortsAndCoalesce");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();
            Dictionary<string, string?> parameters = new()
            {
                { "userPort1", "0" },
                { "userPort2", "0" },
            };

            IReadOnlyList<ITemplateMatchInfo> foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithPortsAndCoalesce") }, cancellationToken: TestContext.CancellationToken);
            ITemplateCreationResult result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "test-template", output, parameters, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(CreationResultStatus.Success, result.Status);

            string targetFile = Path.Combine(output, "bar.cs");
            Assert.IsTrue(File.Exists(targetFile));
            string fileContent = File.ReadAllText(targetFile);
            Assert.AreEqual(
                """
                The port is 0
                The port is 0

                """,
                fileContent);
        }

        [TestMethod]
        public async Task StringCoalesceTest_WithFallbackInput()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithStringCoalesce");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();

            IReadOnlyList<ITemplateMatchInfo> foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithStringCoalesce") }, cancellationToken: TestContext.CancellationToken);
            ITemplateCreationResult result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "test-template", output, new Dictionary<string, string?>(), cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(CreationResultStatus.Success, result.Status);

            string targetFile = Path.Combine(output, "bar.cs");
            Assert.IsTrue(File.Exists(targetFile));
            string fileContent = File.ReadAllText(targetFile);
            Assert.AreEqual(
                """
                var str = "fallback";
                
                """,
                fileContent);
        }

        [TestMethod]
        public async Task StringCoalesceTest_WithUserInput()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithStringCoalesce");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();
            Dictionary<string, string?> parameters = new()
            {
                { "userVal", "myVal" },
            };

            IReadOnlyList<ITemplateMatchInfo> foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithStringCoalesce") }, cancellationToken: TestContext.CancellationToken);
            ITemplateCreationResult result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "test-template", output, parameters, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(CreationResultStatus.Success, result.Status);

            string targetFile = Path.Combine(output, "bar.cs");
            Assert.IsTrue(File.Exists(targetFile));
            string fileContent = File.ReadAllText(targetFile);
            Assert.AreEqual(
                """
                var str = "myVal";
                
                """,
                fileContent);
        }

        [TestMethod]
        public async Task StringCoalesceTest_WithEmptyUserInput()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithStringCoalesce");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();
#pragma warning disable SA1122 // Use string.Empty for empty strings
            Dictionary<string, string?> parameters = new()
            {
                { "userVal", "" },
            };
#pragma warning restore SA1122 // Use string.Empty for empty strings

            IReadOnlyList<ITemplateMatchInfo> foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithStringCoalesce") }, cancellationToken: TestContext.CancellationToken);
            ITemplateCreationResult result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "test-template", output, parameters, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(CreationResultStatus.Success, result.Status);

            string targetFile = Path.Combine(output, "bar.cs");
            Assert.IsTrue(File.Exists(targetFile));
            string fileContent = File.ReadAllText(targetFile);
            Assert.AreEqual(
                """
                var str = "fallback";
                
                """,
                fileContent);
        }

        [TestMethod]
        public async Task StringCoalesceTest_WithNullUserInput()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithStringCoalesce");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();
            Dictionary<string, string?> parameters = new()
            {
                { "userVal", null },
            };

            IReadOnlyList<ITemplateMatchInfo> foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithStringCoalesce") }, cancellationToken: TestContext.CancellationToken);
            ITemplateCreationResult result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "test-template", output, parameters, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(CreationResultStatus.Success, result.Status);

            string targetFile = Path.Combine(output, "bar.cs");
            Assert.IsTrue(File.Exists(targetFile));
            string fileContent = File.ReadAllText(targetFile);
            Assert.AreEqual(
                """
                var str = "A";
                
                """,
                fileContent);
        }

        [TestMethod]
        public async Task Test_CreateAsync_OnInvalidParamsPassed()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithPortsAndCoalesce");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();

            IReadOnlyList<ITemplateMatchInfo> foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithPortsAndCoalesce") }, cancellationToken: TestContext.CancellationToken);

            Dictionary<string, string?> parameters = new()
            {
                { "userPort1", "non-int" },
                { "userPort2", string.Empty }
            };

            ITemplateCreationResult result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "Test", output, parameters, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(CreationResultStatus.InvalidParamValues, result.Status);
            Assert.AreEqual("userPort1, userPort2", result.ErrorMessage);
        }

        [TestMethod]
        public async Task Test_DryRunAsync_OnInvalidParamsPassed()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithPortsAndCoalesce");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();

            IReadOnlyList<ITemplateMatchInfo> foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithPortsAndCoalesce") }, cancellationToken: TestContext.CancellationToken);

            Dictionary<string, string?> parameters = new()
            {
                { "userPort1", "non-int" },
                { "userPort2", string.Empty }
            };

            ITemplateCreationResult result = await bootstrapper.GetCreationEffectsAsync(foundTemplates[0].Info, "Test", output, parameters, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(CreationResultStatus.InvalidParamValues, result.Status);
            Assert.AreEqual("userPort1, userPort2", result.ErrorMessage);
        }

        [TestMethod]
        public async Task Test_CreateAsync_OnTemplateWithConditions()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithConditions");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();

            IReadOnlyList<ITemplateMatchInfo> foundTemplates = await bootstrapper
                .GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithConditions") }, cancellationToken: TestContext.CancellationToken);

            Dictionary<string, string?> parameters = new()
            {
                { "B", "true" },
            };

            ITemplateCreationResult result = await bootstrapper
                .CreateAsync(foundTemplates[0].Info, "Test", output, parameters, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(CreationResultStatus.Success, result.Status);

            foreach (var expectResult in ExpectedOutputWithConditions())
            {
                string targetFile = Path.Combine(output, expectResult.Key);
                Assert.IsTrue(File.Exists(targetFile));
                Assert.AreEqual(expectResult.Value.UnixifyLineBreaks(), File.ReadAllText(targetFile).UnixifyLineBreaks());
            }
        }

        [TestMethod]
        [DataRow(null, "theDefaultName.cs")]
        [DataRow("fileName", "fileName.cs")]
        public async Task Test_CreateAsync_PreferDefaultNameValidParameters(string? name, string expectedFileName)
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithPreferDefaultName");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();

            IReadOnlyList<ITemplateMatchInfo> foundTemplates = await bootstrapper
                .GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithPreferDefaultName") }, cancellationToken: TestContext.CancellationToken);

            // Using this parameter with no real info so bootstrapper.CreateAsync is not an ambiguous call
            Dictionary<string, string?> parameters = new()
            {
                { "some", "parameter" },
            };

            ITemplateCreationResult result = await bootstrapper
                .CreateAsync(foundTemplates[0].Info, name, output, parameters, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(CreationResultStatus.Success, result.Status);
            string expectedName = Path.Combine(output, expectedFileName);
            Assert.IsTrue(File.Exists(expectedName));
        }

        [TestMethod]
        public async Task Test_CreateAsync_PreferDefaultNameInvalidParameters()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithPreferDefaultNameButNoDefaultName");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();

            IReadOnlyList<ITemplateMatchInfo> foundTemplates = await bootstrapper
                .GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithPreferDefaultName") }, cancellationToken: TestContext.CancellationToken);

            // Using this parameter with no real info so bootstrapper.CreateAsync is not an ambiguous call
            Dictionary<string, string?> parameters = new()
            {
                { "some", "parameter" },
            };

            ITemplateCreationResult result = await bootstrapper
                .CreateAsync(foundTemplates[0].Info, null, output, parameters, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(CreationResultStatus.TemplateIssueDetected, result.Status);
            Assert.AreEqual(
                "Failed to create template: the template name is not specified. Template configuration does not configure a default name that can be used when name is not specified. Specify the name for the template when instantiating or configure a default name in the template configuration.",
                result.ErrorMessage);
        }

        [TestMethod]
        public async Task PostAction_WithFileRename_Test()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("PostActions/WithFileRename");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();
            IReadOnlyList<ITemplateMatchInfo> foundTemplates = await bootstrapper
                .GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.PostActions.AddJsonProperty.WithSourceNameChangeInJson") }, cancellationToken: TestContext.CancellationToken);

            // Using this parameter with no real info so bootstrapper.CreateAsync is not an ambiguous call
            Dictionary<string, string?> parameters = new();

            ITemplateCreationResult result = await bootstrapper
                .CreateAsync(foundTemplates[0].Info, "CompanyProject", output, parameters, cancellationToken: TestContext.CancellationToken);

            IPostAction postAction = Assert.ContainsSingle(result.CreationResult!.PostActions);
            Assert.AreEqual("testfile.json", postAction.Args["jsonFileName"]);
            Assert.AreEqual("moduleConfiguration:edgeAgent:properties.desired:modules", postAction.Args["parentPropertyPath"]);
            Assert.AreEqual("CompanyProject", postAction.Args["newJsonPropertyName"]);
            Assert.AreEqual("${MODULEDIR<../CompanyProject>}", postAction.Args["newJsonPropertyValue"]);
            Assert.AreEqual("Add CompanyProject property to testfile.json manually.", postAction.ManualInstructions);
        }

        private Dictionary<string, string> ExpectedOutputWithConditions()
        {
            var expects = new Dictionary<string, string>
            {
                {
".dockerignore",
@"# comment bar
bar
baz
"
                },
                {
".editorconfig",
@"# comment bar
bar
baz
"
                },
                {
".gitattributes",
@"# comment bar
bar
baz
"
                },
                {
".gitignore",
@"# comment bar
bar
baz
"
                },
                {
"Dockerfile",
@"# comment bar
bar
baz
"
                },
                {
"nuget.config",
@"<!-- comment bar -->
bar
baz
"
                },
                {
"Package.appxmanifest",
@"<?xml version=""1.0"" encoding=""utf-8""?>

<Package
  xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10""
  xmlns:mp=""http://schemas.microsoft.com/appx/2014/phone/manifest""
  xmlns:uap=""http://schemas.microsoft.com/appx/manifest/uap/windows10"">

  <Identity Name=""Microsoft.UWPAppExample"" Publisher=""CN=Microsoft Corporation"" Version=""1.0.0.0"" ProcessorArchitecture=""x86"" />

  <Properties>
    <DisplayName>UWP App Example</DisplayName>
    <PublisherDisplayName>Microsoft Corporation</PublisherDisplayName>
    <Logo>Assets\StoreLogo-sdk.png</Logo>
  </Properties>

  <Resources>
    <Resource Language=""en-us""/>
    <!-- comment A is false -->
    <Resource Language=""zh-cn""/>
  </Resources>

  <Dependencies>
    <!-- comment B is true -->
    <TargetDeviceFamily Name=""Windows.Universal"" MinVersion=""10.0.10240.0"" MaxVersionTested=""10.0.22000.0"" />
  </Dependencies>

  <Applications>
    <Application Id=""App"" Executable=""UWPAppExample.exe"" EntryPoint=""UWPAppExample.App"">
  </Applications>

</Package>"
                },
                {
"test.axaml",
@"<!-- comment A is false -->
<Application xmlns=""https://github.com/something""
             xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
             x:Class=""App""
             RequestedThemeVariant=""Default"">

    <Application.Styles>
        <!-- comment B is true -->
        <FluentTheme Mode=""Light"" />
    </Application.Styles>
</Application>"
                },
                {
"test.cake",
@"// comment bar
bar
baz
"
                },
                {
"test.md",
@"<!-- comment bar -->
bar
baz
"
                },
                {
"test.ps1",
@"# comment B true
B true
common text
"
                },
                {
"test.sln",
@"# comment bar
bar
baz
"
                },
                {
"test.slnx",
@"<!-- comment bar -->
bar
baz
"
                },
                {
"test.yaml",
@"# comment bar
bar
baz
"
                }
            };
            return expects;
        }
    }
}
