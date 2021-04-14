﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.ToolPackage;
using Microsoft.NET.TestFramework;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tests
{
    public class NuGetPackageInstallerTests : SdkTest
    {
        public NuGetPackageInstallerTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public async Task It_installs_nuget_package()
        {
            var packageId = "Humanizer";
            var packageVersion = "2.6.2";
            var logger = new NuGetTestLogger();
            var installer = new NuGetPackageDownloader(Directory.GetCurrentDirectory(), logger: logger);
            var packagePath = await installer.DownloadPackageAsync(new PackageId(packageId), new NuGetVersion(packageVersion));

            logger.Errors.Should().Be(0);
            logger.Warnings.Should().Be(0);
            packagePath.Should().ContainEquivalentOf(packageId);
            packagePath.Should().Contain(packageVersion);
            File.Exists(packagePath).Should().BeTrue();
        }

        [Fact]
        public async Task It_extracts_nuget_package()
        {
            var packageId = "Newtonsoft.Json";
            var packageVersion = "12.0.3";
            var logger = new NuGetTestLogger();
            var installer = new NuGetPackageDownloader(Directory.GetCurrentDirectory(), logger: logger);
            var packagePath = await installer.DownloadPackageAsync(new PackageId(packageId), new NuGetVersion(packageVersion));
            var targetPath = Path.Combine(Directory.GetCurrentDirectory(), "ExtractedPackage");
            var result = await installer.ExtractPackageAsync(packagePath, targetPath);

            Directory.Exists(targetPath).Should().BeTrue();
            var extractedFiles = Directory.GetFiles(targetPath, "*", SearchOption.AllDirectories);
            extractedFiles.Should().Contain(Path.Combine(targetPath, $"{packageId}.nuspec"));
            extractedFiles.Should().BeEquivalentTo(result);
        }
    }
}
