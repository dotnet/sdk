// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using ManifestReaderTests;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using Microsoft.Extensions.EnvironmentAbstractions;
using System.Text.Json;
using Microsoft.TemplateEngine.Edge.Constraints;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    public class WorkloadGarbageCollectionTests : SdkTest
    {
        private readonly BufferedReporter _reporter;
        private string _testDirectory;
        private string _dotnetRoot;

        public WorkloadGarbageCollectionTests(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
        }

        [Fact]
        public void GivenManagedInstallItCanGarbageCollect()
        {
            CreateMockManifest("testmanifest", "1.0.0", "6.0.100", sourceManifestName: @"Sample2.json");
            CreateMockManifest("testmanifest", "2.0.0", "6.0.300", sourceManifestName: @"Sample2_v2.json");

            var (installer, getResolver) = GetTestInstaller();
            var packsToKeep = new PackInfo[]
            {
                CreatePackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Sdk),
                CreatePackInfo("Xamarin.Android.Framework", "8.5.0", WorkloadPackKind.Framework),
                CreatePackInfo("Xamarin.Android.Runtime", "8.5.0.1", WorkloadPackKind.Library)
            };

            var packsToCollect = new PackInfo[]
            {
                CreatePackInfo("Xamarin.Android.Framework", "8.4.0", WorkloadPackKind.Framework),
                CreatePackInfo("Xamarin.Android.Runtime", "8.4.7.4", WorkloadPackKind.Library)
            };
            var sdkVersions = new [] { "6.0.100", "6.0.300" };

            // Write packs
            foreach (var sdkVersion in sdkVersions)
            {
                foreach (var pack in packsToKeep.Concat(packsToCollect))
                {
                    CreateInstalledPack(pack, sdkVersion);
                }
            }

            // Write workload install record for 6.0.300
            var workloadsRecordPath = Path.Combine(_dotnetRoot, "metadata", "workloads", sdkVersions[1], "InstalledWorkloads");
            Directory.CreateDirectory(workloadsRecordPath);
            File.Create(Path.Combine(workloadsRecordPath, "xamarin-android-build"));

            installer.GarbageCollect(getResolver);

            foreach (var pack in packsToCollect)
            {
                PackShouldExist(pack, false);
                PackRecord(pack, "6.0.100").Should().NotExist();
                PackRecord(pack, "6.0.300").Should().NotExist();
            }
            foreach (var pack in packsToKeep)
            {
                PackShouldExist(pack, true);
                PackRecord(pack, "6.0.100").Should().NotExist();
                PackRecord(pack, "6.0.300").Should().Exist();
            }
        }

        [Fact]
        public void GivenManagedInstallItCanGarbageCollectPacksMissingFromManifest()
        {
            CreateMockManifest("testmanifest", "1.0.0");
            var (installer, getResolver) = GetTestInstaller();
            // Define packs that don't show up in the manifest
            var packs = new PackInfo[]
            {
                CreatePackInfo("Xamarin.Android.Sdk.fake", "8.4.7", WorkloadPackKind.Framework),
                CreatePackInfo("Xamarin.Android.Framework.mock", "8.4", WorkloadPackKind.Framework)
            };
            var sdkVersions = new WorkloadId[] { new WorkloadId("6.0.100"), new WorkloadId("6.0.300") };

            // Write fake packs
            var installedPacksPath = Path.Combine(_dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1");
            foreach (var sdkVersion in sdkVersions)
            {
                foreach (var pack in packs)
                {
                    CreateInstalledPack(pack, sdkVersion);
                }
            }

            installer.GarbageCollect(getResolver);

            Directory.EnumerateFileSystemEntries(installedPacksPath)
                .Should()
                .BeEmpty();
            foreach (var pack in packs)
            {
                PackShouldExist(pack, false);
            }
        }

        [Fact]
        public void GarbageCollectManifests()
        {
            //  ARRANGE
            //  Create different versions of a manifest, one with a previous feature band
            CreateMockManifest("testmanifest", "1.0.0", "6.0.100", sourceManifestName: @"Sample2.json");
            CreateMockManifest("testmanifest", "2.0.0", "6.0.300", sourceManifestName: @"Sample2_v2.json");
            CreateMockManifest("testmanifest", "3.0.0", "6.0.300", sourceManifestName: @"Sample2_v3.json");

            //  Create manifest installation records (all for "current" version of SDK: 6.0.300)
            CreateManifestRecord("testmanifest", "1.0.0", "6.0.100", "6.0.300");
            CreateManifestRecord("testmanifest", "2.0.0", "6.0.300", "6.0.300");
            CreateManifestRecord("testmanifest", "3.0.0", "6.0.300", "6.0.300");

            var (installer, getResolver) = GetTestInstaller("6.0.300");

            // Write workload install record for xamarin-android-build workload for 6.0.300
            var workloadsRecordPath = Path.Combine(_dotnetRoot, "metadata", "workloads", "6.0.300", "InstalledWorkloads");
            Directory.CreateDirectory(workloadsRecordPath);
            File.Create(Path.Combine(workloadsRecordPath, "xamarin-android-build"));

            //  These packs are referenced by xamarin-android-build from the 3.0 manifest, which is the latest one and therefore the one that will be kept
            var packsToKeep = new PackInfo[]
            {
                CreatePackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Sdk),
                CreatePackInfo("Xamarin.Android.Framework", "8.6.0", WorkloadPackKind.Framework),
                CreatePackInfo("Xamarin.Android.Runtime", "8.6.0.0", WorkloadPackKind.Library)
            };

            //  These packs are referenced by earlier versions of the manifest, and should be garbage collected
            var packsToCollect = new PackInfo[]
            {
                CreatePackInfo("Xamarin.Android.Framework", "8.4.0", WorkloadPackKind.Framework),
                CreatePackInfo("Xamarin.Android.Runtime", "8.4.7.4", WorkloadPackKind.Library)
            };

            //  Create packs and installation records
            foreach (var pack in packsToKeep.Concat(packsToCollect))
            {
                CreateInstalledPack(pack, "6.0.300");
            }

            //  ACT: garbage collect
            installer.GarbageCollect(getResolver);

            //  ASSERT

            //  Only the latest manifest version and its installation record should be kept
            ManifestRecord("testmanifest", "1.0.0", "6.0.100", "6.0.300").Should().NotExist();
            ManifestRecord("testmanifest", "2.0.0", "6.0.300", "6.0.300").Should().NotExist();
            ManifestRecord("testmanifest", "3.0.0", "6.0.300", "6.0.300").Should().Exist();

            new FileInfo(Path.Combine(_dotnetRoot, "sdk-manifests", "6.0.100", "testmanifest", "1.0.0", "WorkloadManifest.json")).Should().NotExist();
            new FileInfo(Path.Combine(_dotnetRoot, "sdk-manifests", "6.0.300", "testmanifest", "2.0.0", "WorkloadManifest.json")).Should().NotExist();
            new FileInfo(Path.Combine(_dotnetRoot, "sdk-manifests", "6.0.300", "testmanifest", "3.0.0", "WorkloadManifest.json")).Should().Exist();

            //  Packs which should be collected should be removed, as well as their installation records
            foreach (var pack in packsToCollect)
            {
                PackShouldExist(pack, false);
                PackRecord(pack, "6.0.300").Should().NotExist();
            }
            //  Packs which should be kept should still exist, as well as their installation records
            foreach (var pack in packsToKeep)
            {
                PackShouldExist(pack, true);
                PackRecord(pack, "6.0.300").Should().Exist();
            }
        }

        [Fact]
        public void GarbageCollectManifestsWithInstallState()
        {
            //  ARRANGE
            //  Create different versions of a manifest, one with a previous feature band
            CreateMockManifest("testmanifest", "1.0.0", "6.0.100", sourceManifestName: @"Sample2.json");
            CreateMockManifest("testmanifest", "2.0.0", "6.0.300", sourceManifestName: @"Sample2_v2.json");
            CreateMockManifest("testmanifest", "3.0.0", "6.0.300", sourceManifestName: @"Sample2_v3.json");

            //  Create manifest installation records (all for "current" version of SDK: 6.0.300)
            CreateManifestRecord("testmanifest", "1.0.0", "6.0.100", "6.0.300");
            CreateManifestRecord("testmanifest", "2.0.0", "6.0.300", "6.0.300");
            CreateManifestRecord("testmanifest", "3.0.0", "6.0.300", "6.0.300");

            //  Create install state pinning the 2.0.0 version of the manifest
            CreateInstallState("6.0.300",
               """
                {
                    "manifests": {
                        "testmanifest": "2.0.0/6.0.300",
                    }
                }
                """);

            var (installer, getResolver) = GetTestInstaller("6.0.300");

            // Write workload install record for xamarin-android-build workload for 6.0.300
            var workloadsRecordPath = Path.Combine(_dotnetRoot, "metadata", "workloads", "6.0.300", "InstalledWorkloads");
            Directory.CreateDirectory(workloadsRecordPath);
            File.Create(Path.Combine(workloadsRecordPath, "xamarin-android-build"));

            //  These packs are referenced by xamarin-android-build from the 2.0 manifest, which is the one that should be kept due to the install state
            var packsToKeep = new PackInfo[]
            {
                CreatePackInfo("Xamarin.Android.Sdk", "8.4.7", WorkloadPackKind.Sdk),
                CreatePackInfo("Xamarin.Android.Framework", "8.5.0", WorkloadPackKind.Framework),
                CreatePackInfo("Xamarin.Android.Runtime", "8.5.0.1", WorkloadPackKind.Library)
            };

            //  These packs are referenced by version 3.0 of the manifest, and should be garbage collected
            var packsToCollect = new PackInfo[]
            {
                CreatePackInfo("Xamarin.Android.Framework", "8.6.0", WorkloadPackKind.Framework),
                CreatePackInfo("Xamarin.Android.Runtime", "8.6.0.0", WorkloadPackKind.Library)
            };

            //  Create packs and installation records
            foreach (var pack in packsToKeep.Concat(packsToCollect))
            {
                CreateInstalledPack(pack, "6.0.300");
            }

            //  ACT: garbage collect
            installer.GarbageCollect(getResolver);

            //  ASSERT

            //  Only the pinned manifest version (2.0.0) and its installation record should be kept
            ManifestRecord("testmanifest", "1.0.0", "6.0.100", "6.0.300").Should().NotExist();
            ManifestRecord("testmanifest", "2.0.0", "6.0.300", "6.0.300").Should().Exist();
            ManifestRecord("testmanifest", "3.0.0", "6.0.300", "6.0.300").Should().NotExist();

            new FileInfo(Path.Combine(_dotnetRoot, "sdk-manifests", "6.0.100", "testmanifest", "1.0.0", "WorkloadManifest.json")).Should().NotExist();
            new FileInfo(Path.Combine(_dotnetRoot, "sdk-manifests", "6.0.300", "testmanifest", "2.0.0", "WorkloadManifest.json")).Should().Exist();
            new FileInfo(Path.Combine(_dotnetRoot, "sdk-manifests", "6.0.300", "testmanifest", "3.0.0", "WorkloadManifest.json")).Should().NotExist();

            //  Packs which should be collected should be removed, as well as their installation records
            foreach (var pack in packsToCollect)
            {
                PackShouldExist(pack, false);
                PackRecord(pack, "6.0.300").Should().NotExist();
            }
            //  Packs which should be kept should still exist, as well as their installation records
            foreach (var pack in packsToKeep)
            {
                PackShouldExist(pack, true);
                PackRecord(pack, "6.0.300").Should().Exist();
            }

        }

        //  TODO: Additional scenarios to add tests for once workload sets are added:
        //  Garbage collect workload sets
        //  Garbage collect with install state with workload set
        //  Don't garbage collect baseline workload set

        void PackShouldExist(PackInfo pack, bool shouldExist)
        {
            if (pack.Kind == WorkloadPackKind.Library)
            {
                if (shouldExist)
                {
                    new FileInfo(pack.Path).Should().Exist();
                }
                else
                {
                    new FileInfo(pack.Path).Should().NotExist();
                }
            }
            else
            {
                if (shouldExist)
                {
                    new DirectoryInfo(pack.Path).Should().Exist();
                }
                else
                {
                    new DirectoryInfo(pack.Path).Should().NotExist();
                }
            }
        }

        PackInfo CreatePackInfo(string id, string version, WorkloadPackKind kind, string resolvedPackageId = null)
        {
            if (resolvedPackageId == null)
            {
                resolvedPackageId = id;
            }

            string path;
            if (kind == WorkloadPackKind.Library)
            {
                path = Path.Combine(_dotnetRoot, "library-packs", $"{resolvedPackageId}.{version}.nupkg");
            }
            else
            {
                path = Path.Combine(_dotnetRoot, "packs", id, version);
            }

            return new PackInfo(new WorkloadPackId(id), version, kind, path, resolvedPackageId);
        }

        FileInfo PackRecord(PackInfo pack, string sdkFeatureBand)
        {
            var installedPacksPath = Path.Combine(_dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1");
            var packRecordPath = Path.Combine(installedPacksPath, pack.Id, pack.Version, sdkFeatureBand);
            return new FileInfo(packRecordPath);
        }


        private void CreateInstalledPack(PackInfo pack, string sdkFeatureBand)
        {
            //  Write pack installation record
            var packRecordPath = PackRecord(pack, sdkFeatureBand);
            Directory.CreateDirectory(Path.GetDirectoryName(packRecordPath.FullName));
            var packRecordContents = JsonSerializer.Serialize<WorkloadResolver.PackInfo>(pack);
            File.WriteAllText(packRecordPath.FullName, packRecordContents);

            //  Create fake pack install
            if (pack.Kind == WorkloadPackKind.Library)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(pack.Path));
                using var _ = File.Create(pack.Path);
            }
            else
            {
                Directory.CreateDirectory(pack.Path);
            }
        }

        private void CreateInstallState(string featureBand, string installStateContents)
        {
            var installStateFolder = Path.Combine(_dotnetRoot!, "metadata", "workloads", featureBand, "InstallState");
            Directory.CreateDirectory(installStateFolder);

            string installStatePath = Path.Combine(installStateFolder, "default.json");

            File.WriteAllText(installStatePath, installStateContents);
        }

        private void CreateDotnetRoot([CallerMemberName]string testName = "", string identifier = "")
        {
            if (_dotnetRoot == null)
            {
                _testDirectory = _testAssetsManager.CreateTestDirectory(testName, identifier: identifier).Path;
                _dotnetRoot = Path.Combine(_testDirectory, "dotnet");
            }
        }

        private void CreateMockManifest(string manifestId, string manifestVersion, string featureBand = "6.0.300", bool useVersionFolder = true,
            string sourceManifestName = "Sample2.json", [CallerMemberName] string testName = "", string identifier = "")
        {
            CreateDotnetRoot(testName, identifier);

            var manifestDirectory = Path.Combine(_dotnetRoot, "sdk-manifests", featureBand, manifestId);
            if (useVersionFolder)
            {
                manifestDirectory = Path.Combine(manifestDirectory, manifestVersion);
            }

            Directory.CreateDirectory(manifestDirectory);

            string manifestSourcePath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), sourceManifestName);

            File.Copy(manifestSourcePath, Path.Combine(manifestDirectory, "WorkloadManifest.json"));
        }

        private FileInfo ManifestRecord(string manifestId, string manifestVersion, string manifestFeatureBand, string referencingFeatureBand)
        {
            return new FileInfo(Path.Combine(_dotnetRoot, "metadata", "workloads", "InstalledManifests", "v1", manifestId.ToLowerInvariant(), manifestVersion, manifestFeatureBand, referencingFeatureBand));
        }

        private void CreateManifestRecord(string manifestId, string manifestVersion, string manifestFeatureBand, string referencingFeatureBand)
        {
            var path = ManifestRecord(manifestId, manifestVersion, manifestFeatureBand, referencingFeatureBand);
            path.Directory.Create();
            using var _ = path.Create();
        }

        private (FileBasedInstaller, Func<string, IWorkloadResolver>) GetTestInstaller(string sdkVersion = "6.0.300", [CallerMemberName] string testName = "", string identifier = "")
        {
            CreateDotnetRoot(testName, identifier);
            var sdkFeatureBand = new SdkFeatureBand(sdkVersion);

            INuGetPackageDownloader nugetInstaller = new MockNuGetPackageDownloader(_dotnetRoot, manifestDownload: true);

            var manifestProvider = new SdkDirectoryWorkloadManifestProvider(_dotnetRoot, sdkVersion, userProfileDir: null, globalJsonPath: null);

            var workloadResolver = WorkloadResolver.CreateForTests(manifestProvider, _dotnetRoot);
            

            IWorkloadResolver GetResolver(string workloadSetVersion)
            {
                if (workloadSetVersion != null && !sdkFeatureBand.Equals(new SdkFeatureBand(workloadSetVersion)))
                {
                    throw new NotSupportedException("Mock doesn't support creating resolver for different feature bands: " + workloadSetVersion);
                }
                return workloadResolver;
            }

            var installer = new FileBasedInstaller(_reporter, sdkFeatureBand, workloadResolver, userProfileDir: _testDirectory, nugetInstaller, _dotnetRoot, packageSourceLocation: null);

            return (installer, GetResolver);
        }
    }
}
