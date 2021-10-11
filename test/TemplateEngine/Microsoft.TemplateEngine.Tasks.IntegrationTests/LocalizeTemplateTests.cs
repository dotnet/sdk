// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.Tasks.IntegrationTests
{
    public class LocalizeTemplateTests
    {
        private readonly ITestOutputHelper _log;

        public LocalizeTemplateTests(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void CanRunTask()
        {
            string tmpDir = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy("Resources\\BasicTemplatePackage", tmpDir, true);
            TestUtils.SetupNuGetConfigForPackagesLocation(tmpDir);

            new DotnetCommand(_log, "add", "TemplatePackage.csproj", "package", "Microsoft.TemplateEngine.Tasks", "--prerelease")
              .WithWorkingDirectory(tmpDir)
              .Execute()
              .Should()
              .Pass();

            new DotnetCommand(_log, "build")
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            string locFolder = Path.Combine(tmpDir, "content\\TemplateWithSourceName\\.template.config\\localize");

            Assert.True(Directory.Exists(locFolder));
            Assert.Equal(14, Directory.GetFiles(locFolder).Length);
            Assert.True(File.Exists(Path.Combine(locFolder, "templatestrings.de.json")));

            Directory.Delete(tmpDir, true);
        }

        [Fact]
        public void CanRunTaskSelectedLangs()
        {
            string tmpDir = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy("Resources\\TemplatePackageEnDe", tmpDir, true);
            TestUtils.SetupNuGetConfigForPackagesLocation(tmpDir);

            new DotnetCommand(_log, "add", "TemplatePackage.csproj", "package", "Microsoft.TemplateEngine.Tasks", "--prerelease")
              .WithWorkingDirectory(tmpDir)
              .Execute()
              .Should()
              .Pass();

            new DotnetCommand(_log, "build")
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            string locFolder = Path.Combine(tmpDir, "content\\TemplateWithSourceName\\.template.config\\localize");

            Assert.True(Directory.Exists(locFolder));
            Assert.Equal(2, Directory.GetFiles(locFolder).Length);
            Assert.True(File.Exists(Path.Combine(locFolder, "templatestrings.de.json")));
            Assert.False(File.Exists(Path.Combine(locFolder, "templatestrings.fr.json")));

            Directory.Delete(tmpDir, true);
        }

        [Fact]
        public void CanRunTaskSelectedTemplates()
        {
            string tmpDir = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy("Resources\\TemplatePackagePartiallyLocalized", tmpDir, true);
            TestUtils.SetupNuGetConfigForPackagesLocation(tmpDir);

            new DotnetCommand(_log, "add", "TemplatePackage.csproj", "package", "Microsoft.TemplateEngine.Tasks", "--prerelease")
              .WithWorkingDirectory(tmpDir)
              .Execute()
              .Should()
              .Pass();

            new DotnetCommand(_log, "build")
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            string locFolder = Path.Combine(tmpDir, "content\\localized\\.template.config\\localize");
            string noLocFolder = Path.Combine(tmpDir, "content\\non-localized\\.template.config\\localize");

            Assert.True(Directory.Exists(locFolder));
            Assert.Equal(14, Directory.GetFiles(locFolder).Length);
            Assert.True(File.Exists(Path.Combine(locFolder, "templatestrings.de.json")));
            Assert.False(Directory.Exists(noLocFolder));

            Directory.Delete(tmpDir, true);
        }

        [Fact]
        public void CanRunTaskAndDetectError()
        {
            string tmpDir = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy("Resources\\InvalidTemplatePackage", tmpDir, true);
            TestUtils.SetupNuGetConfigForPackagesLocation(tmpDir);

            new DotnetCommand(_log, "add", "TemplatePackage.csproj", "package", "Microsoft.TemplateEngine.Tasks", "--prerelease")
              .WithWorkingDirectory(tmpDir)
              .Execute()
              .Should()
              .Pass();

            new DotnetCommand(_log, "build")
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("Build FAILED.")
                .And.HaveStdOutContaining("error : Each child of \"//postActions\" should have a unique id");

            string locFolder = Path.Combine(tmpDir, "content\\TemplateWithSourceName\\.template.config\\localize");

            Assert.False(Directory.Exists(locFolder));
            Directory.Delete(tmpDir, true);
        }

    }
}
