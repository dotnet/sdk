// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using EmptyFiles;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [TestClass]
    public class TemplateEngineSamplesTest : BaseIntegrationTest
    {
        private ILogger? _loggerInstance;
        private ILogger _log => _loggerInstance ??= new TestLoggerFactory(Log).CreateLogger(nameof(TemplateEngineSamplesTest));
        private static SharedHomeDirectory s_sharedHome = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext ctx)
        {
            s_sharedHome = new SharedHomeDirectory(new TestContextOutputHelper(ctx));
            s_sharedHome.InstallPackage("Microsoft.TemplateEngine.Samples");
        }

        [ClassCleanup]
        public static void ClassCleanup() => s_sharedHome?.Dispose();

        private SharedHomeDirectory _sharedHome => s_sharedHome;

        [TestMethod]
        [DataRow("01-basic-template", "sample01", null, "no args")]
        [DataRow("02-add-parameters", "sample02", new[] { "--copyrightName", "Test Copyright", "--title", "Test Title" }, "text args")]
        [DataRow("03-optional-page", "sample03", new[] { "--EnableContactPage", "true" }, "optional content included")]
        [DataRow("03-optional-page", "sample03", null, "optional content excluded")]
        [DataRow("04-parameter-from-list", "sample04", new[] { "--BackgroundColor", "dimgray" }, "the choice parameter")]
        [DataRow("05-multi-project", "sample05", new[] { "--includetest", "true" }, "the optional test project included")]
        [DataRow("05-multi-project", "sample05", new[] { "--includetest", "false" }, "the optional test project excluded")]
        [DataRow("06-console-csharp-fsharp", "sample06", null, "multiple languages supported. This one creates a template according to the default option - C#")]
        [DataRow("06-console-csharp-fsharp", "sample06", new[] { "--language", "F#" }, "multiple languages supported. This one creates F# language template")]
        [DataRow("07-param-with-custom-short-name", "sample07", null, "customised parameter name")]
        [DataRow("09-replace-onlyif-after", "sample09", new[] { "--backgroundColor", "grey" }, "replacing with onlyif condition")]
        [DataRow("10-symbol-from-date", "sample10", null, "usage of date generator")]
        [DataRow("11-change-string-casing", "sample11", null, "usage of casing generator")]
        [DataRow("13-constant-value", "sample13", null, "replacing of constant value")]
        [DataRow("15-computed-symbol", "sample15", null, "usage computed symbols")]
        [DataRow("16-string-value-transform", "sample16", null, "usage of derived parameter")]
        public async Task TemplateEngineSamplesProjectTest(
            string folderName,
            string shortName,
            string[]? arguments,
            string caseDescription)
        {
            _log.LogInformation($"Template with {caseDescription}");
            Dictionary<string, string?> environmentUnderTest = new() { ["DOTNET_NOLOGO"] = false.ToString() };
            SdkTestContext.Current.AddTestEnvironmentVariables(environmentUnderTest);
            FileExtensions.AddTextExtension(".cshtml");

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: shortName)
            {
                TemplateSpecificArgs = arguments ?? Enumerable.Empty<string>(),
                VerifyCommandOutput = true,
                SnapshotsDirectory = ApprovalsDirectory,
                SettingsDirectory = _sharedHome.HomeDirectory,
                DoNotAppendTemplateArgsToScenarioName = true,
                DotnetExecutablePath = SdkTestContext.Current.ToolsetUnderTest?.DotNetHostPath,
                DoNotPrependCallerMethodNameToScenarioName = true,
                ScenarioName = $"{folderName.Substring(folderName.IndexOf("-") + 1)}{GetScenarioName(arguments)}"
            }
            .WithCustomEnvironment(environmentUnderTest!)
            .WithCustomScrubbers(
               ScrubbersDefinition.Empty
               .AddScrubber(sb => sb.Replace(DateTime.Now.ToString("MM/dd/yyyy"), "**/**/****"))
               .AddScrubber(sb => sb.ScrubMSBuildDebugLogMessage(), "txt"));

            VerificationEngine engine = new(_log);
            await engine.Execute(options, TestContext.CancellationToken);
        }

        private string GetScenarioName(string[]? args)
        {
            StringBuilder sb = new();

            if (args != null)
            {
                sb.Append('.');

                for (int index = 0; index < args.Length; index += 2)
                {
                    sb.Append($"{args[index].Replace("--", "")}={args[index + 1]}.");
                }
            }

            return sb.ToString(0, Math.Max(0, sb.Length - 1));
        }
    }
}
