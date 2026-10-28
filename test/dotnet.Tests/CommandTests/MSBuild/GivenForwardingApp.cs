// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli
{
    [TestClass]
    public class GivenForwardingApp
    {
        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void DotnetExeIsExecuted()
        {
            new ForwardingApp("<apppath>", new string[0])
                .GetProcessStartInfo().FileName.Should().EndWith("dotnet.exe");
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        public void DotnetIsExecuted()
        {
            new ForwardingApp("<apppath>", new string[0])
                .GetProcessStartInfo().FileName.Should().EndWith("dotnet");
        }

        [TestMethod]
        public void ItForwardsArgs()
        {
            new ForwardingApp("<apppath>", new string[] { "one", "two", "three" })
                .GetProcessStartInfo().Arguments.Should().Be("exec <apppath> one two three");
        }

        [TestMethod]
        public void ItAddsDepsFileArg()
        {
            new ForwardingApp("<apppath>", new string[] { "<arg>" }, depsFile: "<deps-file>")
                .GetProcessStartInfo().Arguments.Should().Be("exec --depsfile <deps-file> <apppath> <arg>");
        }

        [TestMethod]
        public void ItAddsRuntimeConfigArg()
        {
            new ForwardingApp("<apppath>", new string[] { "<arg>" }, runtimeConfig: "<runtime-config>")
                .GetProcessStartInfo().Arguments.Should().Be("exec --runtimeconfig <runtime-config> <apppath> <arg>");
        }

        [TestMethod]
        public void ItAddsAdditionalProbingPathArg()
        {
            new ForwardingApp("<apppath>", new string[] { "<arg>" }, additionalProbingPath: "<additionalprobingpath>")
                .GetProcessStartInfo().Arguments.Should().Be("exec --additionalprobingpath <additionalprobingpath> <apppath> <arg>");
        }

        [TestMethod]
        public void ItQuotesArgsWithSpaces()
        {
            new ForwardingApp("<apppath>", new string[] { "a b c" })
                .GetProcessStartInfo().Arguments.Should().Be("exec <apppath> \"a b c\"");
        }

        [TestMethod]
        public void ItEscapesArgs()
        {
            new ForwardingApp("<apppath>", new string[] { "a\"b\"c" })
                .GetProcessStartInfo().Arguments.Should().Be("exec <apppath> a\\\"b\\\"c");
        }

        [TestMethod]
        public void ItSetsEnvironmentalVariables()
        {
            var startInfo = new ForwardingApp("<apppath>", new string[0], environmentVariables: new Dictionary<string, string>
            {
                { "env1", "env1value" },
                { "env2", "env2value" }
            })
            .GetProcessStartInfo();

            startInfo.EnvironmentVariables["env1"].Should().Be("env1value");
            startInfo.EnvironmentVariables["env2"].Should().Be("env2value");
        }
    }
}
