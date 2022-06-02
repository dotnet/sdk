// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using System.IO;

namespace Microsoft.DotNet.New.Tests
{
    public class NewCommandTests : SdkTest
    {
        public NewCommandTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void WhenSwitchIsSkippedThenItPrintsError()
        {
            var cmd = new DotnetCommand(Log).Execute("new", "Web1.1", "--debug:ephemeral-hive");

            cmd.ExitCode.Should().NotBe(0);

            if (!TestContext.IsLocalized())
            {
                cmd.StdErr.Should().StartWith("No templates found");
            }
        }

        [Fact]
        public void ItCanCreateTemplate()
        {
            var tempDir = _testAssetsManager.CreateTestDirectory();
            var cmd = new DotnetCommand(Log).Execute("new", "console", "-o", tempDir.Path, "--debug:ephemeral-hive");
            cmd.Should().Pass();
        }

        [Fact]
        public void ItCanShowHelp()
        {
            var cmd = new DotnetCommand(Log).Execute("new", "--help", "--debug:ephemeral-hive");
            cmd.Should().Pass()
                .And.HaveStdOutContaining("Usage:")
                .And.HaveStdOutContaining("dotnet new [command] [options]");
        }

        [Fact]
        public void ItCanShowHelpForTemplate()
        {
            var cmd = new DotnetCommand(Log).Execute("new", "classlib", "--help", "--debug:ephemeral-hive");
            cmd.Should().Pass()
                .And.NotHaveStdOutContaining("Usage: new [options]")
                .And.HaveStdOutContaining("Class Library (C#)")
                .And.HaveStdOutContaining("--framework");
        }

        [Fact]
        public void ItCanShowParseError()
        {
            var cmd = new DotnetCommand(Log).Execute("new", "update", "--bla");
            cmd.Should().ExitWith(127)
                .And.HaveStdErrContaining("Unrecognized command or argument '--bla'")
                .And.HaveStdOutContaining("dotnet new update [options]");
        }

        [Fact]
        public void ItCanInstallRemoteNuGetPackage()
        {
            var cmd = new DotnetCommand(Log).Execute("new", "install", "Microsoft.DotNet.Web.ProjectTemplates.3.0");
            cmd.Should().Pass()
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutMatching($"Success: Microsoft\\.DotNet\\.Web\\.ProjectTemplates\\.3\\.0::([\\d\\.a-z-])+ installed the following templates:")
                .And.HaveStdOutContaining("web")
                .And.HaveStdOutContaining("blazorserver");
        }

        [Fact]
        public void ItCanInstallRemoteNuGetPackageWithVersion()
        {
            var cmd = new DotnetCommand(Log).Execute("new", "install", "Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0");
            cmd.Should().Pass()
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutContaining("Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0")
                .And.HaveStdOutContaining($"Success: Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0 installed the following templates:")
                .And.HaveStdOutContaining("web")
                .And.HaveStdOutContaining("blazorwasm");
        }

        [Fact]
        public void ItCanInstallLocalNuGetPackage()
        {
            var dotnetNewDirectory = _testAssetsManager.CopyTestAsset("dotnet-new", testAssetSubdirectory: "TestPackages")
                            .WithSource();
            var workingDirectory = Path.Combine(dotnetNewDirectory.Path, "nupkg_templates");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(workingDirectory)
                .Execute("new", "install", "TestNupkgInstallTemplate.0.0.1.nupkg");

            cmd.Should().Pass()
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutContaining("TestNupkgInstallTemplate::0.0.1")
                .And.HaveStdOutContaining($"Success: TestNupkgInstallTemplate::0.0.1 installed the following templates:")
                .And.HaveStdOutContaining("nupkginstallv2");
        }

        [Fact(Skip = "https://github.com/dotnet/templating/issues/1971")]
        public void WhenTemplateNameIsNotUniquelyMatchedThenItIndicatesProblemToUser()
        {
            var cmd = new DotnetCommand(Log).Execute("new", "c");

            cmd.ExitCode.Should().NotBe(0);

            if (!TestContext.IsLocalized())
            {
                cmd.StdErr.Should().StartWith("Unable to determine the desired template from the input template name: c.");
            }
        }
    }
}
