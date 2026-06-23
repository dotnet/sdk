// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Tool.Run;
using Microsoft.DotNet.Cli.Extensions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    [TestClass]
    public class ToolRunParserTests
    {

        [TestMethod]
        public void ListToolParserCanGetToolCommandNameArgument()
        {
            var result = Parser.Parse("dotnet tool run dotnetsay");

            var definition = Assert.IsExactInstanceOfType<ToolRunCommandDefinition>(result.CommandResult.Command);
            var packageId = result.GetValue(definition.CommandNameArgument);

            packageId.Should().Be("dotnetsay");
        }

        [TestMethod]
        public void ListToolParserCanGetCommandsArgumentInUnmatchedTokens()
        {
            var result = Parser.Parse("dotnet tool run dotnetsay hi");

            result.ShowHelpOrErrorIfAppropriate(); // Should not throw error
        }

        [TestMethod]
        public void ListToolParserCanGetCommandsArgumentInUnparsedTokens()
        {
            var result = Parser.Parse("dotnet tool run dotnetsay -- hi");

            result.Errors.Should().BeEmpty();
        }

        [TestMethod]
        public void ListToolParserCanGetCommandsArgumentInUnparsedTokens2()
        {
            var result = Parser.Parse("dotnet tool run dotnetsay hi1 -- hi2");

            result.ShowHelpOrErrorIfAppropriate(); // Should not throw error
        }

        [TestMethod]
        public void RootSubCommandIsToolCommand()
        {
            var result = Parser.Parse("dotnetsay run -v arg");
            result.RootSubCommandResult().Should().Be("dotnetsay");
        }
    }
}
