// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace dotnet.Tests
{
    [TestClass]
    public class GivenThatTheUserRequestsHelp : SdkTest
    {
        public GivenThatTheUserRequestsHelp()
        {
        }

        [TestMethod]
        [DataRow("-h")]
        [DataRow("add -h")]
        [DataRow("add package -h")]
        [DataRow("add reference -h")]
        [DataRow("build -h")]
        [DataRow("clean -h")]
        [DataRow("list -h")]
        [DataRow("msbuild -h")]
        [DataRow("new -h --debug:ephemeral-hive")]
        [DataRow("nuget -h")]
        [DataRow("pack -h")]
        [DataRow("publish -h")]
        [DataRow("remove -h")]
        [DataRow("restore -h")]
        [DataRow("run -h")]
        [DataRow("sln -h")]
        [DataRow("sln add -h")]
        [DataRow("sln list -h")]
        [DataRow("sln remove -h")]
        [DataRow("store -h")]
        [DataRow("test -h")]
        public void TheResponseIsNotAnError(string commandLine)
        {
            var result = new DotnetCommand(Log)
                .Execute(commandLine.Split());

            result.ExitCode.Should().Be(0);
        }

        [TestMethod]
        [DataRow("false")]
        [DataRow("true")]
        public void BuildHelpShowsPropertyOption(string enableAot)
        {
            var result = new DotnetCommand(Log)
                .WithEnvironmentVariable("DOTNET_CLI_ENABLEAOT", enableAot)
                .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en")
                .Execute("build", "--help");

            result.Should().Pass()
                .And.HaveStdOutContaining("-p, --property <NAME=VALUE>")
                .And.HaveStdOutContaining("Set one or more MSBuild properties.")
                .And.NotHaveStdOutContaining("--restoreProperty")
                .And.NotHaveStdOutContaining("--getProperty")
                .And.NotHaveStdOutContaining("--source");
        }

        [TestMethod]
        [DataRow("clean")]
        [DataRow("pack")]
        [DataRow("publish")]
        [DataRow("restore")]
        public void PropertyOptionRemainsHiddenForOtherCommands(string command)
        {
            new DotnetCommand(Log)
                .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en")
                .Execute(command, "--help")
                .Should().Pass()
                .And.NotHaveStdOutContaining("--property");
        }

        [TestMethod]
        [DataRow("faketool -h")]
        public void TheResponseIsAnError(string commandLine)
        {
            var result = new DotnetCommand(Log)
                .Execute(commandLine.Split());

            result.ExitCode.Should().Be(1);
        }
    }
}
