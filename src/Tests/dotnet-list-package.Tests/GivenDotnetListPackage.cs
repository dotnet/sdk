// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.List.Package.Tests
{
    public class GivenDotnetListPackage : SdkTest
    {
        public GivenDotnetListPackage(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void ItShowsCoreOutputOnMinimalVerbosity()
        {
            var testAssetName = "NewtonSoftDependentProject";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName)
                .WithSource();
            var projectDirectory = testAsset.Path;

            new RestoreCommand(testAsset)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("--verbosity", "quiet")
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("NewtonSoft.Json");
        }

        [Fact]
        public void RequestedAndResolvedVersionsMatch()
        {
            var testAssetName = "TestAppSimple";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName)
                .WithSource();

            var projectDirectory = testAsset.Path;

            var packageName = "Newtonsoft.Json";
            var packageVersion = "9.0.1";
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("add", "package", packageName, "--version", packageVersion);
            cmd.Should().Pass();

            new RestoreCommand(testAsset)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutContainingIgnoreSpaces(packageName+packageVersion+packageVersion);
        }

        [Fact]
        public void ItListsAutoReferencedPackages()
        {
            var testAssetName = "TestAppSimple";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName)
                .WithSource()
                .WithProjectChanges(ChangeTargetFrameworkTo2_1);
            var projectDirectory = testAsset.Path;

            new RestoreCommand(testAsset)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutContainingIgnoreSpaces("Microsoft.NETCore.App(A)")
                .And.HaveStdOutContainingIgnoreSpaces("(A):Auto-referencedpackage");

            void ChangeTargetFrameworkTo2_1(XDocument project)
            {
                project.Descendants()
                       .Single(e => e.Name.LocalName == "TargetFramework")
                       .Value = "netcoreapp2.1";
            }
        }

        [Fact]
        public void ItRunOnSolution()
        {
            var sln = "TestAppWithSlnAndSolutionFolders";
            var testAsset = _testAssetsManager
                .CopyTestAsset(sln)
                .WithSource();
            var projectDirectory = testAsset.Path;

            new RestoreCommand(testAsset, "App.sln")
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutContainingIgnoreSpaces("NewtonSoft.Json");
        }

        [Fact]
        public void AssetsPathExistsButNotRestored()
        {
            var testAsset = "NewtonSoftDependentProject";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.HaveStdErr();
        }

        [Fact]
        public void ItListsTransitivePackage()
        {
            var testAssetName = "NewtonSoftDependentProject";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName)
                .WithSource();
            var projectDirectory = testAsset.Path;

            new RestoreCommand(testAsset)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("System.IO.FileSystem");

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(args:"--include-transitive")
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("System.IO.FileSystem");
        }

        [Theory]
        [InlineData("", "[net451]", null)]
        [InlineData("", "[netcoreapp3.1]", null)]
        [InlineData("--framework netcoreapp3.1 --framework net451", "[net451]", null)]
        [InlineData("--framework netcoreapp3.1 --framework net451", "[netcoreapp3.1]", null)]
        [InlineData("--framework netcoreapp3.1", "[netcoreapp3.1]", "[net451]")]
        [InlineData("--framework net451", "[net451]", "[netcoreapp3.0]")]
        public void ItListsValidFrameworks(string args, string shouldInclude, string shouldntInclude)
        {
            var testAssetName = "MSBuildAppWithMultipleFrameworks";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName)
                .WithSource();
            var projectDirectory = testAsset.Path;

            new RestoreCommand(testAsset)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            if (shouldntInclude == null)
            {
                new ListPackageCommand(Log)
                    .WithWorkingDirectory(projectDirectory)
                    .Execute(args.Split(' ', options: StringSplitOptions.RemoveEmptyEntries))
                    .Should()
                    .Pass()
                    .And.NotHaveStdErr()
                    .And.HaveStdOutContainingIgnoreSpaces(shouldInclude.Replace(" ", ""));
            }
            else
            {
                new ListPackageCommand(Log)
                    .WithWorkingDirectory(projectDirectory)
                    .Execute(args.Split(' ', options: StringSplitOptions.RemoveEmptyEntries))
                    .Should()
                    .Pass()
                    .And.NotHaveStdErr()
                    .And.HaveStdOutContainingIgnoreSpaces(shouldInclude.Replace(" ", ""))
                    .And.NotHaveStdOutContaining(shouldntInclude.Replace(" ", ""));
            }
            
        }

        [Fact]
        public void ItDoesNotAcceptInvalidFramework()
        {
            var testAssetName = "MSBuildAppWithMultipleFrameworks";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName)
                .WithSource();
            var projectDirectory = testAsset.Path;

            new RestoreCommand(testAsset)
                .Execute()
                .Should()
                .Pass();

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("--framework", "invalid")
                .Should()
                .Fail();
        }

        [FullMSBuildOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/12560")]
        public void ItListsFSharpProject()
        {
            var testAssetName = "FSharpTestAppSimple";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName)
                .WithSource();
            var projectDirectory = testAsset.Path;

            new RestoreCommand(testAsset)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();
        }

    }
}
