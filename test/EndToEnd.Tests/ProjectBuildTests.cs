// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using EndToEnd.Tests.Utilities;

namespace EndToEnd.Tests
{
    public class ProjectBuildTests(ITestOutputHelper log) : SdkTest(log)
    {
        [Fact]
        public void ItCanNewRestoreBuildRunCleanMSBuildProject()
        {
            var directory = _testAssetsManager.CreateTestDirectory();
            string projectDirectory = directory.Path;

            new DotnetNewCommand(Log, "console", "--no-restore")
                .WithVirtualHive()
                .WithWorkingDirectory(projectDirectory)
                .Execute().Should().Pass();

            string projectPath = Path.Combine(projectDirectory, new DirectoryInfo(directory.Path).Name + ".csproj");

            var project = XDocument.Load(projectPath);
            var ns = project.Root.Name.Namespace;

            project.Root.Element(ns + "PropertyGroup")
                .Element(ns + "TargetFramework").Value = ToolsetInfo.CurrentTargetFramework;
            project.Save(projectPath);

            new RestoreCommand(Log, projectPath)
                .WithWorkingDirectory(projectDirectory)
                .Execute().Should().Pass();

            new BuildCommand(Log, projectPath)
                .WithWorkingDirectory(projectDirectory)
                .Execute().Should().Pass();

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(projectDirectory)
                .Execute().Should().Pass().And.HaveStdOutContaining("Hello, World!");

            var binDirectory = new DirectoryInfo(projectDirectory).Sub("bin");
            binDirectory.Should().HaveFilesMatching("*.dll", SearchOption.AllDirectories);

            new CleanCommand(Log, projectPath)
                .WithWorkingDirectory(projectDirectory)
                .Execute().Should().Pass();

            binDirectory.Should().NotHaveFilesMatching("*.dll", SearchOption.AllDirectories);
        }

        [Fact]
        public void ItCanRunAnAppUsingTheWebSdk()
        {
            var directory = _testAssetsManager.CreateTestDirectory();
            string projectDirectory = directory.Path;

            new DotnetNewCommand(Log, "console", "--no-restore")
                .WithVirtualHive()
                .WithWorkingDirectory(projectDirectory)
                .Execute().Should().Pass();

            string projectPath = Path.Combine(projectDirectory, new DirectoryInfo(directory.Path).Name + ".csproj");

            var project = XDocument.Load(projectPath);
            var ns = project.Root.Name.Namespace;

            project.Root.Attribute("Sdk").Value = "Microsoft.NET.Sdk.Web";
            project.Root.Element(ns + "PropertyGroup")
                .Element(ns + "TargetFramework").Value = ToolsetInfo.CurrentTargetFramework;
            project.Save(projectPath);

            new BuildCommand(Log, projectPath)
                .WithWorkingDirectory(projectDirectory)
                .Execute().Should().Pass();

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(projectDirectory)
                .Execute().Should().Pass().And.HaveStdOutContaining("Hello, World!");
        }

        [WindowsOnlyTheory]
        [InlineData("current", true)]
        [InlineData("current", false)]
        public void ItCanPublishArm64Winforms(string targetFramework, bool selfContained)
        {
            var directory = _testAssetsManager.CreateTestDirectory();
            string projectDirectory = directory.Path;

            string[] newArgs = [
                "winforms",
                "--no-restore",
                .. targetFramework != "current" ? ["-f", targetFramework] : Array.Empty<string>()
            ];
            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs).Should().Pass();

            string[] publishArgs = [
                "-r",
                "win-arm64",
                .. selfContained ? ["--self-contained"] : Array.Empty<string>()
            ];
            new DotnetPublishCommand(Log, publishArgs)
                .WithWorkingDirectory(projectDirectory)
                .Execute().Should().Pass();

            var selfContainedPublishDir = new DirectoryInfo(projectDirectory)
                .Sub("bin").Sub(targetFramework != "current" ? "Debug" : "Release")
                .GetDirectories().FirstOrDefault().Sub("win-arm64").Sub("publish");

            if (selfContained)
            {
                selfContainedPublishDir.Should().HaveFilesMatching("System.Windows.Forms.dll", SearchOption.TopDirectoryOnly);
            }
            selfContainedPublishDir.Should().HaveFilesMatching($"{new DirectoryInfo(directory.Path).Name}.dll", SearchOption.TopDirectoryOnly);
        }

        [WindowsOnlyTheory]
        [InlineData("current", true)]
        [InlineData("current", false)]
        public void ItCanPublishArm64Wpf(string targetFramework, bool selfContained)
        {
            var directory = _testAssetsManager.CreateTestDirectory();
            string projectDirectory = directory.Path;

            string[] newArgs = [
                "wpf",
                "--no-restore",
                .. targetFramework != "current" ? ["-f", targetFramework] : Array.Empty<string>()
            ];
            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs).Should().Pass();

            string[] publishArgs = [
                "-r",
                "win-arm64",
                .. selfContained ? ["--self-contained"] : Array.Empty<string>()
            ];
            new DotnetPublishCommand(Log, publishArgs)
                .WithWorkingDirectory(projectDirectory)
                .Execute().Should().Pass();

            var selfContainedPublishDir = new DirectoryInfo(projectDirectory)
                .Sub("bin").Sub(targetFramework != "current" ? "Debug" : "Release")
                .GetDirectories().FirstOrDefault().Sub("win-arm64").Sub("publish");

            if (selfContained)
            {
                selfContainedPublishDir.Should().HaveFilesMatching("PresentationCore.dll", SearchOption.TopDirectoryOnly);
                selfContainedPublishDir.Should().HaveFilesMatching("PresentationNative_*.dll", SearchOption.TopDirectoryOnly);
            }
            selfContainedPublishDir.Should().HaveFilesMatching($"{new DirectoryInfo(directory.Path).Name}.dll", SearchOption.TopDirectoryOnly);
        }

        [Theory]
        // microsoft.dotnet.common.projectemplates templates
        [InlineData("console")]
        [InlineData("console", "C#")]
        [InlineData("console", "VB")]
        [InlineData("console", "F#")]
        [InlineData("classlib")]
        [InlineData("classlib", "C#")]
        [InlineData("classlib", "VB")]
        [InlineData("classlib", "F#")]
        [InlineData("mstest")]
        [InlineData("nunit")]
        [InlineData("web")]
        [InlineData("mvc")]
        public void ItCanBuildTemplates(string templateName, string language = "") => TestTemplateCreateAndBuild(templateName, language: language);

        /// <summary>
        /// The test checks if dotnet new shows curated list correctly after the SDK installation and template insertion.
        /// </summary>
        [Fact]
        public void DotnetNewShowsCuratedListCorrectly()
        {
            string locale = Thread.CurrentThread.CurrentUICulture.Name;
            if (!string.IsNullOrWhiteSpace(locale)
                && !locale.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[{nameof(DotnetNewShowsCuratedListCorrectly)}] CurrentUICulture: {locale}");
                Console.WriteLine($"[{nameof(DotnetNewShowsCuratedListCorrectly)}] Test is skipped as it supports only 'en' or invariant culture.");
                return;
            }

            string expectedOutput =
@"[\-\s]+
[\w \.\(\)]+blazor\s+\[C#\][\w\ \/]+
[\w \.\(\)]+classlib\s+\[C#\],F#,VB[\w\ \/]+
[\w \.\(\)]+console\s+\[C#\],F#,VB[\w\ \/]+
[\w \.\(\)]+mstest\s+\[C#\],F#,VB[\w\ \/]+
";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                expectedOutput +=
@"[\w \.\(\)]+winforms\s+\[C#\],VB[\w\ \/]+
[\w \.\(\)]+\wpf\s+\[C#\],VB[\w\ \/]+
";
            }
            //list should end with new line
            expectedOutput += Environment.NewLine;

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .Execute().Should().Pass()
                .And.HaveStdOutMatching(expectedOutput);
        }

        [Theory]
        // microsoft.dotnet.common.itemtemplates templates
        [InlineData("globaljson")]
        [InlineData("nugetconfig")]
        [InlineData("webconfig")]
        [InlineData("gitignore")]
        [InlineData("tool-manifest")]
        [InlineData("sln")]
        public void ItCanCreateItemTemplate(string templateName)
        {
            var directory = _testAssetsManager.CreateTestDirectory(identifier: templateName);
            string projectDirectory = directory.Path;

            string newArgs = $"{templateName}";

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs).Should().Pass();

            //check if the template created files
            var directoryInfo = new DirectoryInfo(directory.Path);
            Assert.True(directoryInfo.Exists);
            Assert.True(directoryInfo.EnumerateFileSystemInfos().Any());

            // delete test directory for some tests so we aren't leaving behind non-compliant nuget files
            if (templateName.Equals("nugetconfig"))
            {
                directoryInfo.Delete(true);
            }
        }

        [Theory]
        // microsoft.dotnet.common.itemtemplates templates
        [InlineData("class")]
        [InlineData("struct")]
        [InlineData("enum")]
        [InlineData("record")]
        [InlineData("interface")]
        [InlineData("class", "C#")]
        [InlineData("class", "VB")]
        [InlineData("struct", "VB")]
        [InlineData("enum", "VB")]
        [InlineData("interface", "VB")]
        public void ItCanCreateItemTemplateWithProjectRestriction(string templateName, string language = "")
        {
            var languageExtensionMap = new Dictionary<string, string>()
            {
                { "", ".cs" },
                { "C#", ".cs" },
                { "VB", ".vb" }
            };

            var directory = InstantiateProjectTemplate("classlib", language, withNoRestore: false, itemName: templateName);
            string projectDirectory = directory.Path;
            string expectedItemName = $"TestItem_{templateName}";

            string[] newArgs = [
                templateName,
                "--name",
                expectedItemName,
                .. !string.IsNullOrWhiteSpace(language) ? ["--language", language] : Array.Empty<string>()
            ];
            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs).Should().Pass();

            //check if the template created files
            var directoryInfo = new DirectoryInfo(directory.Path);
            Assert.True(directoryInfo.Exists);
            Assert.True(directoryInfo.EnumerateFileSystemInfos().Any());
            Assert.True(directoryInfo.File($"{expectedItemName}.{languageExtensionMap[language]}") != null);
        }

        [WindowsOnlyTheory]
        [InlineData("wpf")]
        [InlineData("winforms")]
        public void ItCanBuildDesktopTemplates(string templateName) => TestTemplateCreateAndBuild(templateName);

        [WindowsOnlyTheory]
        [InlineData("wpf")]
        public void ItCanBuildDesktopTemplatesSelfContained(string templateName) => TestTemplateCreateAndBuild(templateName, selfContained: true);

        [Theory]
        [InlineData("web")]
        [InlineData("console")]
        public void ItCanBuildTemplatesSelfContained(string templateName) => TestTemplateCreateAndBuild(templateName, selfContained: true);

        /// <summary>
        /// The test checks if the template creates the template for correct framework by default.
        /// For .NET 6 the templates should create the projects targeting net6.0
        /// </summary>
        [Theory]
        [InlineData("console")]
        [InlineData("console", "C#")]
        [InlineData("console", "VB")]
        [InlineData("console", "F#")]
        [InlineData("classlib")]
        [InlineData("classlib", "C#")]
        [InlineData("classlib", "VB")]
        [InlineData("classlib", "F#")]
        [InlineData("worker")]
        [InlineData("worker", "C#")]
        [InlineData("worker", "F#")]
        [InlineData("mstest")]
        [InlineData("mstest", "C#")]
        [InlineData("mstest", "VB")]
        [InlineData("mstest", "F#")]
        [InlineData("nunit")]
        [InlineData("nunit", "C#")]
        [InlineData("nunit", "VB")]
        [InlineData("nunit", "F#")]
        [InlineData("xunit")]
        [InlineData("xunit", "C#")]
        [InlineData("xunit", "VB")]
        [InlineData("xunit", "F#")]
        [InlineData("blazorwasm")]
        [InlineData("web")]
        [InlineData("web", "C#")]
        [InlineData("web", "F#")]
        [InlineData("mvc")]
        [InlineData("mvc", "C#")]
        [InlineData("mvc", "F#")]
        [InlineData("webapi")]
        [InlineData("webapi", "C#")]
        [InlineData("webapi", "F#")]
        [InlineData("webapp")]
        [InlineData("razorclasslib")]
        public void ItCanCreateAndBuildTemplatesWithDefaultFramework(string templateName, string language = "")
        {
            string framework = DetectExpectedDefaultFramework(templateName);
            TestTemplateCreateAndBuild(templateName, selfContained: false, language: language, framework: framework);
        }

        /// <summary>
        /// [Windows only tests]
        /// The test checks if the template creates the template for correct framework by default.
        /// For .NET 6 the templates should create the projects targeting net6.0.
        /// </summary>
        [WindowsOnlyTheory]
        [InlineData("wpf")]
        [InlineData("wpf", "C#")]
        [InlineData("wpf", "VB")]
        [InlineData("wpflib")]
        [InlineData("wpflib", "C#")]
        [InlineData("wpflib", "VB")]
        [InlineData("wpfcustomcontrollib")]
        [InlineData("wpfcustomcontrollib", "C#")]
        [InlineData("wpfcustomcontrollib", "VB")]
        [InlineData("wpfusercontrollib")]
        [InlineData("wpfusercontrollib", "C#")]
        [InlineData("wpfusercontrollib", "VB")]
        [InlineData("winforms")]
        [InlineData("winforms", "C#")]
        [InlineData("winforms", "VB")]
        [InlineData("winformslib")]
        [InlineData("winformslib", "C#")]
        [InlineData("winformslib", "VB")]
        [InlineData("winformscontrollib")]
        [InlineData("winformscontrollib", "C#")]
        [InlineData("winformscontrollib", "VB")]
        public void ItCanCreateAndBuildTemplatesWithDefaultFramework_Windows(string templateName, string language = "")
        {
            string framework = DetectExpectedDefaultFramework(templateName);
            TestTemplateCreateAndBuild(templateName, selfContained: false, language: language, framework: $"{framework}-windows");
        }

        /// <summary>
        /// [project is not built on linux-musl]
        /// The test checks if the template creates the template for correct framework by default.
        /// For .NET 6 the templates should create the projects targeting net6.0.
        /// </summary>
        [Theory]
        [InlineData("grpc")]
        public void ItCanCreateAndBuildTemplatesWithDefaultFramework_DisableBuildOnLinuxMusl(string templateName)
        {
            string framework = DetectExpectedDefaultFramework(templateName);

            if (RuntimeInformation.RuntimeIdentifier.StartsWith("linux-musl"))
            {
                TestTemplateCreateAndBuild(templateName, build: false, framework: framework);
            }
            else
            {
                TestTemplateCreateAndBuild(templateName, selfContained: true, framework: framework);
            }
        }

        private static string DetectExpectedDefaultFramework(string template = "")
        {
            string dotnetFolder = Path.GetDirectoryName(TestContext.Current.ToolsetUnderTest.DotNetHostPath);
            string[] runtimeFolders = Directory.GetDirectories(Path.Combine(dotnetFolder, "shared", "Microsoft.NETCore.App"));
            int latestMajorVersion = runtimeFolders.Select(folder => int.Parse(Path.GetFileName(folder).Split('.').First())).Max();
            if (latestMajorVersion == 10)
            {
                // TODO: This block need to be updated when every template updates their default tfm.
                if (template.StartsWith("wpf"))
                {
                    return $"net9.0";
                }
                return $"net{latestMajorVersion}.0";
            }

            throw new Exception("Unsupported version of SDK");
        }

        private void TestTemplateCreateAndBuild(string templateName, bool build = true, bool selfContained = false, string language = "", string framework = "", bool deleteTestDirectory = false)
        {
            var directory = InstantiateProjectTemplate(templateName, language);
            string projectDirectory = directory.Path;

            if (!string.IsNullOrWhiteSpace(framework))
            {
                //check if MSBuild TargetFramework property for *proj is set to expected framework
                string expectedExtension = language switch
                {
                    "C#" => "*.csproj",
                    "F#" => "*.fsproj",
                    "VB" => "*.vbproj",
                    _ => "*.csproj"
                };
                string projectFile = Directory.GetFiles(projectDirectory, expectedExtension).Single();
                XDocument projectXml = XDocument.Load(projectFile);
                XNamespace ns = projectXml.Root.Name.Namespace;
                Assert.Equal(framework, projectXml.Root.Element(ns + "PropertyGroup").Element(ns + "TargetFramework").Value);
            }

            if (build)
            {
                string[] buildArgs = [
                    .. selfContained ? ["-r", RuntimeInformation.RuntimeIdentifier] : Array.Empty<string>(),
                    .. !string.IsNullOrWhiteSpace(framework) ? ["--framework", framework] : Array.Empty<string>(),
                    // Remove this (or formalize it) after https://github.com/dotnet/installer/issues/12479 is resolved.
                    .. language == "F#" ? ["/p:_NETCoreSdkIsPreview=true"] : Array.Empty<string>()
                ];

                string dotnetRoot = Path.GetDirectoryName(TestContext.Current.ToolsetUnderTest.DotNetHostPath);
                new DotnetBuildCommand(Log, projectDirectory)
                     .WithEnvironmentVariable("PATH", dotnetRoot) // override PATH since razor rely on PATH to find dotnet
                     .WithWorkingDirectory(projectDirectory)
                     .Execute(buildArgs).Should().Pass();
            }

            // delete test directory for some tests so we aren't leaving behind non-compliant package files
            if (deleteTestDirectory)
            {
                new DirectoryInfo(directory.Path).Delete(true);
            }
        }

        private TestDirectory InstantiateProjectTemplate(string templateName, string language = "", bool withNoRestore = true, string itemName = "")
        {
            var identifier = templateName;
            if (!string.IsNullOrWhiteSpace(language))
            {
                identifier += $"[{language}]";
            }
            if (!string.IsNullOrWhiteSpace(itemName))
            {
                identifier += $"({itemName})";
            }
            var directory = _testAssetsManager.CreateTestDirectory(identifier: identifier);
            string projectDirectory = directory.Path;

            string[] newArgs = [
                templateName,
                .. withNoRestore ? ["--no-restore"] : Array.Empty<string>(),
                // Remove this (or formalize it) after https://github.com/dotnet/installer/issues/12479 is resolved.
                .. !string.IsNullOrWhiteSpace(language) ? ["--language", language] : Array.Empty<string>()
            ];
            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs).Should().Pass();

            return directory;
        }
    }
}
