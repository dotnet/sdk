// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Transactions;
using System.Threading;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.TemplateEngine.Cli;
using NuGet.Versioning;
using Xunit;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Install.LocalizableStrings;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.NuGetPackageInstaller;
using Microsoft.DotNet.Tests;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Utilities;

namespace Microsoft.DotNet.ToolPackage.Tests
{
    public class NuGetPackageInstallerTests : SdkTest
    {
        [Fact]
        public async void GivenNoFeedInstallFailsWithException()
        {
            var tempDirectory = GetUniqueTempProjectPathEachTest();
            var logger = new NuGetTestLogger();
            var installer = new NuGetPackageInstaller(tempDirectory.ToString(), logger: logger);
            await Assert.ThrowsAsync<NuGetPackageInstallerException>(() =>
                installer.InstallPackageAsync(TestPackageId, new NuGetVersion(TestPackageVersion)));
        }

        [Fact]
        public async void GivenASourceInstallSucceeds()
        {
            var tempDirectory = GetUniqueTempProjectPathEachTest();
            var logger = new NuGetTestLogger();
            var installer = new NuGetPackageInstaller(tempDirectory.ToString(), logger: logger);
            var packagePath = await installer.InstallPackageAsync(
                TestPackageId, 
                new NuGetVersion(TestPackageVersion),
                new string[]{ GetTestLocalFeedPath()});
            File.Exists(packagePath).Should().BeTrue();
        }

        [Fact]
        public void GivenAEmptySourceAndOfflineFeedInstallSucceeds()
        {
        }

        [Fact]
        public void GivenNugetConfigInstallSucceeds()
        {
        }

        [Fact]
        public void GivenAConfigFileRootDirectoryPackageInstallSucceedsViaFindingNugetConfigInParentDir()
        {
        }

        [Fact]
        public void GivenAllButNoPackageVersionItCanInstallThePackage()
        {
        }

        [Fact]
        public void GivenARelativeSourcePathInstallSucceeds()
        {
        }

        [Fact]
        public void GivenAEmptySourceAndNugetConfigInstallSucceeds()
        {
        }

        [UnixOnlyFact]
        public void GivenAPackageWithCasingAndenUSPOSIXInstallSucceeds()
        {
        }

        private static FilePath GetUniqueTempProjectPathEachTest()
        {
            var tempProjectDirectory =
                new DirectoryPath(Path.GetTempPath()).WithSubDirectories(Path.GetRandomFileName());
            var tempProjectPath =
                tempProjectDirectory.WithFile(Path.GetRandomFileName() + ".csproj");
            return tempProjectPath;
        }


        private (IToolPackageStore, IToolPackageStoreQuery, IToolPackageInstaller, IToolPackageUninstaller,
            BufferedReporter, IFileSystem
            ) Setup(
                bool useMock,
                List<MockFeed> feeds = null,
                FilePath? tempProject = null,
                DirectoryPath? offlineFeed = null,
                FilePath? writeLocalFeedToNugetConfig = null,
                [CallerMemberName] string callingMethod = "")
        {
            var root = new DirectoryPath(_testAssetsManager
                .CreateTestDirectory(callingMethod, identifier: useMock.ToString()).Path);
            var reporter = new BufferedReporter();

            IFileSystem fileSystem;
            IToolPackageStore store;
            IToolPackageStoreQuery storeQuery;
            IToolPackageInstaller installer;
            IToolPackageUninstaller uninstaller;

            {
                fileSystem = new FileSystemWrapper();
                WriteNugetConfigFileToPointToTheFeed(fileSystem, writeLocalFeedToNugetConfig);
                var toolPackageStore = new ToolPackageStoreAndQuery(root);
                store = toolPackageStore;
                storeQuery = toolPackageStore;
                installer = new ToolPackageInstaller(
                    store: store,
                    projectRestorer: new Stage2ProjectRestorer(Log, reporter),
                    tempProject: tempProject ?? GetUniqueTempProjectPathEachTest(),
                    offlineFeed: offlineFeed ?? new DirectoryPath("does not exist"));
                uninstaller = new ToolPackageUninstaller(store);
            }

            store.Root.Value.Should().Be(Path.GetFullPath(root.Value));

            return (store, storeQuery, installer, uninstaller, reporter, fileSystem);
        }

        private static void WriteNugetConfigFileToPointToTheFeed(IFileSystem fileSystem, FilePath? filePath)
        {
            if (!filePath.HasValue) return;

            fileSystem.Directory.CreateDirectory(filePath.Value.GetDirectoryPath().Value);

            fileSystem.File.WriteAllText(filePath.Value.Value, FormatNuGetConfig(
                localFeedPath: GetTestLocalFeedPath()));
        }

        public static string FormatNuGetConfig(string localFeedPath)
        {
            const string template = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
<!--To inherit the global NuGet package sources remove the <clear/> line below -->
<clear />
<add key=""Test Source"" value=""{0}"" />
<add key=""dotnet-public"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json"" />
<add key=""myget-legacy"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/myget-legacy/nuget/v3/index.json"" />
</packageSources>
</configuration>";
            return string.Format(template, localFeedPath);
        }

        private static FilePath GenerateRandomNugetConfigFilePath()
        {
            const string nugetConfigName = "nuget.config";
            var tempPathForNugetConfigWithWhiteSpace =
                Path.Combine(Path.GetTempPath(),
                    Path.GetRandomFileName() + " " + Path.GetRandomFileName());

            FilePath nugetConfigFullPath =
                new FilePath(Path.GetFullPath(Path.Combine(tempPathForNugetConfigWithWhiteSpace, nugetConfigName)));
            return nugetConfigFullPath;
        }

        private static string GetTestLocalFeedPath() =>
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestAssetLocalNugetFeed");

        private readonly string _testTargetframework = BundledTargetFramework.GetTargetFrameworkMoniker();
        private const string TestPackageVersion = "1.0.4";
        private static readonly PackageId TestPackageId = new PackageId("global.tool.console.demo");

        public NuGetPackageInstallerTests(ITestOutputHelper log) : base(log)
        {
        }
    }
}
