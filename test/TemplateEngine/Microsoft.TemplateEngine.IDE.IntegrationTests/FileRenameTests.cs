// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.IDE.IntegrationTests.Utils;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    public class FileRenameTests : IClassFixture<PackageManager>
    {
        private PackageManager _packageManager;
        public FileRenameTests(PackageManager packageManager)
        {
            _packageManager = packageManager;
        }

        public static IEnumerable<object[]> Get_FileRename_TestData()
        {
            yield return new object[]
                       {
                "TemplateWithRenames",
                "--foo baz --testForms TestProject",
                new MockCreationEffects()
                    .WithPrimaryOutputs("TestProject1.cs", "testproject2.cs", "TESTPROJECT3.cs", "baz.cs", "BAZ.cs")
                    .WithFileChange(new MockFileChange("bar/bar.cs", "baz/baz.cs", ChangeKind.Create))
                    .WithFileChange(new MockFileChange("bar.cs", "baz.cs", ChangeKind.Create))
                    .WithFileChange(new MockFileChange("bar_uc.cs", "BAZ.cs", ChangeKind.Create))
                    .WithFileChange(new MockFileChange("MyProject1.cs", "TestProject1.cs", ChangeKind.Create))
                    .WithFileChange(new MockFileChange("myproject2.cs", "testproject2.cs", ChangeKind.Create))
                    .WithFileChange(new MockFileChange("MYPROJECT3.cs", "TESTPROJECT3.cs", ChangeKind.Create))
                    .Without("bar.cs", "bar/bar.cs", "bar_uc.cs", "MyProject1.cs", "myproject2.cs", "MYPROJECT3.cs")
                       };

            yield return new object[]
            {
                "TemplateWithSourceName",
                "--name baz",
                 new MockCreationEffects()
                    .WithPrimaryOutputs("baz.cs", "baz/baz.cs")
                    .WithFileChange(new MockFileChange("bar/bar.cs", "baz/baz.cs", ChangeKind.Create))
                    .WithFileChange(new MockFileChange("bar.cs", "baz.cs", ChangeKind.Create))
                    .Without("bar.cs", "bar/bar.cs")
            };

            yield return new object[]
            {
                "TemplateWithUnspecifiedSourceName",
                "--name baz",
                 new MockCreationEffects()
                    .WithPrimaryOutputs("bar.cs", "bar/bar.cs")
                    .WithFileChange(new MockFileChange("bar/bar.cs", "bar/bar.cs", ChangeKind.Create))
                    .WithFileChange(new MockFileChange("bar.cs", "bar.cs", ChangeKind.Create))
                    .Without("baz.cs", "baz/baz.cs")
            };

            //tests are not working due to bugs:
            // -file changes are not taking into account source modifiers https://github.com/dotnet/templating/issues/2746
            yield return new object[]
            {
                "TemplateWithSourceNameAndCustomSourcePath",
                "--name bar",
                 new MockCreationEffects()
                    .WithPrimaryOutputs("bar.name.txt", "bar/bar.cs")
                    .WithFileChange(new MockFileChange("Custom/Path/foo/foo.cs", "bar/bar.cs", ChangeKind.Create))
                    .WithFileChange(new MockFileChange("Custom/Path/foo.name.txt", "bar.name.txt", ChangeKind.Create))
                    .Without("Custom/Path/")
            };

            yield return new object[]
            {
                "TemplateWithSourceNameAndCustomTargetPath",
                "--name bar",
                 new MockCreationEffects()
                    .WithPrimaryOutputs("Custom/Path/bar.name.txt", "Custom/Path/bar/bar.cs")
                    .WithFileChange(new MockFileChange("foo/foo.cs", "Custom/Path/bar/bar.cs", ChangeKind.Create))
                    .WithFileChange(new MockFileChange("foo.name.txt", "Custom/Path/bar.name.txt", ChangeKind.Create))
                    .Without("foo.name.txt", "foo/")
            };

            yield return new object[]
            {
                "TemplateWithSourceNameAndCustomSourceAndTargetPaths",
                "--name bar",
                 new MockCreationEffects()
                    .WithPrimaryOutputs("Target/Output/bar/bar.cs", "Target/Output/bar.name.txt")
                    .WithFileChange(new MockFileChange("Src/Custom/Path/foo/foo.cs", "Target/Output/bar/bar.cs", ChangeKind.Create))
                    .WithFileChange(new MockFileChange("Src/Custom/Path/foo.name.txt", "Target/Output/bar.name.txt", ChangeKind.Create))
                    .Without("Src/Custom/Path/")
            };

            yield return new object[]
            {
                "TemplateWithSourcePathOutsideConfigRoot",
                "--name baz",
                 new MockCreationEffects()
                    .WithPrimaryOutputs("blah/MountPointRoot/mount.baz.cs", "blah/MountPointRoot/baz/baz.baz.cs", "blah/MountPointRoot/baz/bar/bar.baz.cs")
                    .WithFileChange(new MockFileChange("../../../MountPointRoot/mount.foo.cs", "blah/MountPointRoot/mount.baz.cs", ChangeKind.Create))
                    .WithFileChange(new MockFileChange("../../../MountPointRoot/foo/foo.foo.cs", "blah/MountPointRoot/baz/baz.baz.cs", ChangeKind.Create))
                    .WithFileChange(new MockFileChange("../../../MountPointRoot/foo/bar/bar.foo.cs", "blah/MountPointRoot/baz/bar/bar.baz.cs", ChangeKind.Create))
                    .Without("MountPointRoot/")
            };

            yield return new object[]
            {
                "TemplateWithSourceNameInTargetPathGetsRenamed",
                "--name baz",
                 new MockCreationEffects()
                    .WithPrimaryOutputs("bar/baz/baz.cs")
                    .WithFileChange(new MockFileChange("foo.cs", "bar/baz/baz.cs", ChangeKind.Create))
                    .Without("bar/foo/")
            };

            yield return new object[]
            {
                "TemplateWithDerivedSymbolFileRename",
                "--name Last.Part.Is.For.Rename",
                 new MockCreationEffects()
                    .WithPrimaryOutputs("Rename.cs")
                    .WithFileChange(new MockFileChange("Application1.cs", "Rename.cs", ChangeKind.Create))
                    .Without("Application1.cs")
            };

            yield return new object[]
            {
                "TemplateWithMultipleRenamesOnSameFile",
                "--fooRename base --barRename ball",
                 new MockCreationEffects()
                    .WithPrimaryOutputs("ballandbase.txt", "baseball.txt")
                    .WithFileChange(new MockFileChange("foobar.txt", "baseball.txt", ChangeKind.Create))
                    .WithFileChange(new MockFileChange("barandfoo.txt", "ballandbase.txt", ChangeKind.Create))
                    .Without("foobar.txt", "barfoo.txt")
            };

            yield return new object[]
            {
                "TemplateWithMultipleRenamesOnSameFileHandlesOverlap",
                "--fooRename pin --oobRename ball",
                 new MockCreationEffects()
                    .WithPrimaryOutputs("pinb.txt")
                    .WithFileChange(new MockFileChange("foob.txt", "pinb.txt", ChangeKind.Create))
                    .Without("foob.txt", "fball.txt")
            };

            yield return new object[]
            {
                "TemplateWithMultipleRenamesOnSameFileHandlesInducedOverlap",
                "--fooRename bar --barRename baz",
                 new MockCreationEffects()
                    .WithPrimaryOutputs("bar.txt")
                    .WithFileChange(new MockFileChange("foo.txt", "bar.txt", ChangeKind.Create))
                    .Without("foo.txt", "baz.txt")
            };

            yield return new object[]
            {
                "TemplateWithCaseSensitiveNameBasedRenames",
                "--name NewName",
                new MockCreationEffects()
                    .WithPrimaryOutputs("Norenamepart/FileNorenamepart.txt", "Norenamepart/FileYesNewName.txt", "YesNewName/FileNorenamepart.txt", "YesNewName/FileYesNewName.txt" )
                    .WithFileChange(new MockFileChange("Norenamepart/FileNorenamepart.txt", "Norenamepart/FileNorenamepart.txt", ChangeKind.Create))
                    .WithFileChange(new MockFileChange("Norenamepart/FileYesRenamePart.txt", "Norenamepart/FileYesNewName.txt", ChangeKind.Create))
                    .WithFileChange(new MockFileChange("YesRenamePart/FileNorenamepart.txt", "YesNewName/FileNorenamepart.txt", ChangeKind.Create))
                    .WithFileChange(new MockFileChange("YesRenamePart/FileYesRenamePart.txt", "YesNewName/FileYesNewName.txt", ChangeKind.Create))
                    .Without("YesNewName/FileNoNewName.txt", "NoNewName/", "bar_uc.cs", "Norenamepart/FileNoNewName.txt")
            };

            yield return new object[]
            {
                "TemplateWithJoinAndFolderRename",
                "--product Office",
                new MockCreationEffects()
                    .WithPrimaryOutputs("Source/Api/Microsoft/Office/bar.cs")
                    .WithFileChange(new MockFileChange("Api/bar.cs", "Source/Api/Microsoft/Office/bar.cs", ChangeKind.Create))
                    .Without("Api/bar.cs")
            };

            yield return new object[]
            {
                "TemplateWithSourceBasedRenames",
                "--barRename NewName",
                 new MockCreationEffects()
                    .WithPrimaryOutputs("baz.cs", "NewName.cs")
                    .WithFileChange(new MockFileChange("foo.cs", "baz.cs", ChangeKind.Create))
                    .WithFileChange(new MockFileChange("foo.cs", "NewName.cs", ChangeKind.Create))
                    .Without("foo.cs")
            };
        }
    

        [Theory]
        [MemberData(nameof(Get_FileRename_TestData))]
        internal async Task GetCreationEffectsTest(string templateName, string parameters, MockCreationEffects expectedResult)
        {
            Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper();
            await bootstrapper.InstallTestTemplateAsync(templateName).ConfigureAwait(false);

            string name = BasicParametersParser.GetNameFromParameterString(parameters);
            string output = BasicParametersParser.GetOutputFromParameterString(parameters);
            Dictionary<string, string> parametersDict = BasicParametersParser.ParseParameterString(parameters);


            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter(templateName) }).ConfigureAwait(false);
            ITemplateInfo template = foundTemplates.Single(template => template.Info.ShortNameList.Contains($"TestAssets.{templateName}")).Info;
            ICreationEffects result = await bootstrapper.GetCreationEffectsAsync(template, name, output, parametersDict, "").ConfigureAwait(false);

            Assert.Equal(expectedResult.CreationResult.PrimaryOutputs.Count, result.CreationResult.PrimaryOutputs.Count);
            Assert.Equal(
                expectedResult.CreationResult.PrimaryOutputs.Select(po => po.Path).OrderBy(s => s, StringComparer.OrdinalIgnoreCase),
                result.CreationResult.PrimaryOutputs.Select(po => po.Path).OrderBy(s => s, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);


            IFileChangeComparer comparer = new IFileChangeComparer();
            Assert.Equal(expectedResult.FileChanges.Count, result.FileChanges.Count);
            Assert.Equal(
                expectedResult.FileChanges.OrderBy(s => s, comparer),
                result.FileChanges.OrderBy(s => s, comparer),
                comparer);
        }

        [Theory]
        [MemberData(nameof(Get_FileRename_TestData))]
        internal async Task CreateTest(string templateName, string parameters, MockCreationEffects expectedResult)
        {
            Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper();
            await bootstrapper.InstallTestTemplateAsync(templateName).ConfigureAwait(false);

            string name = BasicParametersParser.GetNameFromParameterString(parameters);
            string output = BasicParametersParser.GetOutputFromParameterString(parameters);
            Dictionary<string, string> parametersDict = BasicParametersParser.ParseParameterString(parameters);

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter(templateName) }).ConfigureAwait(false);
            ITemplateInfo template = foundTemplates.Single(template => template.Info.ShortNameList.Contains($"TestAssets.{templateName}")).Info;
            var result = await bootstrapper.CreateAsync(template, name, output, parametersDict, false, "").ConfigureAwait(false);

            Assert.Equal(expectedResult.CreationResult.PrimaryOutputs.Count, result.PrimaryOutputs.Count);
            Assert.Equal(
                expectedResult.CreationResult.PrimaryOutputs.Select(po => po.Path).OrderBy(s => s, StringComparer.OrdinalIgnoreCase),
                result.PrimaryOutputs.Select(po => po.Path).OrderBy(s => s, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

            foreach (string file in expectedResult.FileChanges.Where(fc => fc.ChangeKind != ChangeKind.Delete).Select(fc => fc.TargetRelativePath))
            {
                string expectedFilePath = Path.Combine(output, file);
                Assert.True(File.Exists(expectedFilePath));
            }
            foreach (string file in expectedResult.FileChanges.Where(fc => fc.ChangeKind == ChangeKind.Delete).Select(fc => fc.TargetRelativePath))
            {
                string expectedFilePath = Path.Combine(output, file);
                Assert.False(File.Exists(expectedFilePath));
            }

            foreach (string file in expectedResult.AbsentFiles)
            {
                string expectedFilePath = Path.Combine(output, file);
                Assert.False(File.Exists(expectedFilePath));
            }

            foreach (string dir in expectedResult.AbsentDirectories)
            {
                string expectedPath = Path.Combine(output, dir);
                Assert.False(Directory.Exists(expectedPath));
            }
        }

        [Theory]
        [MemberData(nameof(Get_FileRename_TestData))]
        internal async Task GetCreationEffectsTest_Package(string templateName, string parameters, MockCreationEffects expectedResult)
        {
            Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper();
            string packageLocation = _packageManager.PackTestTemplatesNuGetPackage();
            await bootstrapper.InstallTemplateAsync(packageLocation).ConfigureAwait(false);

            string name = BasicParametersParser.GetNameFromParameterString(parameters);
            string output = BasicParametersParser.GetOutputFromParameterString(parameters);
            Dictionary<string, string> parametersDict = BasicParametersParser.ParseParameterString(parameters);

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter(templateName) }).ConfigureAwait(false);
            ITemplateInfo template = foundTemplates.Single(template => template.Info.ShortNameList.Contains($"TestAssets.{templateName}")).Info;
            ICreationEffects result = await bootstrapper.GetCreationEffectsAsync(template, name, output, parametersDict, "").ConfigureAwait(false);

            Assert.Equal(expectedResult.CreationResult.PrimaryOutputs.Count, result.CreationResult.PrimaryOutputs.Count);
            Assert.Equal(
                expectedResult.CreationResult.PrimaryOutputs.Select(po => po.Path).OrderBy(s => s, StringComparer.OrdinalIgnoreCase),
                result.CreationResult.PrimaryOutputs.Select(po => po.Path).OrderBy(s => s, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);


            IFileChangeComparer comparer = new IFileChangeComparer();
            Assert.Equal(expectedResult.FileChanges.Count, result.FileChanges.Count);
            Assert.Equal(
                expectedResult.FileChanges.OrderBy(s => s, comparer),
                result.FileChanges.OrderBy(s => s, comparer),
                comparer);
        }

        [Theory]
        [MemberData(nameof(Get_FileRename_TestData))]
        internal async Task CreateTest_Package(string templateName, string parameters, MockCreationEffects expectedResult)
        {
            Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper();
            string packageLocation = _packageManager.PackTestTemplatesNuGetPackage();
            await bootstrapper.InstallTemplateAsync(packageLocation).ConfigureAwait(false);

            string name = BasicParametersParser.GetNameFromParameterString(parameters);
            string output = BasicParametersParser.GetOutputFromParameterString(parameters);
            Dictionary<string, string> parametersDict = BasicParametersParser.ParseParameterString(parameters);

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter(templateName) }).ConfigureAwait(false);
            ITemplateInfo template = foundTemplates.Single(template => template.Info.ShortNameList.Contains($"TestAssets.{templateName}")).Info;
            var result = await bootstrapper.CreateAsync(template, name, output, parametersDict, false, "").ConfigureAwait(false);

            Assert.Equal(expectedResult.CreationResult.PrimaryOutputs.Count, result.PrimaryOutputs.Count);
            Assert.Equal(
                expectedResult.CreationResult.PrimaryOutputs.Select(po => po.Path).OrderBy(s => s, StringComparer.OrdinalIgnoreCase),
                result.PrimaryOutputs.Select(po => po.Path).OrderBy(s => s, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

            foreach (string file in expectedResult.FileChanges.Where(fc => fc.ChangeKind != ChangeKind.Delete).Select(fc => fc.TargetRelativePath))
            {
                string expectedFilePath = Path.Combine(output, file);
                Assert.True(File.Exists(expectedFilePath));
            }
            foreach (string file in expectedResult.FileChanges.Where(fc => fc.ChangeKind == ChangeKind.Delete).Select(fc => fc.TargetRelativePath))
            {
                string expectedFilePath = Path.Combine(output, file);
                Assert.False(File.Exists(expectedFilePath));
            }

            foreach (string file in expectedResult.AbsentFiles)
            {
                string expectedFilePath = Path.Combine(output, file);
                Assert.False(File.Exists(expectedFilePath));
            }

            foreach (string dir in expectedResult.AbsentDirectories)
            {
                string expectedPath = Path.Combine(output, dir);
                Assert.False(Directory.Exists(expectedPath));
            }

        }


    }
}
