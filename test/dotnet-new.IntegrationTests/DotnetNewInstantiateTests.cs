// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public partial class DotnetNewInstantiateTests : BaseIntegrationTest
    {
        private ITestOutputHelper _log => Log;
        private static SharedHomeDirectory s_fixture = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext ctx)
        {
            s_fixture = new SharedHomeDirectory(new TestContextOutputHelper(ctx));
        }

        [ClassCleanup]
        public static void ClassCleanup() => s_fixture?.Dispose();

        private SharedHomeDirectory _fixture => s_fixture;

        [TestMethod]
        public void CanInstantiateTemplate()
        {
            string workingDirectory = CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"Console App\" was created successfully.");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [TestMethod]
        [Ignore("https://github.com/dotnet/sdk/issues/42539")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanInstantiateTemplate_WithAlias()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console", "--alias", "csharpconsole")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Successfully created alias named 'csharpconsole' with value 'console'");

            new DotnetNewCommand(_log, "console", "-n", "MyConsole", "-o", "no-alias")
             .WithCustomHive(home)
             .WithWorkingDirectory(workingDirectory)
             .Execute()
             .Should()
             .ExitWith(0)
             .And.NotHaveStdErr()
             .And.HaveStdOutContaining("The template \"Console App\" was created successfully.");

            new DotnetNewCommand(_log, "csharpconsole", "-n", "MyConsole", "-o", "alias")
               .WithCustomHive(home)
               .WithWorkingDirectory(workingDirectory)
               .Execute()
               .Should()
               .ExitWith(0)
               .And.NotHaveStdErr()
               .And.HaveStdOutContaining("The template \"Console App\" was created successfully.")
               .And.HaveStdOutContaining("After expanding aliases, the command is:")
               .And.HaveStdOutContaining("dotnet new console -n MyConsole -o alias");

            Assert.AreSequenceEqual(
                new DirectoryInfo(Path.Combine(workingDirectory, "no-alias")).EnumerateFileSystemInfos().Select(fi => fi.Name),
                new DirectoryInfo(Path.Combine(workingDirectory, "alias")).EnumerateFileSystemInfos().Select(fi => fi.Name));

        }

        [TestMethod]
        public void CanInstantiateTemplate_WithSingleNonDefaultLanguageChoice()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, home, workingDirectory);

            new DotnetNewCommand(_log, "basic")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"Basic FSharp\" was created successfully.");
        }

        [TestMethod]
        public void CanOverwriteFilesWithForce()
        {
            string workingDirectory = CreateTemporaryFolder();

            Utils.CommandResult commandResult = new DotnetNewCommand(_log, "console", "--no-restore")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"Console App\" was created successfully.");

            Utils.CommandResult forceCommandResult = new DotnetNewCommand(_log, "console", "--no-restore", "--force")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .WithRetryOnExitCode(100)
                .Execute();

            forceCommandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"Console App\" was created successfully.");

            Assert.AreEqual(commandResult.StdOut, forceCommandResult.StdOut);
        }

        [TestMethod]
        public void CanInstantiateTemplateWithSecondShortName()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallNuGetTemplate("Microsoft.DotNet.Web.ProjectTemplates.5.0", _log, home, workingDirectory);

            new DotnetNewCommand(_log, "webapp", "-o", "webapp")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"ASP.NET Core Web App (Razor Pages)\" was created successfully.");

            new DotnetNewCommand(_log, "razor", "-o", "razor")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"ASP.NET Core Web App (Razor Pages)\" was created successfully.");
        }

        [TestMethod]
        public void CanInstantiateTemplate_WithBinaryFile_FromFolder()
        {
            string workingDirectory = CreateTemporaryFolder();
            string home = CreateTemporaryFolder(folderName: "Home");
            string templateLocation = GetTestTemplateLocation("TemplateWithBinaryFile");

            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, "TestAssets.TemplateWithBinaryFile")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Pass();

            string sourceImage = Path.Combine(templateLocation, "image.png");
            string targetImage = Path.Combine(workingDirectory, "image.png");

            Assert.IsTrue(File.Exists(targetImage));

            Assert.AreEqual(
                new FileInfo(sourceImage).Length,
                new FileInfo(targetImage).Length);
            Assert.IsTrue(TestUtils.CompareFiles(sourceImage, targetImage), $"The content of {sourceImage} and {targetImage} is not same.");
        }

        [TestMethod]
        public void CanInstantiateTemplate_WithBinaryFile_FromPackage()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithBinaryFile");
            string workingDirectory = CreateTemporaryFolder();
            string home = CreateTemporaryFolder(folderName: "Home");

            string packageLocation = PackTestNuGetPackage(_log);
            InstallNuGetTemplate(packageLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, "TestAssets.TemplateWithBinaryFile")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Pass();

            string sourceImage = Path.Combine(templateLocation, "image.png");
            string targetImage = Path.Combine(workingDirectory, "image.png");

            Assert.IsTrue(File.Exists(targetImage));

            Assert.AreEqual(
                new FileInfo(sourceImage).Length,
                new FileInfo(targetImage).Length);
            Assert.IsTrue(TestUtils.CompareFiles(sourceImage, targetImage), $"The content of {sourceImage} and {targetImage} is not same.");
        }

        [TestMethod]
        public void CanInstantiateTemplate_WithParamsSharingPrefix()
        {
            string workingDirectory = CreateTemporaryFolder();
            string home = CreateTemporaryFolder(folderName: "Home");
            string templateLocation = GetTestTemplateLocation("TemplateWithParamsSharingPrefix");

            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            // not asserting on actual generated content - as there is none
            new DotnetNewCommand(_log, "TestAssets.TemplateWithParamsSharingPrefix")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();
        }

        [TestMethod]
        [DataRow(".dockerignore", "singleHash", false)]
        [DataRow(".editorconfig", "singleHash", false)]
        [DataRow(".gitattributes", "singleHash", false)]
        [DataRow(".gitignore", "singleHash", false)]
        [DataRow("Dockerfile", "singleHash", false)]
        [DataRow("nuget.config", "xml", false)]
        [DataRow("cake", "cSharpNoComments")]
        [DataRow("sln", "singleHash")]
        [DataRow("yaml", "singleHash")]
        [DataRow("md", "xml")]
        public void CanInstantiateTemplate_WithConditions_BasedOnFileName(string testCase, string conditionType, bool useAsExtension = true)
        {
            string expectedCommandFormat = conditionType switch
            {
                "singleHash" => "# comment {0}",
                "xml" => "<!-- comment {0} -->",
                "cSharpNoComments" => "// comment {0}",
                _ => throw new NotSupportedException($"conditionType {conditionType} is not supported")
            };

            string fileName = useAsExtension ? $"test.{testCase}" : testCase;

            //sln always has CRLF line ending, as per .gitattributes settings
            string expectedEol = testCase == "sln" ? "\r\n" : Environment.NewLine;

            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();

            //The template has the following conditions defined in various file types: non actionable on parameter A and actionable on parameter B
            //#if (A)
            //# comment foo
            //foo
            //#endif
            //##if (B)
            //## comment bar
            //#bar
            //#endif
            //baz
            //For extension test cases the template has 'test.<extension>' file defined.

            InstallTestTemplate("TemplateWithConditions", _log, home, workingDirectory);
            new DotnetNewCommand(_log, "TestAssets.TemplateWithConditions", "--A", "true")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"TemplateWithConditions\" was created successfully.");

            string testFile = Path.Combine(workingDirectory, fileName);
            Assert.IsTrue(File.Exists(testFile));
            Assert.AreEqual($"{string.Format(expectedCommandFormat, "foo")}{expectedEol}foo{expectedEol}baz{expectedEol}", File.ReadAllText(testFile));

            workingDirectory = CreateTemporaryFolder();
            new DotnetNewCommand(_log, "TestAssets.TemplateWithConditions", "--A", "false")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"TemplateWithConditions\" was created successfully.");

            testFile = Path.Combine(workingDirectory, fileName);
            Assert.IsTrue(File.Exists(testFile));
            Assert.AreEqual($"baz{expectedEol}", File.ReadAllText(testFile));

            workingDirectory = CreateTemporaryFolder();
            new DotnetNewCommand(_log, "TestAssets.TemplateWithConditions", "--B", "true")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"TemplateWithConditions\" was created successfully.");

            testFile = Path.Combine(workingDirectory, fileName);
            Assert.IsTrue(File.Exists(testFile));
            Assert.AreEqual($"{string.Format(expectedCommandFormat, "bar")}{expectedEol}bar{expectedEol}baz{expectedEol}", File.ReadAllText(testFile));
        }

        [TestMethod]
        [DataRow("", "theDefaultName.cs")]
        [DataRow("newName", "newName.cs")]
        public void CanInstantiateTemplate_WithDefaultName(string name, string expectedFileName)
        {
            string workingDirectory = CreateTemporaryFolder();
            string home = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("TemplateWithPreferDefaultName", _log, home, workingDirectory);

            workingDirectory = CreateTemporaryFolder();
            new DotnetNewCommand(_log, "TestAssets.TemplateWithPreferDefaultName", "-n", name)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"TemplateWithPreferDefaultName\" was created successfully.");

            string testFile = Path.Combine(workingDirectory, expectedFileName);
            Assert.IsTrue(File.Exists(testFile));
        }

        [TestMethod]
        public void DoesNotReportErrorOnDefaultUpdateCheckOfLocalPackageDuringInstantiation()
        {
            string nugetName = "TestNupkgInstallTemplate";
            string nugetVersion = "0.0.1";
            string nugetFullName = $"{nugetName}::{nugetVersion}";
            string nugetFileName = $"{nugetName}.{nugetVersion}.nupkg";
            string templateName = "nupkginstall";
            string workingDirectory = CreateTemporaryFolder();
            string home = CreateTemporaryFolder(folderName: "Home");

            InstallNuGetTemplate(
                Path.Combine(DotnetNewTestPackagesBasePath, nugetFileName),
                _log,
                home,
                workingDirectory);

            new DotnetNewCommand(_log, templateName, "--dry-run")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("File actions would have been taken:");
        }

        [TestMethod]
        public void WhenSwitchIsSkippedThenItPrintsError()
        {
            Utils.CommandResult cmd = new DotnetNewCommand(Log)
                .WithVirtualHive()
                .Execute("Web1.1");

            cmd.ExitCode.Should().NotBe(0);

            if (!SdkTestContext.IsLocalized())
            {
                cmd.StdErr.Should().StartWith("No templates or subcommands found");
            }
        }

        [TestMethod]
        public void ItCanCreateTemplate()
        {
            string tempDir = CreateTemporaryFolder();
            Utils.CommandResult cmd = new DotnetNewCommand(Log)
                .WithVirtualHive()
                .Execute("console", "-o", tempDir);
            cmd.Should().Pass();
        }

        [TestMethod]
        public void ItCanShowHelp()
        {
            Utils.CommandResult cmd = new DotnetNewCommand(Log)
                .WithVirtualHive()
                .Execute("--help");
            cmd.Should().Pass()
                .And.HaveStdOutContaining("Usage:")
                .And.HaveStdOutContaining("dotnet new [command] [options]");
        }

        [TestMethod]
        public void ItCanShowHelpForTemplate()
        {
            Utils.CommandResult cmd = new DotnetNewCommand(Log)
                .WithVirtualHive()
                .Execute("classlib", "--help");

            cmd.Should().Pass()
                .And.NotHaveStdOutContaining("Usage: new [options]")
                .And.HaveStdOutContaining("Class Library (C#)")
                .And.HaveStdOutContaining("--framework");
        }

        [TestMethod]
        [DataRow("-lang", "F#", "--use-program-main")]
        [DataRow("--language", "F#", "--use-program-main")]
        [DataRow("-lang", "C#", "--no-exist")]
        public void ExampleHasLanguageForSepecifiedLanguageWithInvalidOption(string languageOption, string language, string invalidOption)
        {
            CommandResult cmd = new DotnetNewCommand(Log, "console", languageOption, language, invalidOption)
                .WithVirtualHive()
                .Execute();
            cmd.Should().Fail()
                .And.HaveStdErrContaining($"'{invalidOption}' is not a valid option")
                .And.HaveStdErrContaining("For more information, run:")
                .And.HaveStdErrContaining($"dotnet new console --language {language} -h");
        }

        [TestMethod]
        public void ItCanShowParseError()
        {
            Utils.CommandResult cmd = new DotnetNewCommand(Log)
                .WithVirtualHive()
                .Execute("update", "--bla");
            cmd.Should().ExitWith(127)
                .And.HaveStdErrContaining("Unrecognized command or argument '--bla'")
                .And.HaveStdOutContaining("dotnet new update [options]");
        }

        [TestMethod]
        public void WhenTemplateNameIsNotUniquelyMatchedThenItIndicatesProblemToUser()
        {
            Utils.CommandResult cmd = new DotnetNewCommand(Log)
                .WithVirtualHive()
                .Execute("c");

            cmd.ExitCode.Should().NotBe(0);

            if (!SdkTestContext.IsLocalized())
            {
                cmd.StdErr.Should().StartWith("No templates or subcommands found matching: 'c'.");
            }
        }

        [TestMethod]
        public void When_dotnet_new_is_invoked_multiple_times_it_should_fail()
        {
            string rootPath = CreateTemporaryFolder();

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .Execute($"console", "--no-restore");

            DateTime expectedState = Directory.GetLastWriteTime(rootPath);

            CommandResult result = new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .Execute($"console", "--no-restore");

            DateTime actualState = Directory.GetLastWriteTime(rootPath);

            Assert.AreEqual(expectedState, actualState);

            result.Should().Fail();
        }

        [TestMethod]
        public void When_dotnet_new_is_invoked_with_preferred_lang_env_var_set()
        {
            string rootPath = CreateTemporaryFolder();

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .WithEnvironmentVariable("DOTNET_NEW_PREFERRED_LANG", "F#")
                .Execute($"console", "--no-restore", "-n", "f1")
                .Should().Pass();

            string expectedFsprojPath = Path.Combine(rootPath, "f1", "f1.fsproj");
            Assert.IsTrue(File.Exists(expectedFsprojPath), $"expected '{expectedFsprojPath}' but was not found");
        }

        [TestMethod]
        public void When_dotnet_new_is_invoked_default_is_csharp()
        {
            string rootPath = CreateTemporaryFolder();

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .Execute($"console", "--no-restore", "-n", "c1")
                .Should().Pass();

            string expectedCsprojPath = Path.Combine(rootPath, "c1", "c1.csproj");
            Assert.IsTrue(File.Exists(expectedCsprojPath), $"expected '{expectedCsprojPath}' but was not found");
        }

        [TestMethod]
        public void Dotnet_new_can_be_invoked_with_lang_option()
        {
            string rootPath = CreateTemporaryFolder();

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .Execute($"console", "--no-restore", "-n", "vb1", "-lang", "vb")
                .Should().Pass();

            string expectedCsprojPath = Path.Combine(rootPath, "vb1", "vb1.vbproj");
            Assert.IsTrue(File.Exists(expectedCsprojPath), $"expected '{expectedCsprojPath}' but was not found");
        }

        [TestMethod]
        public void When_dotnet_new_is_invoked_with_preferred_lang_env_var_empty()
        {
            string rootPath = CreateTemporaryFolder();

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .WithEnvironmentVariable("DOTNET_NEW_PREFERRED_LANG", "")
                .Execute($"console", "--no-restore", "-n", "c1")
                .Should().Pass();

            string expectedCsprojPath = Path.Combine(rootPath, "c1", "c1.csproj");
            Assert.IsTrue(File.Exists(expectedCsprojPath), $"expected '{expectedCsprojPath}' but was not found");
        }
    }
}
