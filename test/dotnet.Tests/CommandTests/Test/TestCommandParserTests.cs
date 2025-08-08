// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Test;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class TestCommandParserTests
    {
        [Fact]
        public void SurroundWithDoubleQuotesWithNullThrows()
        {
            Assert.Throws<ArgumentNullException>(() =>
                TestCommandParser.SurroundWithDoubleQuotes(null));
        }

        [Theory]
        [InlineData("")]
        [InlineData("\"a\"")]
        [InlineData("\"aaa\"")]
        public void SurroundWithDoubleQuotesWhenAlreadySurroundedDoesNothing(string input)
        {
            var escapedInput = "\"" + input + "\"";
            var result = TestCommandParser.SurroundWithDoubleQuotes(escapedInput);
            result.Should().Be(escapedInput);
        }

        [Theory]
        [InlineData("")]
        [InlineData("a")]
        [InlineData("aaa")]
        [InlineData("\"a")]
        [InlineData("a\"")]
        public void SurroundWithDoubleQuotesWhenNotSurroundedSurrounds(string input)
        {
            var result = TestCommandParser.SurroundWithDoubleQuotes(input);
            result.Should().Be("\"" + input + "\"");
        }

        [Theory]
        [InlineData("\\\\")]
        [InlineData("\\\\\\\\")]
        [InlineData("/\\\\")]
        [InlineData("/\\/\\/\\\\")]
        public void SurroundWithDoubleQuotesHandlesCorrectlyEvenCountOfTrailingBackslashes(string input)
        {
            var result = TestCommandParser.SurroundWithDoubleQuotes(input);
            result.Should().Be("\"" + input + "\"");
        }

        [Theory]
        [InlineData("\\")]
        [InlineData("\\\\\\")]
        [InlineData("/\\")]
        [InlineData("/\\/\\/\\")]
        public void SurroundWithDoubleQuotesHandlesCorrectlyOddCountOfTrailingBackslashes(string input)
        {
            var result = TestCommandParser.SurroundWithDoubleQuotes(input);
            result.Should().Be("\"" + input + "\\\"");
        }

        [Fact]
        public void VSTestCommandIncludesPropertiesOption()
        {
            var command = TestCommandParser.GetCommand();
            
            // Verify that the command includes a property option that supports the /p alias
            var propertyOption = command.Options.FirstOrDefault(o => 
                o.Aliases.Contains("/p") || o.Aliases.Contains("--property"));
            
            propertyOption.Should().NotBeNull("VSTest command should include CommonOptions.PropertiesOption to support /p Property=Value syntax");
            propertyOption.Aliases.Should().Contain("/p", "PropertiesOption should include /p alias for MSBuild compatibility");
        }
    }
}
