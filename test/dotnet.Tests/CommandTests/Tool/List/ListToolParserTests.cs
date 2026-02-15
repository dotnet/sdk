// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Tool.List;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class ListToolParserTests
    {
        private readonly ITestOutputHelper output;

        public ListToolParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void ListToolParserCanGetGlobalOption()
        {
            var result = Parser.Parse("dotnet tool list -g");

            var definition = Assert.IsType<ToolListCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.LocationOptions.GlobalOption).Should().Be(true);
        }

        [Fact]
        public void ListToolParserCanGetLocalOption()
        {
            var result = Parser.Parse("dotnet tool list --local");

            var definition = Assert.IsType<ToolListCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.LocationOptions.LocalOption).Should().Be(true);
        }

        [Fact]
        public void ListToolParserCanParseToolPathOption()
        {
            var result =
                Parser.Parse(@"dotnet tool list --tool-path C:\Tools ");

            var definition = Assert.IsType<ToolListCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.LocationOptions.ToolPathOption).Should().Be(@"C:\Tools");
        }
    }
}
