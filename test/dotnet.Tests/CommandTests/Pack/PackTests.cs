// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using Microsoft.Build.Logging.StructuredLogger;
using NuGet.Packaging;

namespace Microsoft.DotNet.Pack.Tests
{
    public class PackTests : SdkTest
    {
        public PackTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void OutputsPackagesToConfigurationSubdirWhenOutputParameterIsNotPassed()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestLibraryWithConfiguration")
                                         .WithSource();

            var packCommand = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path);

            var result = packCommand.Execute("-c", "Test");

            result.Should().Pass();

            var outputDir = new DirectoryInfo(Path.Combine(testInstance.Path, "bin", "Test"));

            outputDir.Should().Exist()
                          .And.HaveFiles(new[]
                                            {
                                                "TestLibraryWithConfiguration.1.0.0.nupkg"
                                            });
        }

        [Fact]
        public void OutputsPackagesFlatIntoOutputDirWhenOutputParameterIsPassed()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestLibraryWithConfiguration")
                .WithSource();

            var outputDir = new DirectoryInfo(Path.Combine(testInstance.Path, "bin2"));

            var packCommand = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("-o", outputDir.FullName)
                .Should().Pass();

            outputDir.Should().Exist()
                          .And.HaveFiles(new[]
                                            {
                                                "TestLibraryWithConfiguration.1.0.0.nupkg"
                                            });
        }

        [Fact]
        public void SettingVersionSuffixFlag_ShouldStampAssemblyInfoInOutputAssemblyAndPackage()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestLibraryWithConfiguration")
                .WithSource();

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--version-suffix", "85", "-c", "Debug")
                .Should().Pass();

            var output = new FileInfo(Path.Combine(testInstance.Path,
                                     "bin", "Debug", "netstandard1.5",
                                     "TestLibraryWithConfiguration.dll"));

            var informationalVersion = PeReaderUtils.GetAssemblyAttributeValue(output.FullName, "AssemblyInformationalVersionAttribute");

            informationalVersion.Should().NotBeNull()
                                .And.StartWith("1.0.0-85"); // ensure that build metadata doesn't bork the test

            var outputPackage = new FileInfo(Path.Combine(testInstance.Path,
                                            "bin", "Debug",
                                            "TestLibraryWithConfiguration.1.0.0-85.nupkg"));

            outputPackage.Should().Exist();
        }

        [Fact]
        public void HasIncludedFiles()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("EndToEndTestApp")
                .WithSource();

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("-c", "Debug")
                .Should().Pass();

            var outputPackage = new FileInfo(Path.Combine(testInstance.Path,
                                            "bin", "Debug",
                                            "EndToEndTestApp.1.0.0.nupkg"));

            outputPackage.Should().Exist();

            ZipFile.Open(outputPackage.FullName, ZipArchiveMode.Read)
                .Entries
                .Should().Contain(e => e.FullName == "newpath/pack1.txt")
                     .And.Contain(e => e.FullName == "anotherpath/pack2.txt");
        }

        [Fact]
        public void PackAddsCorrectFilesForProjectsWithOutputNameSpecified()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("LibraryWithOutputAssemblyName")
                    .WithSource();

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("-c", "Debug", "-p:PackageID=LibraryWithOutputAssemblyName", "--include-symbols")
                .Should().Pass();


            var outputPackage = new FileInfo(Path.Combine(testInstance.Path,
                                            "bin", "Debug",
                                            "LibraryWithOutputAssemblyName.1.0.0.nupkg"));

            outputPackage.Should().Exist();

            ZipFile.Open(outputPackage.FullName, ZipArchiveMode.Read)
                .Entries
                .Should().Contain(e => e.FullName == "lib/netstandard1.5/MyLibrary.dll");

            var symbolsPackage = new FileInfo(Path.Combine(testInstance.Path,
                                             "bin", "Debug",
                                             "LibraryWithOutputAssemblyName.1.0.0.symbols.nupkg"));

            symbolsPackage.Should().Exist();

            ZipFile.Open(symbolsPackage.FullName, ZipArchiveMode.Read)
                .Entries
                .Should().Contain(e => e.FullName == "lib/netstandard1.5/MyLibrary.dll")
                     .And.Contain(e => e.FullName == "lib/netstandard1.5/MyLibrary.pdb");
        }

        [Theory]
        [InlineData("TestAppSimple")]
        [InlineData("FSharpTestAppSimple")]
        public void PackWorksWithLocalProject(string projectName)
        {
            var testInstance = _testAssetsManager.CopyTestAsset(projectName)
                .WithSource();

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();
        }

        [Fact]
        public void ItImplicitlyRestoresAProjectWhenPackaging()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource();

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();
        }

        [Fact]
        public void ItDoesNotImplicitlyBuildAProjectWhenPackagingWithTheNoBuildOption()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource();

            var result = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--no-build");

            result.Should().Fail();
            if (!TestContext.IsLocalized())
            {
                result.Should().NotHaveStdOutContaining("Restore")
                    .And.HaveStdOutContaining("project.assets.json");
            }
        }

        [Fact]
        public void ItDoesNotImplicitlyRestoreAProjectWhenPackagingWithTheNoRestoreOption()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource();

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--no-restore")
                .Should().Fail()
                .And.HaveStdOutContaining("project.assets.json");
        }

        [Fact]
        public void HasServiceableFlagWhenArgumentPassed()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestLibraryWithConfiguration")
                .WithSource();

            var packCommand = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path);

            var result = packCommand.Execute("-c", "Debug", "--serviceable");

            result.Should().Pass();

            var outputDir = new DirectoryInfo(Path.Combine(testInstance.Path, "bin", "Debug"));

            outputDir.Should().Exist()
                          .And.HaveFile("TestLibraryWithConfiguration.1.0.0.nupkg");

            var outputPackage = new FileInfo(Path.Combine(outputDir.FullName, "TestLibraryWithConfiguration.1.0.0.nupkg"));

            var zip = ZipFile.Open(outputPackage.FullName, ZipArchiveMode.Read);

            zip.Entries.Should().Contain(e => e.FullName == "TestLibraryWithConfiguration.nuspec");

            var manifestReader = new StreamReader(zip.Entries.First(e => e.FullName == "TestLibraryWithConfiguration.nuspec").Open());

            var nuspecXml = XDocument.Parse(manifestReader.ReadToEnd());

            var node = nuspecXml.Descendants().Single(e => e.Name.LocalName == "serviceable");

            Assert.Equal("true", node.Value);
        }

        [Fact]
        public void ItPacksAppWhenRestoringToSpecificPackageDirectory()
        {
            var rootPath = Path.Combine(_testAssetsManager.CreateTestDirectory().Path, "TestProject");
            Directory.CreateDirectory(rootPath);
            var rootDir = new DirectoryInfo(rootPath);

            string dir = "pkgs";

            new DotnetNewCommand(Log, "console", "-o", rootPath, "--no-restore")
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .Execute()
                .Should()
                .Pass();

            new DotnetRestoreCommand(Log, rootPath)
                .Execute("--packages", dir)
                .Should()
                .Pass();

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute("--no-restore")
                .Should()
                .Pass();

            new DirectoryInfo(Path.Combine(rootPath, "bin"))
                .Should().HaveFilesMatching("*.nupkg", SearchOption.AllDirectories);
        }

        [Fact]
        public void DotnetPackDoesNotPrintCopyrightInfo()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildTestApp")
                .WithSource();

            var result = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--nologo");

            result.Should().Pass();

            if (!TestContext.IsLocalized())
            {
                result.Should().NotHaveStdOutContaining("Copyright (C) Microsoft Corporation. All rights reserved.");
            }
        }

        [Fact]
        public void DotnetPackAcceptsRuntimeOption()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource();

            var result = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--runtime", "unknown");

            result.Should().Fail()
                .And.HaveStdOutContaining("NETSDK1083");
        }

        [Fact]
        public void DotnetPack_AcceptsPropertiesOption()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestNuspecProject")
                .WithSource();
            string nuspecPath = Path.Combine(testInstance.Path, "PackNoCsproj.nuspec");
            var result = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(nuspecPath, "--property", "id=CustomID");

            result.Should().Pass();

            var outputDir = new DirectoryInfo(testInstance.Path);
            outputDir.Should().Exist()
                .And.HaveFile("CustomId.1.0.0.nupkg");

            var nupkgPath = Path.Combine(testInstance.Path, "CustomId.1.0.0.nupkg");
            File.Exists(nupkgPath).Should().BeTrue("The package should be created with the custom id");

            using (var nupkgReader = new PackageArchiveReader(nupkgPath))
            {
                var nuspecReader = nupkgReader.NuspecReader;
                nuspecReader.Should().NotBeNull();

                nuspecReader.GetId().Should().Be("CustomID", "The nuspec file should contain the custom id"); 
            }
        }

        [Fact]
        public void DotnetPack_AcceptsVersionOption()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestNuspecProject")
                .WithSource();
            string nuspecPath = Path.Combine(testInstance.Path, "PackNoCsproj.nuspec");
            var result = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(nuspecPath, "--version", "1.2.3");

            result.Should().Pass();

            var outputDir = new DirectoryInfo(testInstance.Path);
            outputDir.Should().Exist()
                .And.HaveFile("PackNoCsproj.1.2.3.nupkg");

            var nupkgPath = Path.Combine(testInstance.Path, "PackNoCsproj.1.2.3.nupkg");
            File.Exists(nupkgPath).Should().BeTrue("The package should be created with the specified version.");

            using (var nupkgReader = new PackageArchiveReader(nupkgPath))
            {
                var nuspecReader = nupkgReader.NuspecReader;
                nuspecReader.Should().NotBeNull();

                nuspecReader.GetVersion().ToNormalizedString().Should().Be("1.2.3", "The package should contain the custom version");
            }
        }

        [Fact]
        public void DotnetPack_FailsWhenVersionOptionHasNoValue()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestNuspecProject")
                .WithSource();
            string nuspecPath = Path.Combine(testInstance.Path, "PackNoCsproj.nuspec");
            var result = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(nuspecPath, "--version");

            result.Should().Fail();
            result.StdErr.Should().Contain("Required argument missing for option: '--version'.");
        }

        [Fact]
        public void DotnetPack_AcceptsCustomProperties()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestNuspecProject")
                .WithSource();

            string nuspecPath = Path.Combine(testInstance.Path, "PackNoCsproj.nuspec");

            var result = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(
                nuspecPath, "--property", "id=CustomValue",
                "--property", "authors=CustomAuthor"
                );

            result.Should().Pass();

            var nupkgPath = Path.Combine(testInstance.Path, "CustomId.1.0.0.nupkg");
            File.Exists(nupkgPath).Should().BeTrue("The package should be created with the custom id.");

            using (var nupkgReader = new PackageArchiveReader(nupkgPath))
            {
                var nuspecReader = nupkgReader.NuspecReader;
                nuspecReader.Should().NotBeNull();

                nuspecReader.GetId().Should().Be("CustomID", "The nuspec file should contain the custom id");
                nuspecReader.GetAuthors().Should().Be("CustomAuthor", "The nuspec file should contain the custom author");
            }
        }

        [Theory]
        [InlineData("Debug")]
        [InlineData("Release")]
        public void DotnetPack_AcceptsConfigurationOption(string configuration)
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestNuspecProject")
                .WithSource();
            string nuspecPath = Path.Combine(testInstance.Path, "PackNoCsproj.nuspec");
            var result = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(nuspecPath, "--configuration", configuration);
            result.Should().Pass();

            var outputPackage = new FileInfo(Path.Combine(testInstance.Path, "bin", configuration, "PackNoCsproj.1.0.0.nupkg"));
            outputPackage.Should().Exist();
        }

        [Fact]
        public void DotnetPack_AcceptsOutputOption()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestNuspecProject")
                .WithSource();
            string nuspecPath = Path.Combine(testInstance.Path, "PackNoCsproj.nuspec");
            string outputDirPath = Path.Combine(testInstance.Path, "output");

            var result = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(nuspecPath, "--output", outputDirPath);
            result.Should().Pass();
            var outputDir = new DirectoryInfo(outputDirPath);
            outputDir.Should().Exist()
                .And.HaveFile("PackNoCsproj.1.0.0.nupkg");

            var nupkgPath = Path.Combine(outputDirPath, "PackNoCsproj.1.0.0.nupkg");
            File.Exists(nupkgPath).Should().BeTrue("The package should be created in the specified output directory.");
            using var zip = ZipFile.OpenRead(nupkgPath);
            var nuspecEntry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".nuspec"));
            nuspecEntry.Should().NotBeNull("The .nuspec file should exist in the package.");
        }

        [Fact]
        public void DotnetPack_FailsForNonExistentNuspec()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestNuspecProject")
                .WithSource();
            string nuspecPath = Path.Combine(testInstance.Path, "NonExistent.nuspec");

            var result = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(nuspecPath);

            result.Should().Fail();            
        }
    }
}
