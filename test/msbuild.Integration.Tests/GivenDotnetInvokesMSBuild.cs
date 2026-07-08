// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.MSBuild.IntegrationTests
{
    [TestClass]
    public class GivenDotnetInvokesMSBuild : SdkTest
    {
        [TestMethod]
        [DataRow("build")]
        [DataRow("clean")]
        [DataRow("msbuild")]
        [DataRow("pack")]
        [DataRow("publish")]
        [DataRow("test")]
        public void When_dotnet_command_invokes_msbuild_Then_env_vars_and_m_are_passed(string command)
        {
            var testInstance = TestAssetsManager.CopyTestAsset("MSBuildIntegration", identifier: command)
                .WithSource();

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(command)
                .Should().Pass();
        }

        [TestMethod]
        [DataRow("build")]
        [DataRow("msbuild")]
        [DataRow("pack")]
        [DataRow("publish")]
        public void When_dotnet_command_invokes_msbuild_with_no_args_verbosity_is_set_to_minimum(string command)
        {
            var testInstance = TestAssetsManager.CopyTestAsset("MSBuildIntegration", identifier: command)
                .WithSource();

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(command);

            cmd.Should().Pass();

            cmd.StdOut
                .Should().NotContain("Message with normal importance", "Because verbosity is set to minimum")
                     .And.Contain("Message with high importance", "Because high importance messages are shown on minimum verbosity");
        }

        [TestMethod]
        [DataRow("build")]
        [DataRow("clean")]
        [DataRow("pack")]
        [DataRow("publish")]
        public void When_dotnet_command_invokes_msbuild_with_diag_verbosity_Then_arg_is_passed(string command)
        {
            var testInstance = TestAssetsManager.CopyTestAsset("MSBuildIntegration", identifier: command)
                .WithSource();

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .WithEnvironmentVariable("HelixAccessToken", "")
                .WithEnvironmentVariable("SYSTEM_ACCESSTOKEN", "")
                .Execute(command, "-v", "diag");

            cmd.Should().Pass();

            cmd.StdOut.Should().Contain("Message with low importance");
        }

        [TestMethod]
        public void When_dotnet_test_invokes_msbuild_with_no_args_verbosity_is_set_to_minimum()
        {
            var testInstance = TestAssetsManager.CopyTestAsset("MSBuildIntegration")
                .WithSource();

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("test");

            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("Message with high importance");
        }

        [TestMethod]
        public void When_dotnet_msbuild_command_is_invoked_with_non_msbuild_switch_Then_it_fails()
        {
            var testInstance = TestAssetsManager.CopyTestAsset("MSBuildIntegration")
                .WithSource();

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("msbuild", "-v", "unknown");

            cmd.ExitCode.Should().NotBe(0);
        }

        [TestMethod]
        public void When_MSBuildSDKsPath_is_set_by_env_var_then_it_is_not_overridden()
        {
            var testInstance = TestAssetsManager.CopyTestAsset("MSBuildIntegration")
                .WithSource();

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .WithEnvironmentVariable("MSBuildSDKsPath", "AnyString")
                .Execute($"msbuild");

            cmd.ExitCode.Should().NotBe(0);

            cmd.StdOut.Should().Contain("Expected 'AnyString")
                           .And.Contain("to exist, but it does not.");
        }
    }
}
