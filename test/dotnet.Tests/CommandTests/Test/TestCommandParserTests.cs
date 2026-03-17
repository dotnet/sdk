// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Extensions;
using TestCommand = Microsoft.DotNet.Cli.Commands.Test.TestCommand;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class TestCommandDefinitionTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("\"a\"")]
        [InlineData("\"aaa\"")]
        public void SurroundWithDoubleQuotesWhenAlreadySurroundedDoesNothing(string input)
        {
            var escapedInput = "\"" + input + "\"";
            var result = MSBuildPropertyParser.SurroundWithDoubleQuotes(escapedInput);
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
            var result = MSBuildPropertyParser.SurroundWithDoubleQuotes(input);
            result.Should().Be("\"" + input + "\"");
        }

        [Theory]
        [InlineData("\\\\")]
        [InlineData("\\\\\\\\")]
        [InlineData("/\\\\")]
        [InlineData("/\\/\\/\\\\")]
        public void SurroundWithDoubleQuotesHandlesCorrectlyEvenCountOfTrailingBackslashes(string input)
        {
            var result = MSBuildPropertyParser.SurroundWithDoubleQuotes(input);
            result.Should().Be("\"" + input + "\"");
        }

        [Theory]
        [InlineData("\\")]
        [InlineData("\\\\\\")]
        [InlineData("/\\")]
        [InlineData("/\\/\\/\\")]
        public void SurroundWithDoubleQuotesHandlesCorrectlyOddCountOfTrailingBackslashes(string input)
        {
            var result = MSBuildPropertyParser.SurroundWithDoubleQuotes(input);
            result.Should().Be("\"" + input + "\\\"");
        }

        [Fact]
        public void VSTestCommandIncludesPropertiesOption()
        {
            var command = TestCommandDefinition.Create();
            
            // Verify that the command includes a property option that supports the /p alias
            var propertyOption = command.Options.FirstOrDefault(o => 
                o.Aliases.Contains("/p") || o.Aliases.Contains("--property"));
            
            propertyOption.Should().NotBeNull("VSTest command should include CommonOptions.CreatePropertyOption to support /p Property=Value syntax");
            propertyOption.Aliases.Should().Contain("/p", "CreatePropertyOption should include /p alias for MSBuild compatibility");
        }

        [Fact]
        public void DllDetectionShouldExcludeRunArgumentsAndGlobalProperties()
        {
            var parseResult = Parser.Parse("""test -p:"RunConfig=abd.dll" -- RunConfig=abd.dll -p:"RunConfig=abd.dll" --results-directory hey.dll""");
            var args = parseResult.GetArguments();

            (args, string[] settings) = TestCommand.SeparateSettingsFromArgs(args);
            int settingsCount = TestCommand.GetSettingsCount(settings);
            settingsCount.Should().Be(4);

            // Our unmatched tokens for this test case are only the settings (after the `--`).
            Assert.Equal(settingsCount, parseResult.UnmatchedTokens.Count);

            Assert.Equal("--", settings[0]);
            Assert.Equal(settings.Length, settingsCount + 1);
            for (int i = 1; i <= settingsCount; i++)
            {
                Assert.Equal(settings[^i], parseResult.UnmatchedTokens[^i]);
            }

            TestCommand.ContainsBuiltTestSources(parseResult, settingsCount).Should().Be(false);
        }

        [Fact]
        public void DllDetectionShouldBeTrueWhenPresentAloneEvenIfDuplicatedInSettings()
        {
            var parseResult = Parser.Parse("""test abd.dll -- abd.dll""");
            var args = parseResult.GetArguments();

            (args, string[] settings) = TestCommand.SeparateSettingsFromArgs(args);
            int settingsCount = TestCommand.GetSettingsCount(settings);
            settingsCount.Should().Be(1);

            // Our unmatched tokens here are all the settings, plus the abd.dll before the `--`.
            Assert.Equal(settingsCount + 1, parseResult.UnmatchedTokens.Count);

            Assert.Equal("--", settings[0]);
            Assert.Equal(settings.Length, settingsCount + 1);
            for (int i = 1; i <= settingsCount; i++)
            {
                Assert.Equal(settings[^i], parseResult.UnmatchedTokens[^i]);
            }

            TestCommand.ContainsBuiltTestSources(parseResult, settingsCount).Should().Be(true);
        }

        [Theory]
        [InlineData("abd.dll", true)]
        [InlineData("abd.dll --", true)]
        [InlineData("-dl:abd.dll", false)]
        [InlineData("-dl:abd.dll --", false)]
        [InlineData("-abcd:abd.dll", false)]
        [InlineData("-abcd:abd.dll --", false)]
        [InlineData("-p:abd.dll", false)]
        [InlineData("-p:abd.dll --", false)]
        public void DllDetectionShouldWorkWhenNoSettings(string testArgs, bool expectedContainsBuiltTestSource)
        {
            var parseResult = Parser.Parse($"test {testArgs}");
            var args = parseResult.GetArguments();

            (args, string[] settings) = TestCommand.SeparateSettingsFromArgs(args);
            int settingsCount = TestCommand.GetSettingsCount(settings);
            settingsCount.Should().Be(0);
            TestCommand.ContainsBuiltTestSources(parseResult, settingsCount).Should().Be(expectedContainsBuiltTestSource);
        }
    }
}
