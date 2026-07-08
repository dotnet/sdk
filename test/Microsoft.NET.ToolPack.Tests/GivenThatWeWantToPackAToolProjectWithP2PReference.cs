// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using NuGet.Packaging;

namespace Microsoft.NET.ToolPack.Tests
{
    [TestClass]
    public class GivenThatWeWantToPackAToolProjectWithP2PReference : SdkTest
    {
        private string SetupNuGetPackage([CallerMemberName] string callingMethod = "")
        {
            TestAsset testAsset = TestAssetsManager
                .CopyTestAsset("PortableToolWithP2P", callingMethod)
                .WithSource();

            var packCommand = new PackCommand(testAsset, "App");

            packCommand.Execute();

            return packCommand.GetNuGetPackage();
        }

        [TestMethod]
        public void It_packs_successfully()
        {
            var nugetPackage = SetupNuGetPackage();
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                nupkgReader
                    .GetToolItems()
                    .Should().NotBeEmpty();
            }
        }

        [TestMethod]
        public void It_contains_dependencies_dll()
        {
            var nugetPackage = SetupNuGetPackage();
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGet.Frameworks.NuGetFramework framework in supportedFrameworks)
                {
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/Library.dll");
                }
            }
        }

        [TestMethod]
        public void It_does_not_add_p2p_references_as_package_references_to_nuspec()
        {
            var nugetPackage = SetupNuGetPackage();
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                nupkgReader
                    .GetPackageDependencies()
                    .Should().BeEmpty();
            }
        }

        [TestMethod]
        public void It_contains_folder_structure_tfm_any()
        {
            var nugetPackage = SetupNuGetPackage();
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                nupkgReader
                    .GetToolItems()
                    .Should().Contain(
                        f => f.Items.
                            Contains($"tools/{f.TargetFramework.GetShortFolderName()}/any/consoledemo.dll"));
            }
        }
    }
}
