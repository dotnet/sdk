// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.CommandUtils;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;

namespace Microsoft.TemplateEngine.Authoring.Tasks.IntegrationTests
{
    [TestClass]
    public class ValidateTemplatesTests : TestBase
    {
        public TestContext TestContext { get; set; } = null!;

        private ILogger Log => new TestContextLogger(TestContext);

        [TestMethod]
        public void CanRunValidateTask_OnError()
        {
            string tmpDir = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy("Resources/InvalidTemplatePackage_MissingName", tmpDir, true);
            SetupNuGetConfigForPackagesLocation(tmpDir);

            new DotnetCommand(Log, "add", "TemplatePackage.csproj", "package", "Microsoft.TemplateEngine.Authoring.Tasks", "--prerelease")
                .WithoutTelemetry()
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand(Log, "build")
                .WithoutTelemetry()
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("Template configuration error MV002: Missing 'name'.")
                .And.HaveStdOutContaining("Template configuration error MV003: Missing 'shortName'.");
        }

        [TestMethod]
        public void CanRunValidateTask_OnInfo()
        {
            string tmpDir = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy("Resources/InvalidTemplatePackage_MissingOptionalData", tmpDir, true);
            SetupNuGetConfigForPackagesLocation(tmpDir);

            new DotnetCommand(Log, "add", "TemplatePackage.csproj", "package", "Microsoft.TemplateEngine.Authoring.Tasks", "--prerelease")
                .WithoutTelemetry()
                .WithWorkingDirectory(tmpDir)
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand(Log, "build")
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
