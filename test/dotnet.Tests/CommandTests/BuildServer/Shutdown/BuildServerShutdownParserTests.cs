// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.BuildServer.Shutdown;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class BuildServerShutdownParserTests
    {
        private readonly ITestOutputHelper output;

        public BuildServerShutdownParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void GivenNoOptionsAllFlagsAreFalse()
        {
            var result = Parser.Parse(["dotnet", "build-server", "shutdown"]);

            result.GetValue<bool>(BuildServerShutdownCommandParser.MSBuildOption).Should().Be(false);
            result.GetValue<bool>(BuildServerShutdownCommandParser.VbcsOption).Should().Be(false);
            result.GetValue<bool>(BuildServerShutdownCommandParser.RazorOption).Should().Be(false);
        }

        [Fact]
        public void GivenMSBuildOptionIsItTrue()
        {
            var result = Parser.Parse(["dotnet", "build-server", "shutdown", "--msbuild"]);

            result.GetValue<bool>(BuildServerShutdownCommandParser.MSBuildOption).Should().Be(true);
            result.GetValue<bool>(BuildServerShutdownCommandParser.VbcsOption).Should().Be(false);
            result.GetValue<bool>(BuildServerShutdownCommandParser.RazorOption).Should().Be(false);
        }

        [Fact]
        public void GivenVBCSCompilerOptionIsItTrue()
        {
            var result = Parser.Parse(["dotnet", "build-server", "shutdown", "--vbcscompiler"]);

            result.GetValue<bool>(BuildServerShutdownCommandParser.MSBuildOption).Should().Be(false);
            result.GetValue<bool>(BuildServerShutdownCommandParser.VbcsOption).Should().Be(true);
            result.GetValue<bool>(BuildServerShutdownCommandParser.RazorOption).Should().Be(false);
        }

        [Fact]
        public void GivenRazorOptionIsItTrue()
        {
            var result = Parser.Parse(["dotnet", "build-server", "shutdown", "--razor"]);

            result.GetValue<bool>(BuildServerShutdownCommandParser.MSBuildOption).Should().Be(false);
            result.GetValue<bool>(BuildServerShutdownCommandParser.VbcsOption).Should().Be(false);
            result.GetValue<bool>(BuildServerShutdownCommandParser.RazorOption).Should().Be(true);
        }

        [Fact]
        public void GivenMultipleOptionsThoseAreTrue()
        {
            var result = Parser.Parse(["dotnet", "build-server", "shutdown", "--razor", "--msbuild"]);

            result.GetValue<bool>(BuildServerShutdownCommandParser.MSBuildOption).Should().Be(true);
            result.GetValue<bool>(BuildServerShutdownCommandParser.VbcsOption).Should().Be(false);
            result.GetValue<bool>(BuildServerShutdownCommandParser.RazorOption).Should().Be(true);
        }
    }
}
