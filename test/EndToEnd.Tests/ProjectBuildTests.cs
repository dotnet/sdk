// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using EndToEnd.Tests.Utilities;

namespace EndToEnd.Tests
{
    [TestClass]
    public class ProjectBuildTests : SdkTest
    {
        [TestMethod]
        public void ItCanNewRestoreBuildRunCleanMSBuildProject()
        {
            var directory = TestAssetsManager.CreateTestDirectory();
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

        [TestMethod]
        public void ItCanRunAnAppUsingTheWebSdk()
        {
            var directory = TestAssetsManager.CreateTestDirectory();
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

        [TestMethod]
        [DataRow("current", true)]
        [DataRow("current", false)]
        public void ItCanPublishArm64Winforms(string targetFramework, bool selfContained)
        {
            var directory = TestAssetsManager.CreateTestDirectory();
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
                .. selfContained ? ["--self-contained"] : Array.Empty<string>(),
                .. RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Array.Empty<string>() : ["/p:EnableWindowsTargeting=true"],
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

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        [DataRow("current", true)]
        [DataRow("current", false)]
        public void ItCanPublishArm64Wpf(string targetFramework, bool selfContained)
        {
            var directory = TestAssetsManager.CreateTestDirectory();
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

        [TestMethod]
        // microsoft.dotnet.common.projectemplates templates
        [DataRow("console")]
        [DataRow("console", "C#")]
        [DataRow("console", "VB")]
        [DataRow("console", "F#")]
        [DataRow("classlib")]
        [DataRow("classlib", "C#")]
        [DataRow("classlib", "VB")]
        [DataRow("classlib", "F#")]
        [DataRow("mstest")]
        [DataRow("nunit")]
        [DataRow("web")]
        [DataRow("mvc")]
        public void ItCanBuildTemplates(string templateName, string language = "") => TestTemplateCreateAndBuild(templateName, language: language);

        /// <summary>
        /// The test checks if dotnet new shows curated list correctly after the SDK installation and template insertion.
        /// </summary>
        [TestMethod]
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

            expectedOutput +=
@"[\w \.\(\)]+winforms\s+\[C#\],VB[\w\ \/]+
[\w \.\(\)]+\wpf\s+\[C#\],VB[\w\ \/]+
";

            //list should end with new line
            expectedOutput += Environment.NewLine;

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .Execute().Should().Pass()
                .And.HaveStdOutMatching(expectedOutput);
        }

        [TestMethod]
        // microsoft.dotnet.common.itemtemplates templates
        [DataRow("globaljson")]
        [DataRow("nugetconfig")]
        [DataRow("webconfig")]
        [DataRow("gitignore")]
        [DataRow("tool-manifest")]
        [DataRow("sln")]
        public void ItCanCreateItemTemplate(string templateName)
        {
            var directory = TestAssetsManager.CreateTestDirectory(identifier: templateName);
            string projectDirectory = directory.Path;

            string newArgs = $"{templateName}";

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs).Should().Pass();

            //check if the template created files
            var directoryInfo = new DirectoryInfo(directory.Path);
            Assert.IsTrue(directoryInfo.Exists);
            Assert.IsNotEmpty(directoryInfo.EnumerateFileSystemInfos());

            // delete test directory for some tests so we aren't leaving behind non-compliant nuget files
            if (templateName.Equals("nugetconfig"))
            {
                directoryInfo.Delete(true);
            }
        }

        [TestMethod]
        // microsoft.dotnet.common.itemtemplates templates
        [DataRow("class")]
        [DataRow("struct")]
        [DataRow("enum")]
        [DataRow("record")]
        [DataRow("interface")]
        [DataRow("class", "C#")]
        [DataRow("class", "VB")]
        [DataRow("struct", "VB")]
        [DataRow("enum", "VB")]
        [DataRow("interface", "VB")]
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
            Assert.IsTrue(directoryInfo.Exists);
            Assert.IsNotEmpty(directoryInfo.EnumerateFileSystemInfos());
            Assert.IsNotNull(directoryInfo.File($"{expectedItemName}.{languageExtensionMap[language]}"));
        }

        [TestMethod]
        [DataRow("wpf")]
        [DataRow("winforms")]
        public void ItCanBuildDesktopTemplates(string templateName) => TestTemplateCreateAndBuild(templateName);

        [TestMethod]
        [DataRow("wpf")]
        public void ItCanBuildDesktopTemplatesSelfContained(string templateName) => TestTemplateCreateAndBuild(templateName, selfContained: true);

        [TestMethod]
        [DataRow("web")]
        [DataRow("console")]
        public void ItCanBuildTemplatesSelfContained(string templateName) => TestTemplateCreateAndBuild(templateName, selfContained: true);

        /// <summary>
        /// The test checks if the template creates the template for correct framework by default.
        /// For .NET 6 the templates should create the projects targeting net6.0
        /// </summary>
        [TestMethod]
        [DataRow("console")]
        [DataRow("console", "C#")]
        [DataRow("console", "VB")]
        [DataRow("console", "F#")]
        [DataRow("classlib")]
        [DataRow("classlib", "C#")]
        [DataRow("classlib", "VB")]
        [DataRow("classlib", "F#")]
        [DataRow("worker")]
        [DataRow("worker", "C#")]
        [DataRow("worker", "F#")]
        [DataRow("mstest")]
        [DataRow("mstest", "C#")]
        [DataRow("mstest", "VB")]
        [DataRow("mstest", "F#")]
        [DataRow("nunit")]
        [DataRow("nunit", "C#")]
        [DataRow("nunit", "VB")]
        [DataRow("nunit", "F#")]
        [DataRow("xunit")]
        [DataRow("xunit", "C#")]
        [DataRow("xunit", "VB")]
        [DataRow("xunit", "F#")]
        // Skip = "https://github.com/dotnet/sdk/issues/53791"
        //[DataRow("blazorwasm")]
        [DataRow("web")]
        [DataRow("web", "C#")]
        [DataRow("web", "F#")]
        [DataRow("mvc")]
        [DataRow("mvc", "C#")]
        [DataRow("mvc", "F#")]
        [DataRow("webapi")]
        [DataRow("webapi", "C#")]
        [DataRow("webapi", "F#")]
        [DataRow("webapp")]
        [DataRow("razorclasslib")]
        public void ItCanCreateAndBuildTemplatesWithDefaultFramework(string templateName, string language = "")
        {
            string framework = DetectExpectedDefaultFramework(templateName);
            TestTemplateCreateAndBuild(templateName, selfContained: false, language: language, framework: framework);
        }

        /// <summary>
        /// The test checks if the template creates the template for correct framework by default.
        /// For .NET 6 the templates should create the projects targeting net6.0.
        /// </summary>
        [TestMethod]
        [DataRow("wpf")]
        [DataRow("wpf", "C#")]
        [DataRow("wpf", "VB")]
        [DataRow("wpflib")]
        [DataRow("wpflib", "C#")]
        [DataRow("wpflib", "VB")]
        [DataRow("wpfcustomcontrollib")]
        [DataRow("wpfcustomcontrollib", "C#")]
        [DataRow("wpfcustomcontrollib", "VB")]
        [DataRow("wpfusercontrollib")]
        [DataRow("wpfusercontrollib", "C#")]
        [DataRow("wpfusercontrollib", "VB")]
        [DataRow("winforms")]
        [DataRow("winforms", "C#")]
        [DataRow("winforms", "VB")]
        [DataRow("winformslib")]
        [DataRow("winformslib", "C#")]
        [DataRow("winformslib", "VB")]
        [DataRow("winformscontrollib")]
        [DataRow("winformscontrollib", "C#")]
        [DataRow("winformscontrollib", "VB")]
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
        [TestMethod]
        [DataRow("grpc")]
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
            string dotnetFolder = Path.GetDirectoryName(SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath);
            string[] runtimeFolders = Directory.GetDirectories(Path.Combine(dotnetFolder, "shared", "Microsoft.NETCore.App"));
            int latestMajorVersion = runtimeFolders.Select(folder => int.Parse(Path.GetFileName(folder).Split('.').First())).Max();
            if (latestMajorVersion == 11)
            {
                return $"net{latestMajorVersion}.0";
            }

            throw new Exception("Unsupported version of SDK");
        }

        private void TestTemplateCreateAndBuild(string templateName, bool build = true, bool selfContained = false, string language = "", string framework = "", bool deleteTestDirectory = false)
        {
            var directory = InstantiateProjectTemplate(templateName, language);
            string projectDirectory = directory.Path;

            XDocument GetProjectXml()
            {
                string expectedExtension = language switch
                {
                    "C#" => "*.csproj",
                    "F#" => "*.fsproj",
                    "VB" => "*.vbproj",
                    _ => "*.csproj"
                };
                string projectFile = Directory.GetFiles(projectDirectory, expectedExtension).Single();
                XDocument projectXml = XDocument.Load(projectFile);
                return projectXml;
            }

            if (!string.IsNullOrWhiteSpace(framework))
            {
                //check if MSBuild TargetFramework property for *proj is set to expected framework
                var projectXml = GetProjectXml();
                XNamespace ns = projectXml.Root.Name.Namespace;
                Assert.AreEqual(framework, projectXml.Root.Element(ns + "PropertyGroup").Element(ns + "TargetFramework").Value);
            }

            bool needsEnableWindowsTargeting = false;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string effectiveFramework = framework;
                if (string.IsNullOrEmpty(effectiveFramework))
                {
                    var projectXml = GetProjectXml();
                    XNamespace ns = projectXml.Root.Name.Namespace;
                    effectiveFramework = projectXml.Root.Element(ns + "PropertyGroup").Element(ns + "TargetFramework").Value;
                }

                if (effectiveFramework.Contains("windows"))
                {
                    needsEnableWindowsTargeting = true;
                }
            }

            if (build)
            {
                string[] buildArgs = [
                    .. selfContained ? ["-r", RuntimeInformation.RuntimeIdentifier] : Array.Empty<string>(),
                    .. !string.IsNullOrWhiteSpace(framework) ? ["--framework", framework] : Array.Empty<string>(),
                    // Remove this (or formalize it) after https://github.com/dotnet/installer/issues/12479 is resolved.
                    .. language == "F#" ? ["/p:_NETCoreSdkIsPreview=true"] : Array.Empty<string>(),
                    .. needsEnableWindowsTargeting ? ["/p:EnableWindowsTargeting=true"] : Array.Empty<string>(),
                    $"/bl:{templateName}-{(selfContained ? "selfcontained" : "fdd")}-{language}-{framework}-{{}}.binlog"
                ];

                string dotnetRoot = Path.GetDirectoryName(SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath);
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
            var directory = TestAssetsManager.CreateTestDirectory(identifier: identifier);
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
