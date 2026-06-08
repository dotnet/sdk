// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.CommandLine;
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
        public void VSTestCommandIncludesNoDependenciesOption()
        {
            var command = new TestCommandDefinition.VSTest();
            var parseResult = command.Parse(["--no-dependencies"]);
            var forwarded = parseResult.OptionValuesToBeForwarded(command);

            forwarded.Should().Contain("-property:BuildProjectReferences=false",
                "--no-dependencies should be forwarded to MSBuild as BuildProjectReferences=false to skip building project-to-project references.");
        }

        [Fact]
        public void MTPCommandIncludesNoDependenciesOption()
        {
            var command = new TestCommandDefinition.MicrosoftTestingPlatform();
            var parseResult = command.Parse(["--no-dependencies"]);
            var forwarded = parseResult.OptionValuesToBeForwarded(command);

            forwarded.Should().Contain("--property:BuildProjectReferences=false",
                "--no-dependencies should be forwarded to MSBuild as BuildProjectReferences=false to skip building project-to-project references.");
        }

        [Theory]
        [InlineData("--use-current-runtime")]
        [InlineData("--ucr")]
        public void MTPCommandForwardsUseCurrentRuntimeOption(string optionAlias)
        {
            var command = new TestCommandDefinition.MicrosoftTestingPlatform();
            var parseResult = command.Parse([optionAlias]);
            var forwarded = parseResult.OptionValuesToBeForwarded(command);

            forwarded.Should().Contain("--property:UseCurrentRuntimeIdentifier=True",
                $"{optionAlias} should be forwarded to MSBuild as UseCurrentRuntimeIdentifier=True so restore and build target the current runtime.");
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

        [Fact]
        public void Create_WhenGlobalJsonIsEmpty_FallsBackToVSTestInsteadOfThrowing()
        {
            using var temp = new TempDirectory();
            File.WriteAllText(Path.Combine(temp.Path, "global.json"), string.Empty);

            var command = TestCommandDefinition.Create(temp.Path, testRunnerEnvironmentValue: null);

            command.Should().BeOfType<TestCommandDefinition.VSTest>(
                "an empty global.json must not crash the CLI parser (regression for https://github.com/dotnet/sdk/issues/52384)");
        }

        [Fact]
        public void Create_WhenGlobalJsonIsMalformed_FallsBackToVSTestInsteadOfThrowing()
        {
            using var temp = new TempDirectory();
            File.WriteAllText(Path.Combine(temp.Path, "global.json"), "{ this is not valid json");

            var command = TestCommandDefinition.Create(temp.Path, testRunnerEnvironmentValue: null);

            command.Should().BeOfType<TestCommandDefinition.VSTest>();
        }

        [Fact]
        public void Create_WhenGlobalJsonHasMtpRunner_ReturnsMicrosoftTestingPlatform()
        {
            using var temp = new TempDirectory();
            File.WriteAllText(
                Path.Combine(temp.Path, "global.json"),
                """{ "test": { "runner": "Microsoft.Testing.Platform" } }""");

            var command = TestCommandDefinition.Create(temp.Path, testRunnerEnvironmentValue: null);

            command.Should().BeOfType<TestCommandDefinition.MicrosoftTestingPlatform>();
        }

        [Fact]
        public void Create_WhenGlobalJsonHasUnknownRunner_StillThrowsInvalidOperation()
        {
            // The "unknown runner" failure is intentional user-facing feedback for a typo in a
            // well-formed global.json and is preserved by the fix for #52384 (which only relaxes
            // the unreadable/malformed cases).
            using var temp = new TempDirectory();
            File.WriteAllText(
                Path.Combine(temp.Path, "global.json"),
                """{ "test": { "runner": "definitely-not-a-real-runner" } }""");

            // Pass an explicit null env value so the test cannot be silently disabled by a
            // DOTNET_TEST_RUNNER value that happens to be set in the developer/CI environment.
            Action act = () => TestCommandDefinition.Create(temp.Path, testRunnerEnvironmentValue: null);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*definitely-not-a-real-runner*");
        }

        [Fact]
        public void Create_WhenEnvironmentVariableIsMtp_ReturnsMicrosoftTestingPlatform()
        {
            using var temp = new TempDirectory();

            var command = TestCommandDefinition.Create(temp.Path, "Microsoft.Testing.Platform");

            command.Should().BeOfType<TestCommandDefinition.MicrosoftTestingPlatform>(
                "DOTNET_TEST_RUNNER=Microsoft.Testing.Platform should select MTP without requiring a global.json (#51505).");
        }

        [Fact]
        public void Create_WhenEnvironmentVariableIsVSTest_ReturnsVSTest()
        {
            using var temp = new TempDirectory();

            var command = TestCommandDefinition.Create(temp.Path, "VSTest");

            command.Should().BeOfType<TestCommandDefinition.VSTest>();
        }

        [Fact]
        public void Create_EnvironmentVariableMatchIsCaseInsensitive()
        {
            using var temp = new TempDirectory();

            var command = TestCommandDefinition.Create(temp.Path, "microsoft.testing.platform");

            command.Should().BeOfType<TestCommandDefinition.MicrosoftTestingPlatform>();
        }

        [Theory]
        [InlineData(" VSTest ")]
        [InlineData("\tVSTest\n")]
        public void Create_TrimsWhitespaceAroundEnvironmentVariableValue_VSTest(string envValue)
        {
            using var temp = new TempDirectory();

            var command = TestCommandDefinition.Create(temp.Path, envValue);

            command.Should().BeOfType<TestCommandDefinition.VSTest>(
                "shell/editor mishaps that surround the env var with whitespace must not silently change the selected runner.");
        }

        [Theory]
        [InlineData(" Microsoft.Testing.Platform ")]
        [InlineData("\tMicrosoft.Testing.Platform\n")]
        public void Create_TrimsWhitespaceAroundEnvironmentVariableValue_MTP(string envValue)
        {
            using var temp = new TempDirectory();

            var command = TestCommandDefinition.Create(temp.Path, envValue);

            command.Should().BeOfType<TestCommandDefinition.MicrosoftTestingPlatform>();
        }

        [Fact]
        public void Create_EnvironmentVariableTakesPrecedenceOverGlobalJson_MtpOverridesVSTest()
        {
            using var temp = new TempDirectory();
            File.WriteAllText(
                Path.Combine(temp.Path, "global.json"),
                """{ "test": { "runner": "VSTest" } }""");

            var command = TestCommandDefinition.Create(temp.Path, "Microsoft.Testing.Platform");

            command.Should().BeOfType<TestCommandDefinition.MicrosoftTestingPlatform>(
                "DOTNET_TEST_RUNNER should override the runner configured in global.json (#51505).");
        }

        [Fact]
        public void Create_EnvironmentVariableTakesPrecedenceOverGlobalJson_VSTestOverridesMtp()
        {
            using var temp = new TempDirectory();
            File.WriteAllText(
                Path.Combine(temp.Path, "global.json"),
                """{ "test": { "runner": "Microsoft.Testing.Platform" } }""");

            var command = TestCommandDefinition.Create(temp.Path, "VSTest");

            command.Should().BeOfType<TestCommandDefinition.VSTest>(
                "DOTNET_TEST_RUNNER should override the runner configured in global.json (#51505).");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_WhenEnvironmentVariableIsEmptyOrWhitespace_FallsBackToGlobalJson(string? envValue)
        {
            using var temp = new TempDirectory();
            File.WriteAllText(
                Path.Combine(temp.Path, "global.json"),
                """{ "test": { "runner": "Microsoft.Testing.Platform" } }""");

            var command = TestCommandDefinition.Create(temp.Path, envValue);

            command.Should().BeOfType<TestCommandDefinition.MicrosoftTestingPlatform>(
                "an unset or whitespace-only DOTNET_TEST_RUNNER must not block the global.json runner from being honored.");
        }

        [Fact]
        public void Create_WhenEnvironmentVariableIsUnknown_FallsBackToGlobalJson()
        {
            // An unrecognized DOTNET_TEST_RUNNER value must NOT throw — TestCommandDefinition.Create
            // is invoked during CLI parser construction (i.e. for every `dotnet ...` invocation,
            // including `dotnet --version` and `dotnet build`). A stale or mistyped env var that
            // a user inadvertently left set must not crash every command on the box.
            using var temp = new TempDirectory();
            File.WriteAllText(
                Path.Combine(temp.Path, "global.json"),
                """{ "test": { "runner": "Microsoft.Testing.Platform" } }""");

            var command = TestCommandDefinition.Create(temp.Path, "definitely-not-a-real-runner");

            command.Should().BeOfType<TestCommandDefinition.MicrosoftTestingPlatform>(
                "an unrecognized env var value should be ignored so the global.json runner still wins.");
        }

        [Fact]
        public void Create_WhenEnvironmentVariableIsUnknownAndNoGlobalJson_DefaultsToVSTest()
        {
            using var temp = new TempDirectory();

            var command = TestCommandDefinition.Create(temp.Path, "definitely-not-a-real-runner");

            command.Should().BeOfType<TestCommandDefinition.VSTest>(
                "with no other configuration, an unrecognized env var value should fall back to the default runner instead of crashing the CLI.");
        }

        [Fact]
        public void Create_ValidEnvironmentVariableShieldsInvalidGlobalJson()
        {
            // When DOTNET_TEST_RUNNER is set to a recognized runner, it must take precedence
            // and prevent the throw that would otherwise come from an invalid global.json
            // runner name. This is the key escape hatch: a user with a broken global.json can
            // unblock themselves by setting the env var instead of editing the repo config.
            using var temp = new TempDirectory();
            File.WriteAllText(
                Path.Combine(temp.Path, "global.json"),
                """{ "test": { "runner": "definitely-not-a-real-runner" } }""");

            var command = TestCommandDefinition.Create(temp.Path, "Microsoft.Testing.Platform");

            command.Should().BeOfType<TestCommandDefinition.MicrosoftTestingPlatform>(
                "a valid DOTNET_TEST_RUNNER should shield users from an unsupported runner name in global.json.");
        }

        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "TestCommandDefinitionTests_" + Guid.NewGuid().ToString("N"));

            public TempDirectory() => Directory.CreateDirectory(Path);

            public void Dispose()
            {
                try
                {
                    Directory.Delete(Path, recursive: true);
                }
                catch
                {
                    // best-effort cleanup
                }
            }
        }
    }
}
