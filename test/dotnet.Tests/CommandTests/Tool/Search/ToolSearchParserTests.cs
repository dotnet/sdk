// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.Tool.Search;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class ToolSearchParserTests
    {
        private readonly ITestOutputHelper output;

        public ToolSearchParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void DotnetToolSearchShouldThrowWhenNoSearchTerm()
        {
            var result = Parser.Parse("dotnet tool search");
            Action a = () => new ToolSearchCommand(result);
            a.Should().Throw<CommandParsingException>();
        }

        [Fact]
        public void ListSearchParserCanGetArguments()
        {
            var result = Parser.Parse("dotnet tool search mytool --detail --skip 3 --take 4 --prerelease");

            var definition = Assert.IsType<ToolSearchCommandDefinition>(result.CommandResult.Command);

            result.GetValue(definition.SearchTermArgument).Should().Be("mytool");
            result.UnmatchedTokens.Should().BeEmpty();
            result.GetValue(definition.DetailOption).Should().Be(true);
            result.GetValue(definition.SkipOption).Should().Be("3");
            result.GetValue(definition.TakeOption).Should().Be("4");
            result.GetValue(definition.PrereleaseOption).Should().Be(true);
        }
    }
}
