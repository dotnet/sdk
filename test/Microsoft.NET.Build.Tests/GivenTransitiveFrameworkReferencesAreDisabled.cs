// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Tests
{
    public class GivenTransitiveFrameworkReferencesAreDisabled : SdkTest
    {
        public GivenTransitiveFrameworkReferencesAreDisabled(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void TargetingPacksAreNotDownloadedIfNotDirectlyReferenced()
        {
            // With DisableTransitiveFrameworkReferenceDownloads=true, only targeting packs for
            // directly referenced frameworks should be downloaded — not for transitive ones.
            // Since this project only references NETCore.App (implicitly), only that targeting
            // pack should appear in the packages folder.
            string nugetPackagesFolder = _testAssetsManager.CreateTestDirectory(identifier: "packages").Path;

            var testProject = new TestProject()
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            //  Don't use AppHost in order to avoid additional download to packages folder
            testProject.AdditionalProperties["UseAppHost"] = "False";

            testProject.AdditionalProperties["DisableTransitiveFrameworkReferenceDownloads"] = "True";
            testProject.AdditionalProperties["RestorePackagesPath"] = nugetPackagesFolder;
            // disable implicit use of the Roslyn Toolset compiler package
            testProject.AdditionalProperties["BuildWithNetFrameworkHostedCompiler"] = false.ToString();

            //  Set packs folder to nonexistent folder so the project won't use installed targeting or runtime packs
            testProject.AdditionalProperties["NetCoreTargetingPackRoot"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            //  Package pruning may load data from the targeting packs directory.  Since we're disabling the targeting pack
            //  root, we need to allow it to succeed even if it can't find that data.
            testProject.AdditionalProperties["AllowMissingPrunePackageData"] = "true";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass();

            var expectedPackages = new List<string>()
            {
                "microsoft.netcore.app.ref"
            };

            Directory.EnumerateDirectories(nugetPackagesFolder)
                .Select(Path.GetFileName)
                .Should().BeEquivalentTo(expectedPackages);
        }

        [Fact]
        public void TransitiveFrameworkReferenceGeneratesError()
        {
            string nugetPackagesFolder = _testAssetsManager.CreateTestDirectory(identifier: "packages").Path;

            var referencedProject = new TestProject()
            {
                Name = "ReferencedProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            referencedProject.FrameworkReferences.Add("Microsoft.AspNetCore.App");

            var testProject = new TestProject()
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            //  Don't use AppHost in order to avoid additional download to packages folder
            testProject.AdditionalProperties["UseAppHost"] = "False";

            testProject.AdditionalProperties["DisableTransitiveFrameworkReferenceDownloads"] = "True";
            testProject.AdditionalProperties["RestorePackagesPath"] = nugetPackagesFolder;

            //  Set packs folder to nonexistent folder so the project won't use installed targeting or runtime packs
            testProject.AdditionalProperties["NetCoreTargetingPackRoot"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            //  Package pruning may load data from the targeting packs directory.  Since we're disabling the targeting pack
            //  root, we need to allow it to succeed even if it can't find that data.
            testProject.AdditionalProperties["AllowMissingPrunePackageData"] = "true";

            testProject.ReferencedProjects.Add(referencedProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("NETSDK1184:");
        }

        [Fact]
        public void TransitiveFrameworkReferenceGeneratesRuntimePackError()
        {
            string nugetPackagesFolder = _testAssetsManager.CreateTestDirectory(identifier: "packages").Path;

            var referencedProject = new TestProject()
            {
                Name = "ReferencedProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            referencedProject.FrameworkReferences.Add("Microsoft.AspNetCore.App");

            var testProject = new TestProject
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                SelfContained = "true",
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid()
            };
            testProject.AdditionalProperties["DisableTransitiveFrameworkReferenceDownloads"] = "True";
            testProject.AdditionalProperties["RestorePackagesPath"] = nugetPackagesFolder;

            testProject.ReferencedProjects.Add(referencedProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            //  ProcessFrameworkReferences runs before AddTransitiveFrameworkReferences, so it never
            //  creates RuntimePack items for the transitive ASP.NET reference.  ResolveRuntimePackAssets
            //  detects this and reports NETSDK1235, suggesting the user add a direct FrameworkReference.
            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("NETSDK1235:");
        }

    }
}
