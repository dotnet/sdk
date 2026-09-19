// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.BuildServer.Shutdown;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    [TestClass]
    public class BuildServerShutdownParserTests
    {
        [TestMethod]
        public void GivenNoOptionsAllFlagsAreFalse()
        {
            var result = Parser.Parse(["dotnet", "build-server", "shutdown"]);
            var definition = Assert.IsExactInstanceOfType<BuildServerShutdownCommandDefinition>(result.CommandResult.Command);

            result.GetValue(definition.MSBuildOption).Should().Be(false);
            result.GetValue(definition.VbcsOption).Should().Be(false);
            result.GetValue(definition.RazorOption).Should().Be(false);
        }

        [TestMethod]
        public void GivenMSBuildOptionIsItTrue()
        {
            var result = Parser.Parse(["dotnet", "build-server", "shutdown", "--msbuild"]);
            var definition = Assert.IsExactInstanceOfType<BuildServerShutdownCommandDefinition>(result.CommandResult.Command);

            result.GetValue(definition.MSBuildOption).Should().Be(true);
            result.GetValue(definition.VbcsOption).Should().Be(false);
            result.GetValue(definition.RazorOption).Should().Be(false);
        }

        [TestMethod]
        public void GivenVBCSCompilerOptionIsItTrue()
        {
            var result = Parser.Parse(["dotnet", "build-server", "shutdown", "--vbcscompiler"]);
            var definition = Assert.IsExactInstanceOfType<BuildServerShutdownCommandDefinition>(result.CommandResult.Command);

            result.GetValue(definition.MSBuildOption).Should().Be(false);
            result.GetValue(definition.VbcsOption).Should().Be(true);
            result.GetValue(definition.RazorOption).Should().Be(false);
        }

        [TestMethod]
        public void GivenRazorOptionIsItTrue()
        {
            var result = Parser.Parse(["dotnet", "build-server", "shutdown", "--razor"]);
            var definition = Assert.IsExactInstanceOfType<BuildServerShutdownCommandDefinition>(result.CommandResult.Command);

            result.GetValue(definition.MSBuildOption).Should().Be(false);
            result.GetValue(definition.VbcsOption).Should().Be(false);
            result.GetValue(definition.RazorOption).Should().Be(true);
        }

        [TestMethod]
        public void GivenMultipleOptionsThoseAreTrue()
        {
            var result = Parser.Parse(["dotnet", "build-server", "shutdown", "--razor", "--msbuild"]);
            var definition = Assert.IsExactInstanceOfType<BuildServerShutdownCommandDefinition>(result.CommandResult.Command);

            result.GetValue(definition.MSBuildOption).Should().Be(true);
            result.GetValue(definition.VbcsOption).Should().Be(false);
            result.GetValue(definition.RazorOption).Should().Be(true);
        }
    }
}
