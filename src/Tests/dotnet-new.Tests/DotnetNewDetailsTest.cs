// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public class DotnetNewDetailsTest : BaseIntegrationTest, IClassFixture<DiagnosticFixture>
    {
        private readonly ITestOutputHelper _log;
        private readonly IMessageSink _messageSink;

        public DotnetNewDetailsTest(DiagnosticFixture diagnosisFixture, ITestOutputHelper log) : base(log)
        {
            _log = log;
            _messageSink = diagnosisFixture.DiagnosticSink;
        }

        [Fact]
        public void CanDisplayDetails_RemotePackage()
        {
            new DotnetNewCommand(_log, "details", "Microsoft.DotNet.Web.ProjectTemplates.5.0")
            .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"Microsoft.DotNet.Web.ProjectTemplates.5.0{Environment.NewLine}   Details:");
        }

        [Fact]
        public void CanDisplayDetails_RemotePackageWithVersion()
        {
            new DotnetNewCommand(_log, "details", "Microsoft.DotNet.Web.ItemTemplates", "--version", "2.2.0")
            .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"Microsoft.DotNet.Web.ItemTemplates.2.2.0{Environment.NewLine}   Details:  Web File Templates for Microsoft Template Engine");
        }

        [Fact]
        public void CanDisplayDetails_LocalPackage()
        {
            string packageLocation = PackTestNuGetPackage(_log);
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "install", packageLocation)
                .WithoutBuiltInTemplates().WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            new DotnetNewCommand(_log, "details", "Microsoft.TemplateEngine.TestTemplates")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutMatching($"Microsoft.TemplateEngine.TestTemplates{Environment.NewLine}   Authors:{Environment.NewLine}      Microsoft{Environment.NewLine}   Templates:");
        }

        [Fact]
        public void CannotDisplayUnknownPackageDetails()
        {
            new DotnetNewCommand(_log, "details", "Some package that does not exist")
            .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(103)
                .And.HaveStdErr()
                .And.HaveStdOutMatching("No template packages found matching: Some package that does not exist.");
        }
    }
}
