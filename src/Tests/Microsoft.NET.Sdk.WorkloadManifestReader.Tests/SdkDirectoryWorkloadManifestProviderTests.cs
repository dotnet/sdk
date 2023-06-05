// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.NET.TestFramework;

using Xunit;
using Xunit.Abstractions;

namespace ManifestReaderTests
{

    public class SdkDirectoryWorkloadManifestProviderTests : SdkTest
    {
        private string? _testDirectory;
        private string? _manifestRoot;
        private string? _manifestVersionBandDirectory;
        private string? _fakeDotnetRootDirectory;

        public SdkDirectoryWorkloadManifestProviderTests(ITestOutputHelper logger) : base(logger)
        {
        }

        [MemberNotNull("_testDirectory", "_manifestRoot", "_manifestVersionBandDirectory", "_fakeDotnetRootDirectory")]
        void Initialize(string featureBand = "5.0.100", [CallerMemberName] string? testName = null, string? identifier = null)
        {
            _testDirectory = _testAssetsManager.CreateTestDirectory(testName, identifier).Path;
            _fakeDotnetRootDirectory = Path.Combine(_testDirectory, "dotnet");
            _manifestRoot = Path.Combine(_fakeDotnetRootDirectory, "sdk-manifests");
            _manifestVersionBandDirectory = Path.Combine(_manifestRoot, featureBand);
            Directory.CreateDirectory(_manifestVersionBandDirectory);
        }

        [Fact]
        public void ItShouldReturnListOfManifestFiles()
        {
            Initialize();

            string androidManifestFileContent = "Android";
            string iosManifestFileContent = "iOS";
            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "Android", "WorkloadManifest.json"), androidManifestFileContent);
            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "iOS"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "iOS", "WorkloadManifest.json"), iosManifestFileContent);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo(iosManifestFileContent, androidManifestFileContent);
        }

        [Fact]
        public void GivenSDKVersionItShouldReturnListOfManifestFilesForThisVersionBand()
        {
            Initialize();

            string androidManifestFileContent = "Android";
            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "Android", "WorkloadManifest.json"), androidManifestFileContent);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo(androidManifestFileContent);
        }

        [Fact]
        public void GivenNoManifestDirectoryItShouldReturnEmpty()
        {
            Initialize();

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null);
            sdkDirectoryWorkloadManifestProvider.GetManifests().Should().BeEmpty();
        }

        [Fact]
        public void GivenNoManifestJsonFileInDirectoryItShouldIgnoreIt()
        {
            Initialize();

            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "Android"));

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null);

            sdkDirectoryWorkloadManifestProvider.GetManifests()
                .Should()
                .BeEmpty();
        }

        [Fact]
        public void ItReturnsLatestManifestVersion()
        {
            Initialize();

            CreateMockManifest(_manifestRoot, "5.0.100-preview.5", "ios", "11.0.3", true);

            CreateMockManifest(_manifestRoot, "5.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "5.0.100", "ios", "11.0.2", true);
            CreateMockManifest(_manifestRoot, "5.0.100", "ios", "11.0.2-rc.1", true);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 11.0.2/5.0.100");
        }

        [Fact]
        public void ItPrefersManifestsInSubfolders()
        {
            Initialize();

            CreateMockManifest(_manifestRoot, "5.0.100", "ios", "11.0.1", true);
            CreateMockManifest(_manifestRoot, "5.0.100", "ios", "11.0.2", true);

            //  Even though this manifest has a higher version, it is not in a versioned subfolder so the other manifests will be preferred
            //  In real use, we would expect the manifest outside the versioned subfolders to be a lower version
            CreateMockManifest(_manifestRoot, "5.0.100", "ios", "12.0.1", false);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("ios: 11.0.2/5.0.100");
        }

        [Fact]
        public void ItFallsBackToLatestManifestVersion()
        {
            Initialize("8.0.200");

            var knownWorkloadsFilePath = Path.Combine(_fakeDotnetRootDirectory, "sdk", "8.0.100", "IncludedWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, "android\nios");

            CreateMockManifest(_manifestRoot, "8.0.100", "android", "33.0.0", true);
            CreateMockManifest(_manifestRoot, "8.0.100", "android", "33.0.1", true);

            CreateMockManifest(_manifestRoot, "7.0.400", "android", "32.0.1", true);

            CreateMockManifest(_manifestRoot, "7.0.400", "ios", "17.0.1", true);
            CreateMockManifest(_manifestRoot, "7.0.400", "ios", "18.0.1", true);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "8.0.100", userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("android: 33.0.1/8.0.100",
                                "ios: 18.0.1/7.0.400");
        }

        [Fact]
        public void ItShouldReturnManifestsFromTestHook()
        {
            Initialize();

            string sdkVersion = "5.0.100";

            var additionalManifestDirectory = Path.Combine(_testDirectory, "AdditionalManifests");
            Directory.CreateDirectory(additionalManifestDirectory);

            var environmentMock = new EnvironmentMock();
            environmentMock.Add(EnvironmentVariableNames.WORKLOAD_MANIFEST_ROOTS, additionalManifestDirectory);

            //  Manifest in test hook directory
            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory, sdkVersion, "Android"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory, sdkVersion, "Android", "WorkloadManifest.json"), "AndroidContent");

            //  Manifest in default directory
            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "iOS"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "iOS", "WorkloadManifest.json"), "iOSContent");


            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: sdkVersion, environmentMock.GetEnvironmentVariable, userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("AndroidContent", "iOSContent");
        }

        [Fact]
        public void ManifestFromTestHookShouldOverrideDefault()
        {
            Initialize();

            string sdkVersion = "5.0.100";

            var additionalManifestDirectory = Path.Combine(_testDirectory, "AdditionalManifests");
            Directory.CreateDirectory(additionalManifestDirectory);

            var environmentMock = new EnvironmentMock();
            environmentMock.Add(EnvironmentVariableNames.WORKLOAD_MANIFEST_ROOTS, additionalManifestDirectory);

            //  Manifest in test hook directory
            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory, sdkVersion, "Android"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory, sdkVersion, "Android", "WorkloadManifest.json"), "OverridingAndroidContent");

            //  Manifest in default directory
            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "Android", "WorkloadManifest.json"), "OverriddenAndroidContent");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: sdkVersion, environmentMock.GetEnvironmentVariable, userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("OverridingAndroidContent");

        }

        [Fact]
        public void ItSupportsMultipleTestHookFolders()
        {
            Initialize();

            string sdkVersion = "5.0.100";

            var additionalManifestDirectory1 = Path.Combine(_testDirectory, "AdditionalManifests1");
            Directory.CreateDirectory(additionalManifestDirectory1);
            var additionalManifestDirectory2 = Path.Combine(_testDirectory, "AdditionalManifests2");
            Directory.CreateDirectory(additionalManifestDirectory2);

            var environmentMock = new EnvironmentMock();
            environmentMock.Add(EnvironmentVariableNames.WORKLOAD_MANIFEST_ROOTS, additionalManifestDirectory1 + Path.PathSeparator + additionalManifestDirectory2);


            //  Manifests in default directory
            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "iOS"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "iOS", "WorkloadManifest.json"), "iOSContent");

            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "Android", "WorkloadManifest.json"), "DefaultAndroidContent");

            //  Manifests in first additional directory
            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory1, sdkVersion, "Android"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory1, sdkVersion, "Android", "WorkloadManifest.json"), "AndroidContent1");

            //  Manifests in second additional directory
            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory2, sdkVersion, "Android"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory2, sdkVersion, "Android", "WorkloadManifest.json"), "AndroidContent2");

            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory2, sdkVersion, "Test"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory2, sdkVersion, "Test", "WorkloadManifest.json"), "TestContent2");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: sdkVersion, environmentMock.GetEnvironmentVariable, userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("AndroidContent1", "iOSContent", "TestContent2");
         
        }

        [Fact]
        public void IfTestHookFolderDoesNotExistItShouldBeIgnored()
        {
            Initialize();

            var additionalManifestDirectory = Path.Combine(_testDirectory, "AdditionalManifests");
                
            var environmentMock = new EnvironmentMock();
            environmentMock.Add(EnvironmentVariableNames.WORKLOAD_MANIFEST_ROOTS, additionalManifestDirectory);

            //  Manifest in default directory
            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "Android", "WorkloadManifest.json"), "AndroidContent");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", environmentMock.GetEnvironmentVariable, userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("AndroidContent");
         
        }

        [Fact]
        public void ItShouldIgnoreOutdatedManifestIds()
        {
            Initialize();

            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "iOS"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "iOS", "WorkloadManifest.json"), "iOSContent");
            Directory.CreateDirectory(Path.Combine(_manifestVersionBandDirectory, "Microsoft.NET.Workload.Android"));
            File.WriteAllText(Path.Combine(_manifestVersionBandDirectory, "Microsoft.NET.Workload.Android", "WorkloadManifest.json"), "iOSContent");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("iOSContent");
        }

        [Fact]
        public void ItShouldFallbackWhenFeatureBandHasNoManifests()
        {
            Initialize("6.0.100");

            // Write 4.0.100 manifests-> android only
            CreateMockManifest(_manifestRoot, "4.0.100", "Android", "1");

            // Write 5.0.100 manifests-> ios and android
            CreateMockManifest(_manifestRoot, "5.0.100", "Android", "2");
            CreateMockManifest(_manifestRoot, "5.0.100", "iOS", "3");

            // Write 6.0.100 manifests-> ios only
            CreateMockManifest(_manifestRoot, "6.0.100", "iOS", "4");

            // Write 7.0.100 manifests-> ios and android
            CreateMockManifest(_manifestRoot, "7.0.100", "Android", "5");
            CreateMockManifest(_manifestRoot, "7.0.100", "iOS", "6");

            var knownWorkloadsFilePath = Path.Combine(_fakeDotnetRootDirectory, "sdk", "6.0.100", "IncludedWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, "Android\niOS");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "6.0.100", userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("iOS: 4/6.0.100", "Android: 2/5.0.100");
        }

        [Fact]
        public void ItShouldFallbackWhenPreviewFeatureBandHasNoManifests()
        {
            Initialize("6.0.100");

            // Write 4.0.100 manifests-> android only
            CreateMockManifest(_manifestRoot, "4.0.100", "iOS", "1");

            // Write 5.0.100 manifests-> android
            CreateMockManifest(_manifestRoot, "5.0.100", "Android", "2");

            // Write 6.0.100-preview.2 manifests-> ios only
            CreateMockManifest(_manifestRoot, "6.0.100-preview.2", "iOS", "3");

            // Write 7.0.100 manifests-> ios and android
            CreateMockManifest(_manifestRoot, "7.0.100", "Android", "4");
            CreateMockManifest(_manifestRoot, "7.0.100", "iOS", " 5");

            var prev4Version = "6.0.100-preview.4.12345";
            var knownWorkloadsFilePath = Path.Combine(_fakeDotnetRootDirectory, "sdk", prev4Version, "IncludedWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, "Android\niOS");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: prev4Version, userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("iOS: 3/6.0.100-preview.2", "Android: 2/5.0.100");
        }

        [Fact]
        public void ItShouldRollForwardToNonPrereleaseWhenPreviewFeatureBandHasNoManifests()
        {
            Initialize("6.0.100");

            // Write 4.0.100 manifests-> android only
            CreateMockManifest(_manifestRoot, "4.0.100", "Android", "1");

            // Write 5.0.100 manifests-> ios and android
            CreateMockManifest(_manifestRoot, "5.0.100", "Android", "2");
            CreateMockManifest(_manifestRoot, "5.0.100", "iOS", "3");

            // Write 6.0.100-preview.4 manifests-> ios only
            CreateMockManifest(_manifestRoot, "6.0.100-preview.4", "iOS", "4");

            // Write 6.0.100 manifests-> android
            CreateMockManifest(_manifestRoot, "6.0.100", "Android", "5");

            var prev4Version = "6.0.100-preview.4.12345";
            var knownWorkloadsFilePath = Path.Combine(_fakeDotnetRootDirectory, "sdk", prev4Version, "IncludedWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, "Android\niOS");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: prev4Version, userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("iOS: 4/6.0.100-preview.4", "Android: 5/6.0.100");
        }

        [Fact]
        public void ItReturnsManifestsInOrderFromKnownWorkloadManifestsFile()
        {
            //  microsoft.net.workload.mono.toolchain.net6, microsoft.net.workload.mono.toolchain.net7, microsoft.net.workload.emscripten.net6, microsoft.net.workload.emscripten.net7

            var currentSdkVersion = "7.0.100";
            var fallbackWorkloadBand = "7.0.100-rc.2";

            Initialize(currentSdkVersion);

            CreateMockManifest(_manifestRoot, currentSdkVersion, "NotInIncudedWorkloadsFile", "1");
            CreateMockManifest(_manifestRoot, currentSdkVersion, "Microsoft.Net.Workload.Mono.Toolchain.net6", "2");
            CreateMockManifest(_manifestRoot, fallbackWorkloadBand, "Microsoft.Net.Workload.Mono.Toolchain.net7", "3");
            CreateMockManifest(_manifestRoot, fallbackWorkloadBand, "Microsoft.Net.Workload.Emscripten.net6", "4");
            CreateMockManifest(_manifestRoot, currentSdkVersion, "Microsoft.Net.Workload.Emscripten.net7", "5");

            var knownWorkloadsFilePath = Path.Combine(_fakeDotnetRootDirectory, "sdk", currentSdkVersion, "IncludedWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, @"
Microsoft.Net.Workload.Mono.Toolchain.net6
Microsoft.Net.Workload.Mono.Toolchain.net7
Microsoft.Net.Workload.Emscripten.net6
Microsoft.Net.Workload.Emscripten.net7"
                .Trim());

            var sdkDirectoryWorkloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: currentSdkVersion, userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .Equal($"Microsoft.Net.Workload.Mono.Toolchain.net6: 2/{currentSdkVersion}",
                       $"Microsoft.Net.Workload.Mono.Toolchain.net7: 3/{fallbackWorkloadBand}",
                       $"Microsoft.Net.Workload.Emscripten.net6: 4/{fallbackWorkloadBand}",
                       $"Microsoft.Net.Workload.Emscripten.net7: 5/{currentSdkVersion}",
                       $"NotInIncudedWorkloadsFile: 1/{currentSdkVersion}");
        }

        private void CreateMockManifest(string manifestRoot, string featureBand, string manifestId, string manifestVersion, bool useVersionFolder = false)
        {
            var manifestDirectory = Path.Combine(manifestRoot, featureBand, manifestId);
            if (useVersionFolder)
            {
                manifestDirectory = Path.Combine(manifestDirectory, manifestVersion);
            }

            if (!Directory.Exists(manifestDirectory))
            {
                Directory.CreateDirectory(manifestDirectory);
            }

            File.WriteAllText(Path.Combine(manifestDirectory, "WorkloadManifest.json"), $"{manifestId}: {manifestVersion}/{featureBand}");
        }

        [Fact]
        public void ItShouldIgnoreManifestsNotFoundInFallback()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var fakeDotnetRootDirectory = Path.Combine(testDirectory, "dotnet");

            // Write 6.0.100 manifests-> ios only
            var manifestDirectory6 = Path.Combine(fakeDotnetRootDirectory, "sdk-manifests", "6.0.100");
            Directory.CreateDirectory(manifestDirectory6);
            Directory.CreateDirectory(Path.Combine(manifestDirectory6, "iOS"));
            File.WriteAllText(Path.Combine(manifestDirectory6, "iOS", "WorkloadManifest.json"), "iOS-6.0.100");

            var knownWorkloadsFilePath = Path.Combine(fakeDotnetRootDirectory, "sdk", "6.0.100", "KnownWorkloadManifests.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(knownWorkloadsFilePath)!);
            File.WriteAllText(knownWorkloadsFilePath, "Android\niOS");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: fakeDotnetRootDirectory, sdkVersion: "6.0.100", userProfileDir: null);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("iOS-6.0.100");

        }

        private IEnumerable<string> GetManifestContents(SdkDirectoryWorkloadManifestProvider manifestProvider)
        {
            return manifestProvider.GetManifests().Select(manifest => new StreamReader(manifest.OpenManifestStream()).ReadToEnd());
        }

        private class EnvironmentMock
        {
            Dictionary<string, string> _mockedEnvironmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public void Add(string variable, string value)
            {
                _mockedEnvironmentVariables[variable] = value;
            }

            public string? GetEnvironmentVariable(string variable)
            {
                if (_mockedEnvironmentVariables.TryGetValue(variable, out string? value))
                {
                    return value;
                }
                return Environment.GetEnvironmentVariable(variable);
            }
        }
    }
}

#if NETFRAMEWORK
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    sealed class MemberNotNullAttribute : Attribute
    {
        public MemberNotNullAttribute(params string[] members)
        {
            Members = members;
        }

        public MemberNotNullAttribute(string member)
        {
            Members = new[] { member };
        }

        public string[] Members { get; }
    }
}
#endif
