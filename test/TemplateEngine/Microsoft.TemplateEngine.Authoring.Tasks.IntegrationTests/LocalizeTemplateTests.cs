// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.NET.TestFramework;
using Microsoft.TemplateEngine.CommandUtils;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;

namespace Microsoft.TemplateEngine.Authoring.Tasks.IntegrationTests
{
    [TestClass]
    public class LocalizeTemplateTests : TestBase
    {
        public TestContext TestContext { get; set; } = null!;

        private ILogger Log => new TestContextLogger(TestContext);

        [TestMethod]
        public async Task CanRunTask()
        {
            string tmpDir = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy("Resources/BasicTemplatePackage", tmpDir, true);
            SetupNuGetConfigForPackagesLocation(tmpDir);

            CreateDotnetCommand("add", "TemplatePackage.csproj", "package", "Microsoft.TemplateEngine.Authoring.Tasks", "--prerelease")
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            CreateDotnetCommand("build")
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            string locFolder = Path.Combine(tmpDir, "content/TemplateWithSourceName/.template.config/localize");

            Assert.IsTrue(Directory.Exists(locFolder));
            Assert.HasCount(14, Directory.GetFiles(locFolder));
            Assert.IsTrue(File.Exists(Path.Combine(locFolder, "templatestrings.de.json")));

            await DeleteTemporaryDirectoryAsync(tmpDir);
        }

        [TestMethod]
        public async Task CanRunTaskSelectedLangs()
        {
            string tmpDir = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy("Resources/TemplatePackageEnDe", tmpDir, true);
            SetupNuGetConfigForPackagesLocation(tmpDir);

            CreateDotnetCommand("add", "TemplatePackage.csproj", "package", "Microsoft.TemplateEngine.Authoring.Tasks", "--prerelease")
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            CreateDotnetCommand("build")
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            string locFolder = Path.Combine(tmpDir, "content/TemplateWithSourceName/.template.config/localize");

            Assert.IsTrue(Directory.Exists(locFolder));
            Assert.HasCount(2, Directory.GetFiles(locFolder));
            Assert.IsTrue(File.Exists(Path.Combine(locFolder, "templatestrings.de.json")));
            Assert.IsFalse(File.Exists(Path.Combine(locFolder, "templatestrings.fr.json")));

            await DeleteTemporaryDirectoryAsync(tmpDir);
        }

        [TestMethod]
        public async Task CanRunTaskSelectedTemplates()
        {
            string tmpDir = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy("Resources/TemplatePackagePartiallyLocalized", tmpDir, true);
            SetupNuGetConfigForPackagesLocation(tmpDir);

            CreateDotnetCommand("add", "TemplatePackage.csproj", "package", "Microsoft.TemplateEngine.Authoring.Tasks", "--prerelease")
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            CreateDotnetCommand("build")
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            string locFolder = Path.Combine(tmpDir, "content/localized/.template.config/localize");
            string noLocFolder = Path.Combine(tmpDir, "content/non-localized/.template.config/localize");

            Assert.IsTrue(Directory.Exists(locFolder));
            Assert.HasCount(14, Directory.GetFiles(locFolder));
            Assert.IsTrue(File.Exists(Path.Combine(locFolder, "templatestrings.de.json")));
            Assert.IsFalse(Directory.Exists(noLocFolder));

            await DeleteTemporaryDirectoryAsync(tmpDir);
        }

        [TestMethod]
        public async Task CanRunTaskAndDetectError()
        {
            string tmpDir = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy("Resources/InvalidTemplatePackage", tmpDir, true);
            SetupNuGetConfigForPackagesLocation(tmpDir);

            CreateDotnetCommand("add", "TemplatePackage.csproj", "package", "Microsoft.TemplateEngine.Authoring.Tasks", "--prerelease")
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            CreateDotnetCommand("build")
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("Build FAILED.")
                .And.HaveStdOutContaining("Each child of '//postActions' should have a unique id");

            string locFolder = Path.Combine(tmpDir, "content/TemplateWithSourceName/.template.config/localize");

            Assert.IsFalse(Directory.Exists(locFolder));
            await DeleteTemporaryDirectoryAsync(tmpDir);
        }

        private DotnetCommand CreateDotnetCommand(string subcommand, params string[] args) =>
            new DotnetCommand(Log, subcommand, args)
                .WithCustomExecutablePath(SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath)
                .WithoutTelemetry();

        private static Task DeleteTemporaryDirectoryAsync(string path) =>
            TestUtils.AttemptSearch<bool, IOException>(10, TimeSpan.FromMilliseconds(500), () =>
            {
                Directory.Delete(path, true);
                return Task.FromResult(true);
            });
    }
}
