// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToDetectAspireWorkloadDeprecation : SdkTest
    {
        public GivenThatWeWantToDetectAspireWorkloadDeprecation(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_errors_when_Aspire_host_with_old_SDK_version()
        {
            var testProject = new TestProject()
            {
                Name = "AspireHost",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            // Set the properties that would indicate old Aspire workload usage
            testProject.AdditionalProperties["IsAspireHost"] = "true";
            testProject.AdditionalProperties["AspireHostingSDKVersion"] = "8.1.0"; // Below 8.2.0

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1228")
                .And
                .HaveStdOutContaining("Aspire Workload which has been deprecated")
                .And
                .HaveStdOutContaining("https://aka.ms/aspire/update-to-sdk");
        }

        [Fact]
        public void It_errors_when_Aspire_host_with_no_SDK_version()
        {
            var testProject = new TestProject()
            {
                Name = "AspireHost",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            // Set only IsAspireHost without AspireHostingSDKVersion (simulating missing SDK)
            // This represents the old workload-based project pattern
            testProject.AdditionalProperties["IsAspireHost"] = "true";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1228")
                .And
                .HaveStdOutContaining("Aspire Workload which has been deprecated");
        }

        [Fact]
        public void It_does_not_error_when_Aspire_host_with_new_SDK_version()
        {
            var testProject = new TestProject()
            {
                Name = "AspireHost",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            // Set the properties with a new SDK version
            testProject.AdditionalProperties["IsAspireHost"] = "true";
            testProject.AdditionalProperties["AspireHostingSDKVersion"] = "9.0.0"; // At 9.0.0 or above

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("NETSDK1228");
        }

        [Fact]
        public void It_does_not_error_when_not_Aspire_host()
        {
            var testProject = new TestProject()
            {
                Name = "RegularApp",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            // No Aspire-related properties set

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("NETSDK1228");
        }

        [Fact]
        public void It_errors_for_old_workload_based_project_pattern()
        {
            var testProject = new TestProject()
            {
                Name = "AspireOldProject",
                TargetFrameworks = "net8.0",
                IsExe = true,
            };

            // Simulate an old workload-based Aspire project
            testProject.AdditionalProperties["IsAspireHost"] = "true";
            testProject.AdditionalProperties["UserSecretsId"] = "6dac1860-9125-4f25-b9f8-2790fdfd4b37";
            testProject.PackageReferences.Add(new TestPackageReference("Aspire.Hosting.AppHost", "8.2.2"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1228")
                .And
                .HaveStdOutContaining("Aspire Workload which has been deprecated");
        }

        [Fact]
        public void It_does_not_error_for_new_SDK_based_project_pattern()
        {
            var testProject = new TestProject()
            {
                Name = "AspireNewProject",
                TargetFrameworks = "net8.0",
                IsExe = true,
            };

            // Simulate a new SDK-based Aspire project
            testProject.AdditionalProperties["IsAspireHost"] = "true";
            testProject.AdditionalProperties["UserSecretsId"] = "e57bc21a-52dd-4fc5-b316-b0cb1625d3f3";
            testProject.AdditionalProperties["AspireHostingSDKVersion"] = "9.0.0"; // This would be set by Aspire.AppHost.Sdk
            testProject.PackageReferences.Add(new TestPackageReference("Aspire.Hosting.AppHost", "9.0.0"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("NETSDK1228");
        }
    }
}
