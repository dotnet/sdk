// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.TemplateEngine.CommandUtils;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.Authoring.Tasks.IntegrationTests
{
    public class ValidateTemplatesTests : TestBase
    {
        private readonly ITestOutputHelper _log;

        public ValidateTemplatesTests(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void CanRunValidateTask_OnError()
        {
            string tmpDir = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy("Resources/InvalidTemplatePackage_MissingName", tmpDir, true);
            TestUtils.SetupNuGetConfigForPackagesLocation(tmpDir, ShippingPackagesLocation);

            new DotnetCommand(_log, "add", "TemplatePackage.csproj", "package", "Microsoft.TemplateEngine.Authoring.Tasks", "--prerelease")
                .WithoutTelemetry()
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand(_log, "build")
                .WithoutTelemetry()
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("Template configuration error MV002: Missing 'name'.")
                .And.HaveStdOutContaining("Template configuration error MV003: Missing 'shortName'.");
        }

        [Fact]
        public void CanRunValidateTask_OnInfo()
        {
            string tmpDir = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy("Resources/InvalidTemplatePackage_MissingOptionalData", tmpDir, true);
            TestUtils.SetupNuGetConfigForPackagesLocation(tmpDir, ShippingPackagesLocation);

            new DotnetCommand(_log, "add", "TemplatePackage.csproj", "package", "Microsoft.TemplateEngine.Authoring.Tasks", "--prerelease")
                .WithoutTelemetry()
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand(_log, "build")
                .WithoutTelemetry()
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining("Template configuration message MV006: Missing 'author'.")
                .And.HaveStdOutContaining("Template configuration message MV010: Missing 'classifications'.");
        }
    }
}
