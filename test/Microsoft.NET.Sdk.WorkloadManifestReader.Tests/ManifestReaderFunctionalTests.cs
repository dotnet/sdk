// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.TestFramework;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace ManifestReaderTests
{
    [TestClass]
    public class ManifestReaderFunctionalTests : SdkTest
    {
        private readonly string ManifestPath;

        public ManifestReaderFunctionalTests()
        {
            ManifestPath = Path.Combine(TestAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "Sample.json");
        }

        [TestMethod]
        public void ItShouldGetAllTemplatesPacks()
        {
            WorkloadResolver workloadResolver = SetUp();
            var result = workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Template);
            result.Should().HaveCount(1);
            var templateItem = result.First();
            templateItem.Id.ToString().Should().Be("Xamarin.Android.Templates");
            templateItem.IsStillPacked.Should().BeFalse();
            templateItem.Kind.Should().Be(WorkloadPackKind.Template);
            templateItem.Path.Should()
                .Be(Path.Combine("fakepath", "template-packs", "xamarin.android.templates.1.0.3.nupkg"));
        }

        [TestMethod]
        public void ItShouldGetAllSdkPacks()
        {
            WorkloadResolver workloadResolver = SetUp();
            var result = workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Sdk);
            result.Should().HaveCount(5);
            var androidWorkloads = result.Single(w => w.Id == "Xamarin.Android.Sdk");
            androidWorkloads.Id.ToString().Should().Be("Xamarin.Android.Sdk");
            androidWorkloads.IsStillPacked.Should().BeTrue();
            androidWorkloads.Kind.Should().Be(WorkloadPackKind.Sdk);
            androidWorkloads.Version.Should().Be("8.4.7");
            androidWorkloads.Path.Should().Be(Path.Combine("fakepath", "packs", "Xamarin.Android.Sdk", "8.4.7"));
        }

        [TestMethod]
        public void ItShouldGetWorkloadDescription()
        {
            WorkloadResolver workloadResolver = SetUp();
            var result = workloadResolver.GetWorkloadInfo(new WorkloadId("xamarin-android"));
            result.Description.Should().Be("Create, build and run Android apps");
        }

        private WorkloadResolver SetUp()
        {
            var workloadResolver =
                WorkloadResolver.CreateForTests(new FakeManifestProvider(new[] { ManifestPath }),
                    "fakepath");

            workloadResolver.ReplaceFilesystemChecksForTest(fileExists: (_) => true, directoryExists: (_) => true);
            return workloadResolver;
        }

        [TestMethod]
        public void GivenTemplateNupkgDoesNotExistOnDiskItShouldReturnEmpty()
        {
            var workloadResolver =
                WorkloadResolver.CreateForTests(new FakeManifestProvider(new[] { ManifestPath }),
                    "fakepath");
            workloadResolver.ReplaceFilesystemChecksForTest(fileExists: (_) => false, directoryExists: (_) => true);
            var result = workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Template);
            result.Should().HaveCount(0);
        }

        [TestMethod]
        public void GivenWorkloadSDKsDirectoryNotExistOnDiskItShouldReturnEmpty()
        {
            var workloadResolver =
                WorkloadResolver.CreateForTests(new FakeManifestProvider(new[] { ManifestPath }),
                    "fakepath");
            workloadResolver.ReplaceFilesystemChecksForTest(fileExists: (_) => true, directoryExists: (_) => false);
            var result = workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Sdk);
            result.Should().HaveCount(0);
        }

        [TestMethod]
        public void ItCanReadIntegerVersion()
        {
            var testFolder = TestAssetsManager.CreateTestDirectory().Path;
            var manifestPath = Path.Combine(testFolder, "manifest.json");
            File.WriteAllText(manifestPath, @"
{
    ""version"": 5
}");

            var workloadResolver =
                WorkloadResolver.CreateForTests(new FakeManifestProvider(manifestPath), "fakepath");

            workloadResolver.ReplaceFilesystemChecksForTest(fileExists: (_) => true, directoryExists: (_) => true);

            workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Template).Should().BeEmpty();

        }
    }
}
