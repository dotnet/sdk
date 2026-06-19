// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [TestClass]
    public class WebProjectsTests : BaseIntegrationTest
    {
        private ITestOutputHelper _log => Log;
        private static WebProjectsFixture s_fixture = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext ctx)
        {
            s_fixture = new WebProjectsFixture(new TestContextOutputHelper(ctx));
        }

        [ClassCleanup]
        public static void ClassCleanup() => s_fixture?.Dispose();

        private WebProjectsFixture _fixture => s_fixture;

        [TestMethod]
        [DataRow("emptyweb_cs-latest", "web")]
        [DataRow("mvc_cs-latest", "mvc")]
        [DataRow("mvc_fs-latest", "mvc", "-lang", "F#")]
        [DataRow("api_cs-latest", "webapi")]
        [DataRow("emptyweb_cs-60", "web", "-f", "net6.0")]
        [DataRow("mvc_cs-60", "mvc", "-f", "net6.0")]
        [DataRow("mvc_fs-60", "mvc", "-lang", "F#", "-f", "net6.0")]
        [DataRow("api_cs-60", "webapi", "-f", "net6.0")]
        [DataRow("emptyweb_cs-70", "web", "-f", "net7.0")]
        [DataRow("mvc_cs-70", "mvc", "-f", "net7.0")]
        [DataRow("mvc_fs-70", "mvc", "-lang", "F#", "-f", "net7.0")]
        [DataRow("api_cs-70", "webapi", "-f", "net7.0")]
        [DataRow("emptyweb_cs-80", "web", "-f", "net8.0")]
        [DataRow("mvc_cs-80", "mvc", "-f", "net8.0")]
        [DataRow("mvc_fs-80", "mvc", "-lang", "F#", "-f", "net8.0")]
        [DataRow("api_cs-80", "webapi", "-f", "net8.0")]
        [DataRow("emptyweb_cs-90", "web", "-f", "net9.0")]
        [DataRow("mvc_cs-90", "mvc", "-f", "net9.0")]
        [DataRow("mvc_fs-90", "mvc", "-lang", "F#", "-f", "net9.0")]
        [DataRow("api_cs-90", "webapi", "-f", "net9.0")]
        public void AllWebProjectsRestoreAndBuild(string testName, params string[] args)
        {
            string workingDir = Path.Combine(_fixture.BaseWorkingDirectory, testName);
            Directory.CreateDirectory(workingDir);

            new DotnetNewCommand(_log, args)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetRestoreCommand(_log)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetBuildCommand(_log)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            Directory.Delete(workingDir, true);
        }

        [TestMethod]
        public Task CanShowHelp_WebAPI()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "webapi", "-h")
               .WithCustomHive(_fixture.HomeDirectory)
               .WithWorkingDirectory(_fixture.BaseWorkingDirectory)
               .Execute();

            commandResult
               .Should()
               .ExitWith(0)
               .And
               .NotHaveStdErr();

            return Verify(commandResult.StdOut);
        }

        [TestMethod]
        public Task CanShowHelp_Mvc()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "mvc", "-h")
               .WithCustomHive(_fixture.HomeDirectory)
               .WithWorkingDirectory(_fixture.BaseWorkingDirectory)
               .Execute();

            commandResult
               .Should()
               .ExitWith(0)
               .And
               .NotHaveStdErr();

            return Verify(commandResult.StdOut)
                .AddScrubber(output => output.ScrubByRegex("[A-Za-z0-9\\.]+-third-party-notices", "%version%-third-party-notices"));
        }

        [TestMethod]
        [DataRow("webapp")]
        [DataRow("razor")]
        public Task CanShowHelp_Webapp(string templateName)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, templateName, "-h")
               .WithCustomHive(_fixture.HomeDirectory)
               .WithWorkingDirectory(_fixture.BaseWorkingDirectory)
               .Execute();

            commandResult
               .Should()
               .ExitWith(0)
               .And
               .NotHaveStdErr();

            return Verify(commandResult.StdOut)
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix()
                .AddScrubber(output => output.ScrubByRegex("[A-Za-z0-9\\.]+-third-party-notices", "%version%-third-party-notices"));
        }
    }

    public sealed class WebProjectsFixture : SharedHomeDirectory
    {
        public WebProjectsFixture(ITestOutputHelper log) : base(log)
        {
            BaseWorkingDirectory = Utilities.CreateTemporaryFolder(nameof(WebProjectsTests));

            InstallPackage(TemplatePackagesPaths.MicrosoftDotNetWebProjectTemplates60Path, BaseWorkingDirectory);
            InstallPackage(TemplatePackagesPaths.MicrosoftDotNetWebProjectTemplates70Path, BaseWorkingDirectory);
            InstallPackage(TemplatePackagesPaths.MicrosoftDotNetWebProjectTemplates80Path, BaseWorkingDirectory);
            InstallPackage(TemplatePackagesPaths.MicrosoftDotNetWebProjectTemplates90Path, BaseWorkingDirectory);
        }

        internal string BaseWorkingDirectory { get; private set; }
    }
}
