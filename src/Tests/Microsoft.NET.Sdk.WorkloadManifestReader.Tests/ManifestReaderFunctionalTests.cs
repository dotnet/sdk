// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.IO;
using Xunit;
using System.Linq;

namespace ManifestReaderTests
{
    public class ManifestReaderFunctionalTests
    {
        [Fact]
        public void ItShouldGetAllTemplatesPacks()
        {
            WorkloadResolver workloadResolver = SetUp();
            var result = workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Template);
            result.Should().HaveCount(1);
            var templateItem = result.First();
            templateItem.Id.Should().Be("Xamarin.Android.Templates");
            templateItem.IsStillPacked.Should().BeFalse();
            templateItem.Kind.Should().Be(WorkloadPackKind.Template);
            templateItem.Path.Should()
                .Be(Path.Combine("fakepath", "template-packs", "xamarin.android.templates.1.0.3.nupkg"));
        }

        [Fact]
        public void ItShouldGetAllSdkPacks()
        {
            WorkloadResolver workloadResolver = SetUp();
            var result = workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Sdk);
            result.Should().HaveCount(4);
            var androidWorkloads = result.Single(w => w.Id == "Xamarin.Android.Sdk");
            androidWorkloads.Id.Should().Be("Xamarin.Android.Sdk");
            androidWorkloads.IsStillPacked.Should().BeTrue();
            androidWorkloads.Kind.Should().Be(WorkloadPackKind.Sdk);
            androidWorkloads.Version.Should().Be("8.4.7");
            androidWorkloads.Path.Should().Be(Path.Combine("fakepath", "packs", "Xamarin.Android.Sdk", "8.4.7"));
        }

        private static WorkloadResolver SetUp()
        {
            var workloadResolver =
                WorkloadResolver.CreateForTests(new FakeManifestProvider(new[] {Path.Combine("Manifests", "Sample.json")}),
                    "fakepath", ManifestTests.TEST_RUNTIME_IDENTIFIER_CHAIN);

            workloadResolver.ReplaceFilesystemChecksForTest(fileExists: (_) => true, directoryExists: (_) => true);
            return workloadResolver;
        }

        [Fact]
        public void GivenTemplateNupkgDoesNotExistOnDiskItShouldReturnEmpty()
        {
            var workloadResolver =
                WorkloadResolver.CreateForTests(new FakeManifestProvider(new[] {Path.Combine("Manifests", "Sample.json")}),
                    "fakepath", ManifestTests.TEST_RUNTIME_IDENTIFIER_CHAIN);
            workloadResolver.ReplaceFilesystemChecksForTest(fileExists: (_) => false, directoryExists: (_) => true);
            var result = workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Template);
            result.Should().HaveCount(0);
        }

        [Fact]
        public void GivenWorkloadSDKsDirectoryNotExistOnDiskItShouldReturnEmpty()
        {
            var workloadResolver =
                WorkloadResolver.CreateForTests(new FakeManifestProvider(new[] {Path.Combine("Manifests", "Sample.json")}),
                    "fakepath", ManifestTests.TEST_RUNTIME_IDENTIFIER_CHAIN);
            workloadResolver.ReplaceFilesystemChecksForTest(fileExists: (_) => true, directoryExists: (_) => false);
            var result = workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Sdk);
            result.Should().HaveCount(0);
        }
    }
}
