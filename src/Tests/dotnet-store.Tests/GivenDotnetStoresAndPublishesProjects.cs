// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Publish.Tests
{
    public class GivenDotnetStoresAndPublishesProjects : SdkTest
    {
        private static string _tfm = "netcoreapp3.0";
        private static string _arch = RuntimeEnvironment.RuntimeArchitecture.ToLowerInvariant();

        public GivenDotnetStoresAndPublishesProjects(ITestOutputHelper log) : base(log)
        {
        }

        [Fact(Skip = "https://github.com/dotnet/cli/issues/12482")]
        public void ItPublishesARunnablePortableApp()
        {
            var testAppName = "NewtonSoftDependentProject";
            var profileProjectName = "NewtonsoftProfile";

            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var testProjectDirectory = testInstance.Path;
            var rid = EnvironmentInfo.GetCompatibleRid();
            var localAssemblyCache = Path.Combine(testProjectDirectory, "localAssemblyCache");
            var intermediateWorkingDirectory = Path.Combine(testProjectDirectory, "workingDirectory");
            var profileProjectPath = _testAssetsManager.CopyTestAsset(profileProjectName).WithSource().Path;
            var profileProject = Path.Combine(profileProjectPath, $"{profileProjectName}.xml");

            new RestoreCommand(Log, testProjectDirectory)
                .Execute()
                .Should().Pass();

            new DotnetCommand(Log, "store",
                    "--manifest", profileProject,
                    "-f", _tfm,
                    "-r", rid,
                    "-o", localAssemblyCache,
                    "-w", intermediateWorkingDirectory)
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            var profileFilter = Path.Combine(localAssemblyCache, _arch, _tfm, "artifact.xml");

            new DotnetPublishCommand(Log,
                    "-f", _tfm,
                    "--manifest", profileFilter)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(testProjectDirectory, "bin", configuration, _tfm, "publish", $"{testAppName}.dll");

            new DotnetCommand(Log)
                .WithEnvironmentVariable("DOTNET_SHARED_STORE", localAssemblyCache)
                .Execute(outputDll)
                .Should().Pass()
                .And.HaveStdOutContaining("{}");
        }

        [Fact]
        public void AppFailsDueToMissingCache()
        {
            var testAppName = "NuGetConfigDependentProject";
            var profileProjectName = "NuGetConfigProfile";
            var targetManifestFileName = "NuGetConfigFilterProfile.xml";

            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var testProjectDirectory = testInstance.Path;
            var profileProjectPath = _testAssetsManager.CopyTestAsset(profileProjectName).WithSource().Path;
            var profileFilter = Path.Combine(profileProjectPath, targetManifestFileName);

            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            new DotnetPublishCommand(Log,
                    "-f", _tfm,
                    "--manifest", profileFilter)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(testProjectDirectory, "bin", configuration, _tfm, "publish", $"{testAppName}.dll");

            new DotnetCommand(Log)
                .Execute(outputDll)
                .Should().Fail()
                .And.HaveStdErrContaining($"Error:{Environment.NewLine}" +
                    $"  An assembly specified in the application dependencies manifest (NuGetConfigDependentProject.deps.json) was not found:{Environment.NewLine}" +
                    $"    package: 'NuGet.Configuration', version: '4.3.0'{Environment.NewLine}" +
                    "    path: 'lib/netstandard1.3/NuGet.Configuration.dll'");
        }

        //  Windows only for now due to https://github.com/dotnet/cli/issues/7501
        [WindowsOnlyFact(Skip = "https://github.com/dotnet/cli/issues/12482")]
        public void ItPublishesAnAppWithMultipleProfiles()
        {
            var testAppName = "MultiDependentProject";
            var profileProjectName = "NewtonsoftProfile";
            var profileProjectName1 = "FluentProfile";

            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var testProjectDirectory = testInstance.Path;
            var rid = EnvironmentInfo.GetCompatibleRid();
            var localAssemblyCache = Path.Combine(testProjectDirectory, "lAC");
            var intermediateWorkingDirectory = Path.Combine(testProjectDirectory, "workingDirectory");

            var profileProjectPath = _testAssetsManager.CopyTestAsset(profileProjectName).WithSource().Path;
            var profileProject = Path.Combine(profileProjectPath, $"{profileProjectName}.xml");
            var profileFilter = Path.Combine(profileProjectPath, "NewtonsoftFilterProfile.xml");

            var profileProjectPath1 = _testAssetsManager.CopyTestAsset(profileProjectName1).WithSource().Path; 
            var profileProject1 = Path.Combine(profileProjectPath1, $"{profileProjectName1}.xml");
            var profileFilter1 = Path.Combine(profileProjectPath1, "FluentFilterProfile.xml");

            new RestoreCommand(Log, testProjectDirectory)
                .Execute()
                .Should().Pass();

            new DotnetCommand(Log, "store",
                    "--manifest", profileProject,
                    "--manifest", profileProject1,
                    "-f", _tfm,
                    "-r", rid,
                    "-o", localAssemblyCache,
                    "-w", intermediateWorkingDirectory)
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            new DotnetPublishCommand(Log,
                    "-f", _tfm,
                    "--manifest", profileFilter,
                    "--manifest", profileFilter1)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(testProjectDirectory, "bin", configuration, _tfm, "publish", $"{testAppName}.dll");

            new DotnetCommand(Log)
                .WithEnvironmentVariable("DOTNET_SHARED_STORE", localAssemblyCache)
                .Execute(outputDll)
                .Should().Pass()
                .And.HaveStdOutContaining("{}");
        }
    }
}
