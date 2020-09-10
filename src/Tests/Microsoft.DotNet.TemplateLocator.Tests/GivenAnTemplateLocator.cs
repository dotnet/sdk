// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework;
using System.Linq;
using Microsoft.DotNet.DotNetSdkResolver;

namespace Microsoft.DotNet.TemplateLocator.Tests
{
    public class GivenAnTemplateLocator : SdkTest
    {
        private readonly TemplateLocator _resolver;
        private readonly string _manifestDirectory;
        private readonly string _fakeDotnetRootDirectory;

        public GivenAnTemplateLocator(ITestOutputHelper logger) : base(logger)
        {
            _resolver = new TemplateLocator(Environment.GetEnvironmentVariable, VSSettings.Ambient, null, null);
            _fakeDotnetRootDirectory =
                Path.Combine(TestContext.Current.TestExecutionDirectory, Path.GetRandomFileName());
            _manifestDirectory = Path.Combine(_fakeDotnetRootDirectory, "sdk-manifests", "5.0.100");
            Directory.CreateDirectory(_manifestDirectory);
        }

        [Fact]
        public void ItShouldReturnListOfTemplates()
        {
            Directory.CreateDirectory(Path.Combine(_manifestDirectory, "Android"));
            File.Copy(Path.Combine("Manifests", "AndroidWorkloadManifest.json"), Path.Combine(_manifestDirectory, "Android", "WorkloadManifest.json"));
            // the nupkg need to exist to be considered installed
            string templatePacksDirectory = Path.Combine(_fakeDotnetRootDirectory, "template-packs");
            Directory.CreateDirectory(templatePacksDirectory);
            string templateNupkgPath = Path.Combine(templatePacksDirectory, "xamarin.android.templates.1.0.3.nupkg");
            File.WriteAllText(templateNupkgPath, "");

            var result = _resolver.GetDotnetSdkTemplatePackages("5.0.102", _fakeDotnetRootDirectory);

            result.First().Path.Should().Be(templateNupkgPath);
            result.First().TemplatePackageId.Should().Be("Xamarin.Android.Templates");
            result.First().TemplateVersion.Should().Be("1.0.3");

            result.Should().HaveCount(1);
        }

        [Fact]
        public void WhenPassEmptyManifestItShouldReturnEmpty()
        {
            File.WriteAllText(Path.Combine(_manifestDirectory, "WorkloadManifest.xml"), emptyManifest);
            var result = _resolver.GetDotnetSdkTemplatePackages("5.0.102", _fakeDotnetRootDirectory);
            result.Should().BeEmpty();
        }

        [Fact]
        public void GivenNoSdkToBondItShouldReturnEmpty()
        {
            File.WriteAllText(Path.Combine(_manifestDirectory, "WorkloadManifest.xml"), fakeManifest);
            var result = _resolver.GetDotnetSdkTemplatePackages("5.1.102", _fakeDotnetRootDirectory);
            result.Should().BeEmpty();
        }

        [Fact]
        public void GivenNoManifestDirectoryItShouldReturnEmpty()
        {
            var fakeDotnetRootDirectory =
                Path.Combine(TestContext.Current.TestExecutionDirectory, Path.GetRandomFileName());
            var result = _resolver.GetDotnetSdkTemplatePackages("5.1.102", fakeDotnetRootDirectory);
            result.Should().BeEmpty();
        }

        private static string fakeManifest = @"
<WorkloadManifest>
  <Workloads>
    <Workload Name=""Xamarin.Android.Workload"">
      <RequiredPack Name=""xamarin.android.workload""/>
      <RequiredPack Name=""xamarin.android.templates""/>
    </Workload>
    <Workload Name=""Xamarin.iOS.Workload"">
      <RequiredPack Name=""xamarin.ios.workload""/>
      <RequiredPack Name=""xamarin.ios.templates""/>
    </Workload>
  </Workloads>
  <WorkloadPacks>
    <Pack Name=""xamarin.android.workload""
          Version=""1.0.1""
          Kind=""sdk"" />
    <Pack Name=""xamarin.android.templates""
          Version=""2.0.1""
          Kind=""Template"" />
    <Pack Name=""xamarin.ios.workload""
          Version=""3.0.1""
          Kind=""sdk"" />
    <Pack Name=""xamarin.ios.templates""
          Version=""4.0.1""
          Kind=""template"" />
  </WorkloadPacks>
</WorkloadManifest>
";

        private static string emptyManifest = @"
<WorkloadManifest>
</WorkloadManifest>
";
    }
}
