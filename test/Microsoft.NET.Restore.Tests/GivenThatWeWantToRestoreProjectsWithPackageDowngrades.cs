// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Restore.Tests
{
    [TestClass]
    public class GivenThatWeWantToRestoreProjectsWithPackageDowngrades : SdkTest
    {
        [TestMethod]
        public void DowngradeWarningsAreWarningsByDefault()
        {
            const string testProjectName = "ProjectWithDowngradeWarning";
            var testProject = new TestProject()
            {
                Name = testProjectName,
                TargetFrameworks = "netstandard2.0",
            };

            testProject.PackageReferences.Add(new TestPackageReference("NuGet.Packaging", "3.5.0", null));
            testProject.PackageReferences.Add(new TestPackageReference("NuGet.Commands", "4.0.0", null));

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var packagesFolder = Path.Combine(SdkTestContext.Current.TestExecutionDirectory, "packages", testProjectName);

            var restoreCommand = testAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand
                .Execute($"/p:RestorePackagesPath={packagesFolder}")
                .Should().Pass()
                .And.HaveStdOutContaining("warning NU1605");

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should().Pass();
        }

        [TestMethod]
        public void DowngradeWarningsCanBePromotedToErrors()
        {
            const string testProjectName = "ProjectWithDowngradeWarning";
            var testProject = new TestProject()
            {
                Name = testProjectName,
                TargetFrameworks = "netstandard2.0",
            };

            testProject.AdditionalProperties.Add("WarningsAsErrors", "NU1605");
            testProject.PackageReferences.Add(new TestPackageReference("NuGet.Packaging", "3.5.0", null));
            testProject.PackageReferences.Add(new TestPackageReference("NuGet.Commands", "4.0.0", null));

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var packagesFolder = Path.Combine(SdkTestContext.Current.TestExecutionDirectory, "packages", testProjectName);

            var restoreCommand = testAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand
                .Execute($"/p:RestorePackagesPath={packagesFolder}")
                .Should().Fail()
                .And.HaveStdOutContaining("error NU1605");
        }

        [TestMethod]
        public void WarningsNotAsErrorsExcludesDowngradeWarningsFromTreatWarningsAsErrors()
        {
            const string testProjectName = "ProjectWithDowngradeWarning";
            var testProject = new TestProject()
            {
                Name = testProjectName,
                TargetFrameworks = "netstandard2.0",
            };

            testProject.AdditionalProperties.Add("NuGetAudit", "false");
            testProject.AdditionalProperties.Add("TreatWarningsAsErrors", "true");
            testProject.AdditionalProperties.Add("WarningsNotAsErrors", "NU1605");
            testProject.PackageReferences.Add(new TestPackageReference("NuGet.Packaging", "3.5.0", null));
            testProject.PackageReferences.Add(new TestPackageReference("NuGet.Commands", "4.0.0", null));

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var packagesFolder = Path.Combine(SdkTestContext.Current.TestExecutionDirectory, "packages", testProjectName);

            var restoreCommand = testAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand
                .Execute($"/p:RestorePackagesPath={packagesFolder}")
                .Should().Pass()
                .And.HaveStdOutContaining("warning NU1605");
        }
    }
}
