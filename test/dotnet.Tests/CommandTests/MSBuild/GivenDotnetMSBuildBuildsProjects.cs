// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [TestClass]
    public class GivenDotnetMSBuildBuildsProjects : SdkTest
    {

        public GivenDotnetMSBuildBuildsProjects()
        {
        }

        [TestMethod]
        public void ItRunsSpecifiedTargetsWithPropertiesCorrectly()
        {
            var testInstance = TestAssetsManager.CopyTestAsset("MSBuildBareBonesProject")
                                        .WithSource();

            var testProjectDirectory = testInstance.Path;

            new MSBuildCommand(Log, "SayHello", testProjectDirectory)
                .ExecuteWithoutRestore()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello, from MSBuild!");

            new MSBuildCommand(Log, "SayGoodbye", testProjectDirectory)
                .ExecuteWithoutRestore()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Goodbye, from MSBuild. :'(");

            new DotnetCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("msbuild", "/t:SayThis", "/p:This=GreatScott")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("You want me to say 'GreatScott'");
        }

        [TestMethod]
        [Ignore("https://github.com/dotnet/sdk/issues/46822")]
        [DataRow("build")]
        [DataRow("clean")]
        [DataRow("pack")]
        [DataRow("publish")]
        [DataRow("restore")]
        public void When_help_is_invoked_Then_MSBuild_extra_options_text_is_included_in_output(string commandName)
        {
            const string MSBuildHelpText = " Any extra options that should be passed to MSBuild. See 'dotnet msbuild -h' for available options.";

            var projectDirectory = TestAssetsManager.CreateTestDirectory(identifier: commandName);
            var result = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory.Path)
                .Execute(commandName, "--help");

            result.ExitCode.Should().Be(0);
            result.StdOut.Should().Contain(MSBuildHelpText);
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        [DataRow("/p")]
        [DataRow("/property")]
        [DataRow("-p")]
        [DataRow("-property")]
        public void WhenRestoreSourcesStartsWithUnixPathThenHttpsSourceIsParsedCorrectly(string propertyFormat)
        {
            // this is a workaround for https://github.com/Microsoft/msbuild/issues/1622
            var testInstance = TestAssetsManager.CopyTestAsset("LibraryWithUnresolvablePackageReference", identifier: propertyFormat.GetHashCode().ToString())
                                        .WithSource();

            var root = testInstance.Path;
            var somePathThatExists = "/usr/local/bin";

            var result = new DotnetCommand(Log)
                .WithWorkingDirectory(root)
                .Execute("msbuild", $"{propertyFormat}:RestoreSources={somePathThatExists};https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json", "/t:restore", "LibraryWithUnresolvablePackageReference.csproj");

            result.Should().Fail();

            result.StdOut.Should().ContainVisuallySameFragment("NU1101");
        }

        [TestMethod]
        public void WhenDotnetRunHelpIsInvokedAppArgumentsTextIsIncludedInOutput()
        {
            string AppArgumentsText = "Arguments passed to the application that is being run.";

            var projectDirectory = TestAssetsManager.CreateTestDirectory("RunContainsAppArgumentsText");
            var result = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory.Path)
                .Execute("run", "--help");

            result.ExitCode.Should().Be(0);
            result.StdOut.Should().Contain(AppArgumentsText);
        }
    }
}
