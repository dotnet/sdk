// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using WindowsOnlyTheory = Microsoft.DotNet.Tools.Test.Utilities.WindowsOnlyTheoryAttribute;
using BuildCommand = Microsoft.DotNet.Tools.Test.Utilities.BuildCommand;
using PublishCommand = Microsoft.DotNet.Tools.Test.Utilities.PublishCommand;
using RestoreCommand = Microsoft.DotNet.Tools.Test.Utilities.RestoreCommand;
using CleanCommand = Microsoft.DotNet.Tools.Test.Utilities.CleanCommand;

namespace EndToEnd.Tests
{
    public class ProjectBuildTests : TestBase
    {
        [Fact]
        public void ItCanNewRestoreBuildRunCleanMSBuildProject()
        {
            var directory = TestAssets.CreateTestDirectory();
            string projectDirectory = directory.FullName;

            string newArgs = "console --no-restore";
            Microsoft.DotNet.Tools.Test.Utilities.CommandResultExtensions.Should(new NewCommandShim()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs)).Pass();

            string projectPath = Path.Combine(projectDirectory, directory.Name + ".csproj");

            var project = XDocument.Load(projectPath);
            var ns = project.Root.Name.Namespace;

            project.Root.Element(ns + "PropertyGroup")
                .Element(ns + "TargetFramework").Value = TestAssetInfo.currentTfm;
            project.Save(projectPath);

            Microsoft.DotNet.Tools.Test.Utilities.CommandResultExtensions.Should(new RestoreCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()).Pass();

            Microsoft.DotNet.Tools.Test.Utilities.CommandResultExtensions.Should(new BuildCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()).Pass();

            var runCommand = Microsoft.DotNet.Tools.Test.Utilities.CommandResultExtensions.Should(new RunCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput()).Pass().And.HaveStdOutContaining("Hello, World!");

            var binDirectory = Microsoft.DotNet.Tools.Test.Utilities.DirectoryInfoExtensions.Sub(new DirectoryInfo(projectDirectory), "bin");
            Microsoft.DotNet.Tools.Test.Utilities.DirectoryInfoExtensions.Should(binDirectory).HaveFilesMatching("*.dll", SearchOption.AllDirectories);

            Microsoft.DotNet.Tools.Test.Utilities.CommandResultExtensions.Should(new CleanCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()).Pass();

            Microsoft.DotNet.Tools.Test.Utilities.DirectoryInfoExtensions.Should(binDirectory).NotHaveFilesMatching("*.dll", SearchOption.AllDirectories);
        }

        [Fact]
        public void ItCanRunAnAppUsingTheWebSdk()
        {
            var directory = TestAssets.CreateTestDirectory();
            string projectDirectory = directory.FullName;

            string newArgs = "console --no-restore";
            Microsoft.DotNet.Tools.Test.Utilities.CommandResultExtensions.Should(new NewCommandShim()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs)).Pass();

            string projectPath = Path.Combine(projectDirectory, directory.Name + ".csproj");

            var project = XDocument.Load(projectPath);
            var ns = project.Root.Name.Namespace;

            project.Root.Attribute("Sdk").Value = "Microsoft.NET.Sdk.Web";
            project.Root.Element(ns + "PropertyGroup")
                .Element(ns + "TargetFramework").Value = TestAssetInfo.currentTfm;
            project.Save(projectPath);

            Microsoft.DotNet.Tools.Test.Utilities.CommandResultExtensions.Should(new BuildCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()).Pass();

            var runCommand = Microsoft.DotNet.Tools.Test.Utilities.CommandResultExtensions.Should(new RunCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput()).Pass().And.HaveStdOutContaining("Hello, World!");
        }

        [WindowsOnlyTheory]
        // [InlineData("net6.0", true)]
        // [InlineData("net6.0", false)]
        [InlineData("current", true)]
        [InlineData("current", false)]
        public void ItCanPublishArm64Winforms(string TargetFramework, bool selfContained)
        {
            DirectoryInfo directory = TestAssets.CreateTestDirectory();
            string projectDirectory = directory.FullName;
            string TargetFrameworkParameter = "";

            if (TargetFramework != "current")
            {
                TargetFrameworkParameter = $"-f {TargetFramework}";
            }
            string newArgs = $"winforms {TargetFrameworkParameter} --no-restore";
            Microsoft.DotNet.Tools.Test.Utilities.CommandResultExtensions.Should(new NewCommandShim()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs)).Pass();

            string selfContainedArgs = selfContained ? " --self-contained" : "";
            string publishArgs = "-r win-arm64" + selfContainedArgs;
            Microsoft.DotNet.Tools.Test.Utilities.CommandResultExtensions.Should(new PublishCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute(publishArgs)).Pass();

            var selfContainedPublishDir =
                Microsoft.DotNet.Tools.Test.Utilities.DirectoryInfoExtensions.Sub(
                    Microsoft.DotNet.Tools.Test.Utilities.DirectoryInfoExtensions.Sub(
                    Microsoft.DotNet.Tools.Test.Utilities.DirectoryInfoExtensions.Sub(
                    Microsoft.DotNet.Tools.Test.Utilities.DirectoryInfoExtensions.Sub(
                        new DirectoryInfo(projectDirectory), "bin"), TargetFramework != "current" ? "Debug" : "Release")
                    .GetDirectories().FirstOrDefault(), "win-arm64"), "publish");

            if (selfContained)
            {
                Microsoft.DotNet.Tools.Test.Utilities.DirectoryInfoExtensions.Should(selfContainedPublishDir).HaveFilesMatching("System.Windows.Forms.dll", SearchOption.TopDirectoryOnly);
            }
            Microsoft.DotNet.Tools.Test.Utilities.DirectoryInfoExtensions.Should(selfContainedPublishDir).HaveFilesMatching($"{directory.Name}.dll", SearchOption.TopDirectoryOnly);
        }

        [WindowsOnlyTheory]
        // [InlineData("net6.0", true)]
        // [InlineData("net6.0", false)]
        [InlineData("current", true)]
        [InlineData("current", false)]
        public void ItCanPublishArm64Wpf(string TargetFramework, bool selfContained)
        {
            DirectoryInfo directory = TestAssets.CreateTestDirectory();
            string projectDirectory = directory.FullName;
            string TargetFrameworkParameter = "";

            if (TargetFramework != "current")
            {
                TargetFrameworkParameter = $"-f {TargetFramework}";
            }

            string newArgs = $"wpf {TargetFrameworkParameter} --no-restore";
            Microsoft.DotNet.Tools.Test.Utilities.CommandResultExtensions.Should(new NewCommandShim()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs)).Pass();

            string selfContainedArgs = selfContained ? " --self-contained" : "";
            string publishArgs = "-r win-arm64" + selfContainedArgs;
            Microsoft.DotNet.Tools.Test.Utilities.CommandResultExtensions.Should(new PublishCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute(publishArgs)).Pass();

            var selfContainedPublishDir =
                Microsoft.DotNet.Tools.Test.Utilities.DirectoryInfoExtensions.Sub(
                    Microsoft.DotNet.Tools.Test.Utilities.DirectoryInfoExtensions.Sub(
                    Microsoft.DotNet.Tools.Test.Utilities.DirectoryInfoExtensions.Sub(
                    Microsoft.DotNet.Tools.Test.Utilities.DirectoryInfoExtensions.Sub(
                        new DirectoryInfo(projectDirectory), "bin"), TargetFramework != "current" ? "Debug" : "Release")
                    .GetDirectories().FirstOrDefault(), "win-arm64"), "publish");

            if (selfContained)
            {
                Microsoft.DotNet.Tools.Test.Utilities.DirectoryInfoExtensions.Should(selfContainedPublishDir).HaveFilesMatching("PresentationCore.dll", SearchOption.TopDirectoryOnly);
                Microsoft.DotNet.Tools.Test.Utilities.DirectoryInfoExtensions.Should(selfContainedPublishDir).HaveFilesMatching("PresentationNative_*.dll", SearchOption.TopDirectoryOnly);
            }
            Microsoft.DotNet.Tools.Test.Utilities.DirectoryInfoExtensions.Should(selfContainedPublishDir).HaveFilesMatching($"{directory.Name}.dll", SearchOption.TopDirectoryOnly);
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
            string locale = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;
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

            Microsoft.DotNet.Tools.Test.Utilities.CommandResultExtensions.Should(new NewCommandShim()
             .Execute()).Pass()
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
            DirectoryInfo directory = TestAssets.CreateTestDirectory(identifier: templateName);
            string projectDirectory = directory.FullName;

            string newArgs = $"{templateName}";

            Microsoft.DotNet.Tools.Test.Utilities.CommandResultExtensions.Should(new NewCommandShim()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs)).Pass();

            //check if the template created files
            Assert.True(directory.Exists);
            Assert.True(directory.EnumerateFileSystemInfos().Any());

            // delete test directory for some tests so we aren't leaving behind non-compliant nuget files
            if (templateName.Equals("nugetconfig"))
            {
                directory.Delete(true);
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

            DirectoryInfo directory = InstantiateProjectTemplate("classlib", language, withNoRestore: false);
            string projectDirectory = directory.FullName;
            string expectedItemName = $"TestItem_{templateName}";
            string newArgs = $"{templateName} --name {expectedItemName} --debug:ephemeral-hive";
            if (!string.IsNullOrWhiteSpace(language))
            {
                newArgs += $" --language {language}";
            }

            Microsoft.DotNet.Tools.Test.Utilities.CommandResultExtensions.Should(new NewCommandShim()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs)).Pass();

            //check if the template created files
            Assert.True(directory.Exists);
            Assert.True(directory.EnumerateFileSystemInfos().Any());
            Assert.True(directory.GetFile($"{expectedItemName}.{languageExtensionMap[language]}") != null);
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
            string dotnetFolder = Path.GetDirectoryName(RepoDirectoriesProvider.DotnetUnderTest);
            string[] runtimeFolders = Directory.GetDirectories(Path.Combine(dotnetFolder, "shared", "Microsoft.NETCore.App"));

            int latestMajorVersion = runtimeFolders.Select(folder => int.Parse(Path.GetFileName(folder).Split('.').First())).Max();
            if (latestMajorVersion == 9)
            {
                return $"net{latestMajorVersion}.0";
            }

            throw new Exception("Unsupported version of SDK");
        }

        private static void TestTemplateCreateAndBuild(string templateName, bool build = true, bool selfContained = false, string language = "", string framework = "", bool deleteTestDirectory = false)
        {
            DirectoryInfo directory = InstantiateProjectTemplate(templateName, language);
            string projectDirectory = directory.FullName;

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
                string buildArgs = selfContained ? $"-r {RuntimeInformation.RuntimeIdentifier} --self-contained" : "";
                if (!string.IsNullOrWhiteSpace(framework))
                {
                    buildArgs += $" --framework {framework}";
                }

                // Remove this (or formalize it) after https://github.com/dotnet/installer/issues/12479 is resolved.
                if (language == "F#")
                {
                    buildArgs += $" /p:_NETCoreSdkIsPreview=true";
                }

                string dotnetRoot = Path.GetDirectoryName(RepoDirectoriesProvider.DotnetUnderTest);
                Microsoft.DotNet.Tools.Test.Utilities.CommandResultExtensions.Should(new BuildCommand()
                     .WithEnvironmentVariable("PATH", dotnetRoot) // override PATH since razor rely on PATH to find dotnet
                     .WithWorkingDirectory(projectDirectory)
                     .Execute(buildArgs)).Pass();
            }

            // delete test directory for some tests so we aren't leaving behind non-compliant package files
            if (deleteTestDirectory)
            {
                directory.Delete(true);
            }
        }

        private static DirectoryInfo InstantiateProjectTemplate(string templateName, string language = "", bool withNoRestore = true)
        {
            DirectoryInfo directory = TestAssets.CreateTestDirectory(
                identifier: string.IsNullOrWhiteSpace(language)
                ? templateName
                : $"{templateName}[{language}]");
            string projectDirectory = directory.FullName;

            string newArgs = $"{templateName} --debug:ephemeral-hive {(withNoRestore ? "--no-restore" : "")}";
            if (!string.IsNullOrWhiteSpace(language))
            {
                newArgs += $" --language {language}";
            }

            Microsoft.DotNet.Tools.Test.Utilities.CommandResultExtensions.Should(new NewCommandShim()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs)).Pass();

            return directory;
        }
    }
}
