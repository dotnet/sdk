// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework;
using System.Linq;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ManifestReaderTests
{

    public class SdkDirectoryWorkloadManifestProviderTests : SdkTest
    {
        private string _testDirectory;
        private string _manifestDirectory;
        private string _fakeDotnetRootDirectory;

        public SdkDirectoryWorkloadManifestProviderTests(ITestOutputHelper logger) : base(logger)
        {
        }

        void Initialize([CallerMemberName] string testName = null, string identifier = null)
        {
            _testDirectory = _testAssetsManager.CreateTestDirectory(testName, identifier).Path;
            _fakeDotnetRootDirectory = Path.Combine(_testDirectory, "dotnet");
            _manifestDirectory = Path.Combine(_fakeDotnetRootDirectory, "sdk-manifests", "5.0.100");
            Directory.CreateDirectory(_manifestDirectory);
        }

        [Fact]
        public void ItShouldReturnListOfManifestFiles()
        {
            Initialize();

            string androidManifestFileContent = "Android";
            string iosManifestFileContent = "iOS";
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "Android", "WorkloadManifest.json"), androidManifestFileContent);
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "iOS"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "iOS", "WorkloadManifest.json"), iosManifestFileContent);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100");

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo(iosManifestFileContent, androidManifestFileContent);
        }

        [Fact]
        public void GivenSDKVersionItShouldReturnListOfManifestFilesForThisVersionBand()
        {
            Initialize();

            string androidManifestFileContent = "Android";
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "Android", "WorkloadManifest.json"), androidManifestFileContent);

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.105");

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo(androidManifestFileContent);
        }

        [Fact]
        public void GivenNoManifestDirectoryItShouldReturnEmpty()
        {
            Initialize();

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.105");
            sdkDirectoryWorkloadManifestProvider.GetManifests().Should().BeEmpty();
        }

        [Fact]
        public void GivenNoManifestJsonFileInDirectoryItShouldThrow()
        {
            Initialize();

            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "Android"));

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.105");

            Action a = () => sdkDirectoryWorkloadManifestProvider.GetManifests().ToList();
            a.ShouldThrow<FileNotFoundException>();
        }

        [Fact]
        public void ItShouldReturnManifestsFromTestHook()
        {
            Initialize();

            var additionalManifestDirectory = Path.Combine(_testDirectory, "AdditionalManifests");
            Directory.CreateDirectory(additionalManifestDirectory);

            var environmentMock = new EnvironmentMock();
            environmentMock.Add("DOTNETSDK_WORKLOAD_MANIFEST_ROOTS", additionalManifestDirectory);

            //  Manifest in test hook directory
            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory, "Android"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory, "Android", "WorkloadManifest.json"), "AndroidContent");

            //  Manifest in default directory
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "iOS"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "iOS", "WorkloadManifest.json"), "iOSContent");


            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", environmentMock.GetEnvironmentVariable);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("AndroidContent", "iOSContent");
        }

        [Fact]
        public void ManifestFromTestHookShouldOverrideDefault()
        {
            Initialize();

            var additionalManifestDirectory = Path.Combine(_testDirectory, "AdditionalManifests");
            Directory.CreateDirectory(additionalManifestDirectory);

            var environmentMock = new EnvironmentMock();
            environmentMock.Add("DOTNETSDK_WORKLOAD_MANIFEST_ROOTS", additionalManifestDirectory);

            //  Manifest in test hook directory
            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory, "Android"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory, "Android", "WorkloadManifest.json"), "OverridingAndroidContent");

            //  Manifest in default directory
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "Android", "WorkloadManifest.json"), "OverriddenAndroidContent");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", environmentMock.GetEnvironmentVariable);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("OverridingAndroidContent");

        }

        [Fact]
        public void ItSupportsMultipleTestHookFolders()
        {
            Initialize();

            var additionalManifestDirectory1 = Path.Combine(_testDirectory, "AdditionalManifests1");
            Directory.CreateDirectory(additionalManifestDirectory1);
            var additionalManifestDirectory2 = Path.Combine(_testDirectory, "AdditionalManifests2");
            Directory.CreateDirectory(additionalManifestDirectory2);

            var environmentMock = new EnvironmentMock();
            environmentMock.Add("DOTNETSDK_WORKLOAD_MANIFEST_ROOTS", additionalManifestDirectory1 + Path.PathSeparator + additionalManifestDirectory2);


            //  Manifests in default directory
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "iOS"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "iOS", "WorkloadManifest.json"), "iOSContent");

            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "Android", "WorkloadManifest.json"), "DefaultAndroidContent");

            //  Manifests in first additional directory
            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory1, "Android"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory1, "Android", "WorkloadManifest.json"), "AndroidContent1");

            //  Manifests in second additional directory
            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory2, "Android"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory2, "Android", "WorkloadManifest.json"), "AndroidContent2");

            Directory.CreateDirectory(Path.Combine(additionalManifestDirectory2, "Test"));
            File.WriteAllText(Path.Combine(additionalManifestDirectory2, "Test", "WorkloadManifest.json"), "TestContent2");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", environmentMock.GetEnvironmentVariable);

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
            environmentMock.Add("DOTNETSDK_WORKLOAD_MANIFEST_ROOTS", additionalManifestDirectory);

            //  Manifest in default directory
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "Android"));
            File.WriteAllText(Path.Combine(_manifestDirectory, "Android", "WorkloadManifest.json"), "AndroidContent");

            var sdkDirectoryWorkloadManifestProvider
                = new SdkDirectoryWorkloadManifestProvider(sdkRootPath: _fakeDotnetRootDirectory, sdkVersion: "5.0.100", environmentMock.GetEnvironmentVariable);

            GetManifestContents(sdkDirectoryWorkloadManifestProvider)
                .Should()
                .BeEquivalentTo("AndroidContent");
         
        }

        private IEnumerable<string> GetManifestContents(SdkDirectoryWorkloadManifestProvider manifestProvider)
        {
            return manifestProvider.GetManifests().Select(stream => new StreamReader(stream).ReadToEnd());
        }

        private class EnvironmentMock
        {
            Dictionary<string, string> _mockedEnvironmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public void Add(string variable, string value)
            {
                _mockedEnvironmentVariables[variable] = value;
            }

            public string GetEnvironmentVariable(string variable)
            {
                if (_mockedEnvironmentVariables.TryGetValue(variable, out string value))
                {
                    return value;
                }
                return Environment.GetEnvironmentVariable(variable);
            }
        }
    }
}
