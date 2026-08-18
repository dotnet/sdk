// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.Tool.Search;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    [TestClass]
    public class ToolSearchParserTests
    {

        [TestMethod]
        public void DotnetToolSearchShouldThrowWhenNoSearchTerm()
        {
            var result = Parser.Parse("dotnet tool search");
            Action a = () => new ToolSearchCommand(result);
            a.Should().Throw<CommandParsingException>();
        }

        [TestMethod]
        public void ListSearchParserCanGetArguments()
        {
            var result = Parser.Parse("dotnet tool search mytool --detail --skip 3 --take 4 --prerelease");

            var definition = Assert.IsExactInstanceOfType<ToolSearchCommandDefinition>(result.CommandResult.Command);

            result.GetValue(definition.SearchTermArgument).Should().Be("mytool");
            result.UnmatchedTokens.Should().BeEmpty();
            result.GetValue(definition.DetailOption).Should().Be(true);
            result.GetValue(definition.SkipOption).Should().Be("3");
            result.GetValue(definition.TakeOption).Should().Be("4");
            result.GetValue(definition.PrereleaseOption).Should().Be(true);
        }

        [TestMethod]
        public void ToolSearchParserCanGetConfigFileOption()
        {
            var result = Parser.Parse(@"dotnet tool search mytool --configfile C:\TestAssetLocalNugetFeed");

            var definition = Assert.IsExactInstanceOfType<ToolSearchCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.ConfigOption).Should().Be(@"C:\TestAssetLocalNugetFeed");
        }

        [TestMethod]
        public void ToolSearchParserCanParseSourceOption()
        {
            const string expectedSourceValue = "TestSourceValue";

            var result = Parser.Parse($"dotnet tool search mytool --source {expectedSourceValue}");

            var definition = Assert.IsExactInstanceOfType<ToolSearchCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.SourceOption).First().Should().Be(expectedSourceValue);
        }

        [TestMethod]
        public void ToolSearchParserCanParseMultipleSourceOption()
        {
            const string expectedSourceValue1 = "TestSourceValue1";
            const string expectedSourceValue2 = "TestSourceValue2";

            var result =
                Parser.Parse(
                    $"dotnet tool search mytool " +
                    $"--source {expectedSourceValue1} " +
                    $"--source {expectedSourceValue2}");

            var definition = Assert.IsExactInstanceOfType<ToolSearchCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.SourceOption)[0].Should().Be(expectedSourceValue1);
            result.GetRequiredValue(definition.SourceOption)[1].Should().Be(expectedSourceValue2);
        }

        [TestMethod]
        public void ToolSearchParserCanParseAddSourceOption()
        {
            const string expectedSourceValue = "TestSourceValue";

            var result = Parser.Parse($"dotnet tool search mytool --add-source {expectedSourceValue}");

            var definition = Assert.IsExactInstanceOfType<ToolSearchCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.AddSourceOption).First().Should().Be(expectedSourceValue);
        }

        [TestMethod]
        public void ToolSearchParserCanParseMultipleAddSourceOption()
        {
            const string expectedSourceValue1 = "TestSourceValue1";
            const string expectedSourceValue2 = "TestSourceValue2";

            var result =
                Parser.Parse(
                    $"dotnet tool search mytool " +
                    $"--add-source {expectedSourceValue1} " +
                    $"--add-source {expectedSourceValue2}");

            var definition = Assert.IsExactInstanceOfType<ToolSearchCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.AddSourceOption)[0].Should().Be(expectedSourceValue1);
            result.GetRequiredValue(definition.AddSourceOption)[1].Should().Be(expectedSourceValue2);
        }

        [TestMethod]
        public void ToolSearchParserCanParseSourceAndAddSourceTogether()
        {
            const string expectedSourceValue = "TestSourceValue";
            const string expectedAddSourceValue = "TestAddSourceValue";

            var result =
                Parser.Parse(
                    $"dotnet tool search mytool " +
                    $"--source {expectedSourceValue} " +
                    $"--add-source {expectedAddSourceValue}");

            var definition = Assert.IsExactInstanceOfType<ToolSearchCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.SourceOption).First().Should().Be(expectedSourceValue);
            result.GetRequiredValue(definition.AddSourceOption).First().Should().Be(expectedAddSourceValue);
        }
    }
}
