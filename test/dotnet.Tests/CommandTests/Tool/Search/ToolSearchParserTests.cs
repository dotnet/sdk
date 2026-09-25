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
        public void ToolSearchParserCanGetSourceSelectionOptions()
        {
            string configFile = typeof(ToolSearchParserTests).Assembly.Location;
            var result = Parser.Parse(
                $"dotnet tool search mytool --configfile \"{configFile}\" " +
                "--source source1 --source source2 --add-source additional1 --add-source additional2 --interactive");

            var definition = Assert.IsExactInstanceOfType<ToolSearchCommandDefinition>(result.CommandResult.Command);
            result.Errors.Should().BeEmpty();
            result.GetRequiredValue(definition.ConfigOption).FullName.Should().Be(configFile);
            result.GetRequiredValue(definition.SourceOption).Should().Equal("source1", "source2");
            result.GetRequiredValue(definition.AddSourceOption).Should().Equal("additional1", "additional2");
            result.GetValue(definition.InteractiveOption).Should().BeTrue();
        }

        [TestMethod]
        public void ToolSearchParserRejectsMissingConfigFile()
        {
            string configFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.config");

            var result = Parser.Parse($"dotnet tool search mytool --configfile \"{configFile}\"");

            result.Errors.Should().ContainSingle();
            result.Errors[0].Message.Should().Contain(configFile);
        }

    }
}
