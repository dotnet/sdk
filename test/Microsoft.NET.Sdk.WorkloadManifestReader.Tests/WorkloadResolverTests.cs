// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.TestFramework;
using ManifestReaderTests;

namespace Microsoft.NET.Sdk.WorkloadManifestReader.Tests
{
    [TestClass]
    public class WorkloadResolverTests : SdkTest
    {
        private const string fakeRootPath = "fakeRootPath";

        [TestMethod]
        public void GetExtendedWorkloads_SampleDeduplicatedClosureExpected()
        {
            var manifestPath = Path.Combine(TestAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "Sample.json");
            var workloadResolver = WorkloadResolver.CreateForTests(new FakeManifestProvider(manifestPath), fakeRootPath);

            var resultWorkloads = workloadResolver.GetExtendedWorkloads(new List<WorkloadId>()
            {
                new WorkloadId("xamarin-android-build-x86"),
                new WorkloadId("xamarin-empty-mock"),
                new WorkloadId("xamarin-android"),
            }).ToList();

            List<WorkloadResolver.WorkloadInfo> expected = new()
            {
                new(new WorkloadId("xamarin-android-build-x86"), null),
                new(new WorkloadId("xamarin-android-build"), "Build and run Android apps"),
                new(new WorkloadId("xamarin-empty-mock"), "Empty mock workload for testing"),
                new(new WorkloadId("xamarin-android"), "Create, build and run Android apps"),
                new(new WorkloadId("xamarin-android-build-armv7a"), null),
            };

            resultWorkloads.Should().Equal(expected,
                (w1, w2) => w1.Id.Equals(w2.Id) && string.Equals(w1.Description, w2.Description),
                "WorkloadResolver should return expected workload infos based on manifest");
        }

        [TestMethod]
        public void GetExtendedWorkloads_EmptyInputYieldsEmptyOutput()
        {
            var manifestPath = Path.Combine(TestAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "Sample.json");
            var workloadResolver = WorkloadResolver.CreateForTests(new FakeManifestProvider(manifestPath), fakeRootPath);

            var resultWorkloads = workloadResolver.GetExtendedWorkloads(Enumerable.Empty<WorkloadId>()).ToList();

            resultWorkloads.Should().BeEmpty();
        }

        [TestMethod]
        public void GetExtendedWorkloads_ThrowsOnUnknownWorkload()
        {
            var manifestPath = Path.Combine(TestAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "Sample.json");
            var workloadResolver = WorkloadResolver.CreateForTests(new FakeManifestProvider(manifestPath), fakeRootPath);

            Exception exc = Assert.ThrowsExactly<WorkloadManifestCompositionException>(() =>
                workloadResolver.GetExtendedWorkloads(new List<WorkloadId>() { new WorkloadId("BAH"), }).ToList());

            exc.Message.Should().StartWith("Could not find workload 'BAH'");
        }
    }
}
