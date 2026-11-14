// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public partial class DotnetNewListTests : BaseIntegrationTest
    {
        private ITestOutputHelper _log => Log;
        private static SharedHomeDirectory s_sharedHome = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext ctx)
        {
            s_sharedHome = new SharedHomeDirectory(new TestContextOutputHelper(ctx));
            s_sharedHome.InstallPackage("Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0");
        }

        [ClassCleanup]
        public static void ClassCleanup() => s_sharedHome?.Dispose();

        private SharedHomeDirectory _sharedHome => s_sharedHome;

        [TestMethod]
        [DataRow("--list c")]
        [DataRow("-l c")]
        [DataRow("list c")]
        [DataRow("c --list")]
        public void BasicTest_WithNameCriteria(string command)
        {
            new DotnetNewCommand(_log, command.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"These templates matched your input: 'c'")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.NotHaveStdOutMatching("dotnet gitignore file\\s+gitignore\\s+Config")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library");
        }

        [TestMethod]
        [DataRow("list c --format json")]
        [DataRow("--list c --format json")]
        public void CanListTemplatesInJsonFormat(string command)
        {
            CommandResult result = new DotnetNewCommand(_log, command.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            result
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("These templates matched your input");

            using JsonDocument document = JsonDocument.Parse(result.StdOut!);
            Assert.AreEqual(1, document.RootElement.GetProperty("version").GetInt32());

            JsonElement data = document.RootElement.GetProperty("data");
            Assert.AreEqual(JsonValueKind.Array, data.ValueKind);

            JsonElement consoleTemplate = data.EnumerateArray().Single(e => e.GetProperty("name").GetString() == "Console App");
            Assert.Contains("console", consoleTemplate.GetProperty("shortNames").EnumerateArray().Select(e => e.GetString()));
            Assert.Contains("project", consoleTemplate.GetProperty("types").EnumerateArray().Select(e => e.GetString()));
            Assert.Contains("Microsoft", consoleTemplate.GetProperty("authors").EnumerateArray().Select(e => e.GetString()));
            Assert.Contains("Common", consoleTemplate.GetProperty("classifications").EnumerateArray().Select(e => e.GetString()));
            Assert.Contains("Console", consoleTemplate.GetProperty("classifications").EnumerateArray().Select(e => e.GetString()));

            JsonElement csharpLang = consoleTemplate.GetProperty("languages").EnumerateArray().Single(e => e.GetProperty("id").GetString() == "C#");
            Assert.IsTrue(csharpLang.GetProperty("default").GetBoolean());
        }

        [TestMethod]
        [DataRow("list --format json")]
        [DataRow("list TemplateWithConflictShortName --format json")]
        public void JsonFormatDoesNotIncludeShortNameConflictHint(string command)
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("TemplateWithConflictShortName", _log, customHivePath);

            CommandResult result = new DotnetNewCommand(_log, command.Split(" "))
                .WithCustomHive(customHivePath)
                .WithoutBuiltInTemplates()
                .Execute();

            result
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("To instantiate the template with short name 'list'");

            using JsonDocument document = JsonDocument.Parse(result.StdOut!);
            JsonElement template = document.RootElement.GetProperty("data").EnumerateArray().Single();
            Assert.AreEqual("TemplateWithConflictShortName", template.GetProperty("name").GetString());
            Assert.Contains("list", template.GetProperty("shortNames").EnumerateArray().Select(e => e.GetString()));
        }

        [TestMethod]
        [DataRow("list --format json")]
        [DataRow("--list --format json")]
        public void JsonFormatMarksFallbackLanguageAsDefault(string command)
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, customHivePath);

            CommandResult result = new DotnetNewCommand(_log, command.Split(" "))
                .WithCustomHive(customHivePath)
                .WithoutBuiltInTemplates()
                .Execute();

            result
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            using JsonDocument document = JsonDocument.Parse(result.StdOut!);
            JsonElement template = document.RootElement.GetProperty("data").EnumerateArray().Single();
            Assert.AreEqual("Basic FSharp", template.GetProperty("name").GetString());

            JsonElement fsharpLang = template.GetProperty("languages").EnumerateArray().Single();
            Assert.AreEqual("F#", fsharpLang.GetProperty("id").GetString());
            Assert.IsTrue(fsharpLang.GetProperty("default").GetBoolean());
        }

        [TestMethod]
        [DataRow("list --format json")]
        [DataRow("--list --format json")]
        public void JsonFormatDoesNotMarkNonPreferredLanguageAsDefaultWhenNeutralVariantExists(string command)
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            string templateRoot = CreateTemporaryFolder(folderName: "Templates");
            string neutralConfigDir = Path.Combine(templateRoot, "NeutralVariant", ".template.config");
            string fsharpConfigDir = Path.Combine(templateRoot, "FSharpVariant", ".template.config");
            Directory.CreateDirectory(neutralConfigDir);
            Directory.CreateDirectory(fsharpConfigDir);

            File.WriteAllText(
                Path.Combine(neutralConfigDir, "template.json"),
                """
                {
                  "$schema": "http://json.schemastore.org/template",
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "MixedNeutralAndFSharp",
                  "tags": { "type": "item" },
                  "identity": "TestAssets.MixedNeutralAndFSharp.Neutral",
                  "groupIdentity": "TestAssets.MixedNeutralAndFSharp",
                  "shortName": "review-mixed-language"
                }
                """);
            File.WriteAllText(
                Path.Combine(fsharpConfigDir, "template.json"),
                """
                {
                  "$schema": "http://json.schemastore.org/template",
                  "author": "Test Asset",
                  "classifications": [ "Test Asset" ],
                  "name": "MixedNeutralAndFSharp",
                  "tags": { "language": "F#", "type": "item" },
                  "identity": "TestAssets.MixedNeutralAndFSharp.FSharp",
                  "groupIdentity": "TestAssets.MixedNeutralAndFSharp",
                  "shortName": "review-mixed-language"
                }
                """);

            InstallTestTemplate(templateRoot, _log, customHivePath);

            CommandResult result = new DotnetNewCommand(_log, command.Split(" "))
                .WithCustomHive(customHivePath)
                .WithoutBuiltInTemplates()
                .Execute();

            result
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            using JsonDocument document = JsonDocument.Parse(result.StdOut!);
            JsonElement template = document.RootElement.GetProperty("data").EnumerateArray().Single();
            Assert.AreEqual("MixedNeutralAndFSharp", template.GetProperty("name").GetString());

            JsonElement fsharpLang = template.GetProperty("languages").EnumerateArray().Single();
            Assert.AreEqual("F#", fsharpLang.GetProperty("id").GetString());
            Assert.IsFalse(fsharpLang.GetProperty("default").GetBoolean());
        }

        [TestMethod]
        [DataRow("list --format json")]
        [DataRow("--list --format json")]
        [DataRow("list --format json --diagnostics")]
        [DataRow("-d new list --format json")]
        [DataRow("--diagnostics new list --format json")]
        [DataRow("-d new --list --format json")]
        [DataRow("--diagnostics new --list --format json")]
        public void JsonFormatRoutesEngineWarningsAndDiagnosticsToStdErr(string command)
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("Invalid/InvalidHostData", _log, customHivePath);

            string[] tokens = command.Split(" ");
            DotnetNewCommand dotnetNewCommand;
            if (tokens[0] is "-d" or "--diagnostics" && tokens[1] == "new")
            {
                dotnetNewCommand = new DotnetNewCommand(_log, tokens[2..]);
                dotnetNewCommand.Arguments.Insert(0, tokens[0]);
            }
            else
            {
                dotnetNewCommand = new DotnetNewCommand(_log, tokens);
            }

            CommandResult result = dotnetNewCommand
                .WithCustomHive(customHivePath)
                .WithoutBuiltInTemplates()
                .WithEnvironmentVariable("DOTNET_CLI_HOME", customHivePath)
                .Execute();

            result
                .Should()
                .ExitWith(0)
                .And.HaveStdErrContaining("Warning:");

            using JsonDocument document = JsonDocument.Parse(result.StdOut!);
            JsonElement template = document.RootElement.GetProperty("data").EnumerateArray().Single();
            Assert.AreEqual("TestAssets.Invalid.InvalidHostData", template.GetProperty("name").GetString());
        }

        [TestMethod]
        [DataRow("--list --columns-all")]
        [DataRow("--columns-all --list")]
        [DataRow("list --columns-all")]
        public void CanShowAllColumns(string command)
        {
            new DotnetNewCommand(_log, command.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Type\\s+Author\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+project\\s+Microsoft\\s+Common/Console");
        }

        [TestMethod]
        [DataRow("--list --tag Common")]
        [DataRow("-l --tag Common")]
        [DataRow("list --tag Common")]
        [DataRow("--tag Common --list")]
        public void CanFilterTags(string command)
        {
            new DotnetNewCommand(_log, command.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"These templates matched your input: --tag='Common'")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.NotHaveStdOutMatching("dotnet gitignore file\\s+gitignore\\s+Config")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library");
        }

        [TestMethod]
        [DataRow("app --list --tag Common")]
        [DataRow("app -l --tag Common")]
        [DataRow("--list app --tag Common")]
        [DataRow("list app --tag Common")]
        public void CanFilterTags_WithNameCriteria(string command)
        {
            new DotnetNewCommand(_log, command.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"These templates matched your input: 'app', --tag='Common'")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.NotHaveStdOutMatching("dotnet gitignore file\\s+gitignore\\s+Config")
                .And.NotHaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library");
        }

        [TestMethod]
        public void CanShowMultipleShortNames()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();

            new DotnetNewCommand(_log, "--install", "Microsoft.DotNet.Web.ProjectTemplates.5.0")
                  .WithCustomHive(home)
                  .WithWorkingDirectory(workingDirectory)
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr()
                  .And.HaveStdOutMatching("ASP\\.NET Core Web App\\s+webapp,razor\\s+\\[C#\\]\\s+Web/MVC/Razor Pages");

            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(home)
                .WithoutBuiltInTemplates()
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("ASP\\.NET Core Web App\\s+webapp,razor\\s+\\[C#\\]\\s+Web/MVC/Razor Pages");

            new DotnetNewCommand(_log, "webapp", "--list")
                .WithCustomHive(home)
                .WithoutBuiltInTemplates()
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: 'webapp'")
                .And.HaveStdOutMatching("ASP\\.NET Core Web App\\s+webapp,razor\\s+\\[C#\\]\\s+Web/MVC/Razor Pages");

            new DotnetNewCommand(_log, "razor", "--list")
                .WithCustomHive(home)
                .WithoutBuiltInTemplates()
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: 'razor'")
                .And.HaveStdOutMatching("ASP\\.NET Core Web App\\s+webapp,razor\\s+\\[C#\\]\\s+Web/MVC/Razor Pages");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [TestMethod]
        [Ignore("https://github.com/dotnet/sdk/issues/42541")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanFilterByChoiceParameter()
        {
            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list", "--framework")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: 'c', --framework")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list", "-f")
              .WithCustomHive(_sharedHome.HomeDirectory)
              .Execute()
              .Should()
              .ExitWith(0)
              .And.NotHaveStdErr()
              .And.HaveStdOutContaining("These templates matched your input: 'c', -f")
              .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
              .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
              .And.NotHaveStdOutMatching("dotnet gitignore file\\s+gitignore\\s+Config")
              .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
              .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "--framework")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: --framework")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "-f")
              .WithCustomHive(_sharedHome.HomeDirectory)
              .Execute()
              .Should()
              .ExitWith(0)
              .And.NotHaveStdErr()
              .And.HaveStdOutContaining("These templates matched your input: -f")
              .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
              .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
              .And.NotHaveStdOutMatching("dotnet gitignore file\\s+gitignore\\s+Config")
              .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
              .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [TestMethod]
        [Ignore("https://github.com/dotnet/sdk/issues/42541")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanFilterByNonChoiceParameter()
        {
            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list", "--langVersion")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: 'c', --langVersion")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "--langVersion")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: --langVersion")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [TestMethod]
        [Ignore("https://github.com/dotnet/sdk/issues/42541")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void IgnoresValueForNonChoiceParameter()
        {
            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list", "--no-restore", "invalid")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: 'c', --no-restore")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "--no-restore", "invalid")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: --no-restore")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [TestMethod]
        [Ignore("https://github.com/dotnet/sdk/issues/42541")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanFilterByChoiceParameterWithValue()
        {
            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list", "--framework", "net5.0")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: 'c', --framework='net5.0'")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list", "-f", "net5.0")
              .WithCustomHive(_sharedHome.HomeDirectory)
              .Execute()
              .Should()
              .ExitWith(0)
              .And.NotHaveStdErr()
              .And.HaveStdOutContaining("These templates matched your input: 'c', -f")
              .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
              .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
              .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
              .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "--framework", "net5.0")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: --framework")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "-f", "net5.0")
              .WithCustomHive(_sharedHome.HomeDirectory)
              .Execute()
              .Should()
              .ExitWith(0)
              .And.NotHaveStdErr()
              .And.HaveStdOutContaining("These templates matched your input: -f")
              .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
              .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
              .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
              .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [TestMethod]
        [Ignore("https://github.com/dotnet/sdk/issues/42541")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CannotListTemplatesWithUnknownParameter()
        {
            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "--unknown")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("No templates found matching: --unknown.")
                .And.HaveStdErrContaining("9 template(s) partially matched, but failed on --unknown.")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new <TEMPLATE_NAME> --search");

            new DotnetNewCommand(_log, "c", "--list", "--unknown")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("No templates found matching: 'c', --unknown.")
                .And.HaveStdErrContaining("6 template(s) partially matched, but failed on --unknown.")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new c --search");

            new DotnetNewCommand(_log, "c", "--list", "--unknown", "--language", "C#")
              .WithCustomHive(_sharedHome.HomeDirectory)
              .Execute()
              .Should().Fail()
              .And.HaveStdErrContaining("No templates found matching: 'c', language='C#', --unknown.")
              .And.HaveStdErrContaining("6 template(s) partially matched, but failed on language='C#', --unknown.")
              .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new c --search");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [TestMethod]
        [Ignore("https://github.com/dotnet/sdk/issues/42541")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CannotListTemplatesWithUnknownValueForChoiceParameter()
        {
            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "--framework", "unknown")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("No templates found matching: --framework='unknown'.")
                .And.HaveStdErrContaining("9 template(s) partially matched, but failed on --framework='unknown'.")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new <TEMPLATE_NAME> --search");

            new DotnetNewCommand(_log, "c", "--list", "--framework", "unknown")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("No templates found matching: 'c', --framework='unknown'.")
                .And.HaveStdErrContaining("6 template(s) partially matched, but failed on --framework='unknown'.")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new c --search");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [TestMethod]
        [Ignore("https://github.com/dotnet/sdk/issues/42541")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CannotListTemplatesForInvalidFilters()
        {
            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "--language", "unknown", "--framework", "unknown")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("No templates found matching: language='unknown'.")
                .And.HaveStdErrContaining("9 template(s) partially matched, but failed on language='unknown'.")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new <TEMPLATE_NAME> --search");

            new DotnetNewCommand(_log, "c", "--list", "--language", "unknown", "--framework", "unknown")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("No templates found matching: 'c', language='unknown'.")
                .And.HaveStdErrContaining("6 template(s) partially matched, but failed on language='unknown'.")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new c --search");
        }

        [TestMethod]
        public void TemplateGroupingTest()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDir = CreateTemporaryFolder();
            InstallTestTemplate("TemplateGrouping", _log, home, workingDir);

            new DotnetNewCommand(_log, "--list", "--columns-all")
                .WithCustomHive(home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Basic FSharp +template-grouping +\\[C#],F# +item +Author1 +Test Asset +\\r?\\n +Q# +item,project +Author2 +Test Asset");
        }

        [TestMethod]
        [DataRow("author", "Author", "Microsoft")]
        [DataRow("type", "Type", "")]
        [DataRow("tags", "Tags", "Solution")]
        [DataRow("language", "Language", "")]
        public void TemplateWithSpecifiedColumnOutput(string columnName, string columnHeader, string columnValue)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();

            new DotnetNewCommand(_log, "--install", "Microsoft.DotNet.Web.ProjectTemplates.5.0")
                  .WithCustomHive(home)
                  .WithWorkingDirectory(workingDirectory)
                  .Execute()
                  .Should()
                  .ExitWith(0);

            new DotnetNewCommand(_log, "list", "--columns", columnName)
                .WithCustomHive(home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching($"Template Name\\s+Short Name\\s+{columnHeader}")
                .And.HaveStdOutMatching($"Solution File\\s+sln,solution\\s+{columnValue}");
        }

        [TestMethod]
        [DataRow("c --list", "--list c")]
        [DataRow("c --list --language F#", "--list c --language F#")]
        [DataRow("c --list --columns-all", "--list c --columns-all")]
        public void CanFallbackToListOption(string command1, string command2)
        {
            CommandResult commandResult1 = new DotnetNewCommand(_log, command1.Split())
             .WithCustomHive(_sharedHome.HomeDirectory)
             .Execute();

            CommandResult commandResult2 = new DotnetNewCommand(_log, command2.Split())
               .WithCustomHive(_sharedHome.HomeDirectory)
               .Execute();

            Assert.AreEqual(commandResult1.StdOut, commandResult2.StdOut);
        }

        [TestMethod]
        [DataRow("--list foo --columns-all bar", "bar", "foo")]
        [DataRow("list foo --columns-all bar", "bar", "foo")]
        [DataRow("-l foo --columns-all bar", "bar", "foo")]
        [DataRow("--list foo bar", "bar", "foo")]
        [DataRow("list foo bar", "bar", "foo")]
        [DataRow("foo --list bar", "foo", "bar")]
        [DataRow("foo list bar", "foo", "bar")]
        [DataRow("foo --list bar --language F#", "foo", "bar")]
        [DataRow("foo --list --columns-all bar", "foo", "bar")]
        [DataRow("foo --list --columns-all --framework net6.0 bar", "bar|net6.0|foo", "--framework")]
        [DataRow("foo --list --columns-all -other-param --framework net6.0 bar", "bar|--framework|net6.0|foo", "-other-param")]
        public void CannotShowListOnParseError(string command, string invalidArguments, string validArguments)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, command.Split())
             .WithCustomHive(_sharedHome.HomeDirectory)
             .Execute();

            commandResult.Should().Fail();
            foreach (string arg in invalidArguments.Split('|'))
            {
                commandResult.Should().HaveStdErrMatching($"Unrecognized command or (argument\\(s\\)\\:|argument) '{arg}'");
            }

            foreach (string arg in validArguments.Split('|'))
            {
                commandResult.Should()
                    .NotHaveStdErrContaining($"Unrecognized command or argument '{arg}'")
                    .And.NotHaveStdErrContaining($"Unrecognized command or argument(s): '{arg}'");
            }
        }
    }
}
