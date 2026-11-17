// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
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
        public void CanRunTask()
        {
            string tmpDir = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy("Resources/BasicTemplatePackage", tmpDir, true);
            SetupNuGetConfigForPackagesLocation(tmpDir);

            new DotnetCommand(Log, "add", "TemplatePackage.csproj", "package", "Microsoft.TemplateEngine.Authoring.Tasks", "--prerelease")
                .WithoutTelemetry()
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand(Log, "build")
                .WithoutTelemetry()
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            string locFolder = Path.Combine(tmpDir, "content/TemplateWithSourceName/.template.config/localize");

            Assert.IsTrue(Directory.Exists(locFolder));
            Assert.HasCount(14, Directory.GetFiles(locFolder));
            Assert.IsTrue(File.Exists(Path.Combine(locFolder, "templatestrings.de.json")));

            Directory.Delete(tmpDir, true);
        }

        [TestMethod]
        public void CanRunTaskSelectedLangs()
        {
            string tmpDir = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy("Resources/TemplatePackageEnDe", tmpDir, true);
            SetupNuGetConfigForPackagesLocation(tmpDir);

            new DotnetCommand(Log, "add", "TemplatePackage.csproj", "package", "Microsoft.TemplateEngine.Authoring.Tasks", "--prerelease")
                .WithoutTelemetry()
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand(Log, "build")
                .WithoutTelemetry()
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            string locFolder = Path.Combine(tmpDir, "content/TemplateWithSourceName/.template.config/localize");

            Assert.IsTrue(Directory.Exists(locFolder));
            Assert.HasCount(2, Directory.GetFiles(locFolder));
            Assert.IsTrue(File.Exists(Path.Combine(locFolder, "templatestrings.de.json")));
            Assert.IsFalse(File.Exists(Path.Combine(locFolder, "templatestrings.fr.json")));

            Directory.Delete(tmpDir, true);
        }

        [TestMethod]
        public void CanRunTaskSelectedTemplates()
        {
            string tmpDir = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy("Resources/TemplatePackagePartiallyLocalized", tmpDir, true);
            SetupNuGetConfigForPackagesLocation(tmpDir);

            new DotnetCommand(Log, "add", "TemplatePackage.csproj", "package", "Microsoft.TemplateEngine.Authoring.Tasks", "--prerelease")
                .WithoutTelemetry()
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand(Log, "build")
                .WithoutTelemetry()
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

            Directory.Delete(tmpDir, true);
        }

        [TestMethod]
        public void CanRunTaskAndDetectError()
        {
            string tmpDir = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy("Resources/InvalidTemplatePackage", tmpDir, true);
            SetupNuGetConfigForPackagesLocation(tmpDir);

            new DotnetCommand(Log, "add", "TemplatePackage.csproj", "package", "Microsoft.TemplateEngine.Authoring.Tasks", "--prerelease")
                .WithoutTelemetry()
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand(Log, "build")
                .WithoutTelemetry()
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("Build FAILED.")
                .And.HaveStdOutContaining("Each child of '//postActions' should have a unique id");

            string locFolder = Path.Combine(tmpDir, "content/TemplateWithSourceName/.template.config/localize");

            Assert.IsFalse(Directory.Exists(locFolder));
            Directory.Delete(tmpDir, true);
        }
    }
}
