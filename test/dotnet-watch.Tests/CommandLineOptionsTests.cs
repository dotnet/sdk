// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watcher.Tools
{
    public class CommandLineOptionsTests
    {
        private readonly MockReporter _testReporter = new();

        private CommandLineOptions VerifyOptions(string[] args, string expectedOutput = "", string[] expectedMessages = null)
            => VerifyOptions(args, actualOutput => AssertEx.Equal(expectedOutput, actualOutput), expectedMessages ?? []);

        private CommandLineOptions VerifyOptions(string[] args, Action<string> outputValidator, string[] expectedMessages)
        {
            var output = new StringWriter();
            var options = CommandLineOptions.Parse(args, _testReporter, output: output, errorCode: out var errorCode);

            Assert.Equal(expectedMessages, _testReporter.Messages);
            outputValidator(output.ToString());

            Assert.NotNull(options);
            Assert.Equal(0, errorCode);
            return options;
        }

        private void VerifyErrors(string[] args, params string[] expectedErrors)
        {
            var output = new StringWriter();
            var options = CommandLineOptions.Parse(args, _testReporter, output: output, errorCode: out var errorCode);

            AssertEx.Equal(expectedErrors, _testReporter.Messages);
            Assert.Empty(output.ToString());

            Assert.Null(options);
            Assert.NotEqual(0, errorCode);
        }

        [Theory]
        [InlineData(new object[] { new[] { "-h" } })]
        [InlineData(new object[] { new[] { "-?" } })]
        [InlineData(new object[] { new[] { "--help" } })]
        [InlineData(new object[] { new[] { "--help", "--bogus" } })]
        public void HelpArgs(string[] args)
        {
            var output = new StringWriter();
            var options = CommandLineOptions.Parse(args, _testReporter, output: output, errorCode: out var errorCode);
            Assert.Null(options);
            Assert.Equal(0, errorCode);

            Assert.Empty(_testReporter.Messages);
            Assert.Contains("Usage:", output.ToString());
        }

        [Theory]
        [InlineData("P=V", "P", "V")]
        [InlineData("P==", "P", "=")]
        [InlineData("P=A=B", "P", "A=B")]
        [InlineData(" P\t = V ", "P", " V ")]
        [InlineData("P=", "P", "")]
        public void BuildProperties_Valid(string argValue, string name, string value)
        {
            var options = VerifyOptions(["--property", argValue]);
            Assert.Equal([(name, value)], options.BuildProperties);
        }

        [Theory]
        [InlineData("P")]
        [InlineData("=P3")]
        [InlineData("=")]
        [InlineData("==")]
        public void BuildProperties_Invalid(string value)
        {
            var options = VerifyOptions(["--property", value]);
            Assert.Empty(options.BuildProperties);
        }

        [Fact]
        public void ImplicitCommand()
        {
            var options = VerifyOptions([]);
            Assert.Equal("run", options.Command);
            Assert.Empty(options.CommandArguments);
        }

        [Theory]
        [InlineData("add")]
        [InlineData("build")]
        [InlineData("build-server")]
        [InlineData("clean")]
        [InlineData("format")]
        [InlineData("help")]
        [InlineData("list")]
        [InlineData("msbuild")]
        [InlineData("new")]
        [InlineData("nuget")]
        [InlineData("pack")]
        [InlineData("publish")]
        [InlineData("remove")]
        [InlineData("restore")]
        [InlineData("run")]
        [InlineData("sdk")]
        [InlineData("solution")]
        [InlineData("store")]
        [InlineData("test")]
        [InlineData("tool")]
        [InlineData("vstest")]
        [InlineData("workload")]
        public void ExplicitCommand(string command)
        {
            var options = VerifyOptions([command]);
            Assert.Equal(command, options.ExplicitCommand);
            Assert.Equal(command, options.Command);
            Assert.Empty(options.CommandArguments);
        }

        [Theory]
        [CombinatorialData]
        public void WatchOptions_NotPassedThrough_BeforeCommand(
            [CombinatorialValues("--quiet", "--verbose", "--no-hot-reload", "--non-interactive")] string option,
            bool before)
        {
            var options = VerifyOptions(before ? [option, "test"] : ["test", option]);
            Assert.Equal("test", options.Command);
            Assert.Empty(options.CommandArguments);
        }

        [Fact]
        public void RunOptions_LaunchProfile_Watch()
        {
            var options = VerifyOptions(["-lp", "P", "run"]);
            Assert.Equal("P", options.LaunchProfileName);
            Assert.Equal("run", options.Command);
            Assert.Equal(["-lp", "P"], options.CommandArguments);
        }

        [Fact]
        public void RunOptions_LaunchProfile_Run()
        {
            var options = VerifyOptions(["run", "-lp", "P"]);
            Assert.Equal("P", options.LaunchProfileName);
            Assert.Equal("run", options.Command);
            Assert.Equal(["-lp", "P"], options.CommandArguments);
        }

        [Fact]
        public void RunOptions_LaunchProfile_Both()
        {
            VerifyErrors(["-lp", "P1", "run", "-lp", "P2"],
                "error ❌ Option '-lp' expects a single argument but 2 were provided.");
        }

        [Fact]
        public void RunOptions_NoProfile_Watch()
        {
            var options = VerifyOptions(["--no-launch-profile", "run"]);

            Assert.True(options.NoLaunchProfile);
            Assert.Equal("run", options.Command);
            Assert.Equal(["--no-launch-profile"], options.CommandArguments);
        }

        [Fact]
        public void RunOptions_NoProfile_Run()
        {
            var options = VerifyOptions(["run", "--no-launch-profile"]);

            Assert.True(options.NoLaunchProfile);
            Assert.Equal("run", options.Command);
            Assert.Equal(["--no-launch-profile"], options.CommandArguments);
        }

        [Fact]
        public void RunOptions_NoProfile_Both()
        {
            var options = VerifyOptions(["--no-launch-profile", "run", "--no-launch-profile"]);

            Assert.True(options.NoLaunchProfile);
            Assert.Equal("run", options.Command);
            Assert.Equal(["--no-launch-profile"], options.CommandArguments);
        }

        [Fact]
        public void RemainingOptions()
        {
            var options = VerifyOptions(["-watchArg", "--verbose", "run", "-runArg"]);

            Assert.True(options.GlobalOptions.Verbose);
            Assert.Equal("run", options.Command);
            Assert.Equal(["-watchArg", "-runArg"], options.CommandArguments);
        }

        [Fact]
        public void UnknownOption()
        {
            var options = VerifyOptions(["--verbose", "--unknown", "x", "y", "run", "--project", "p"]);

            Assert.Equal("p", options.ProjectPath);
            Assert.Equal("run", options.Command);
            Assert.Equal(["--project", "p", "--unknown", "x", "y"], options.CommandArguments);
        }

        [Fact]
        public void RemainingOptionsDashDash()
        {
            var options = VerifyOptions(["-watchArg", "--", "--verbose", "run", "-runArg"]);

            Assert.False(options.GlobalOptions.Verbose);
            Assert.Equal("run", options.Command);
            Assert.Equal(["-watchArg", "--", "--verbose", "run", "-runArg"], options.CommandArguments);
        }

        [Fact]
        public void RemainingOptionsDashDashRun()
        {
            var options = VerifyOptions(["--", "run"]);

            Assert.False(options.GlobalOptions.Verbose);
            Assert.Equal("run", options.Command);
            Assert.Equal(["--", "run"], options.CommandArguments);
        }

        [Fact]
        public void NoOptionsAfterDashDash()
        {
            var options = VerifyOptions(["--"]);
            Assert.Equal("run", options.Command);
            Assert.Empty(options.CommandArguments);
        }

        /// <summary>
        /// dotnet watch needs to understand some options that are passed to the subcommands.
        /// For example, `-f TFM`
        /// When `dotnet watch run -- -f TFM` is parsed `-f TFM` is ignored.
        /// Therfore, it has to also be ignored by `dotnet run`,
        /// otherwise the TFMs would be inconsistent between `dotnet watch` and `dotnet run`.
        /// </summary>
        [Fact]
        public void ParsedNonWatchOptionsAfterDashDash_Framework()
        {
            var options = VerifyOptions(["--", "-f", "TFM"]);

            Assert.Null(options.TargetFramework);
            Assert.Equal(["--", "-f", "TFM"], options.CommandArguments);
        }

        [Fact]
        public void ParsedNonWatchOptionsAfterDashDash_Project()
        {
            var options = VerifyOptions(["--", "--project", "proj"]);

            Assert.Null(options.ProjectPath);
            Assert.Equal(["--", "--project", "proj"], options.CommandArguments);
        }

        [Fact]
        public void ParsedNonWatchOptionsAfterDashDash_NoLaunchProfile()
        {
            var options = VerifyOptions(["--", "--no-launch-profile"]);

            Assert.False(options.NoLaunchProfile);
            Assert.Equal(["--", "--no-launch-profile"], options.CommandArguments);
        }

        [Fact]
        public void ParsedNonWatchOptionsAfterDashDash_LaunchProfile()
        {
            var options = VerifyOptions(["--", "--launch-profile", "p"]);

            Assert.False(options.NoLaunchProfile);
            Assert.Equal(["--", "--launch-profile", "p"], options.CommandArguments);
        }

        [Fact]
        public void ParsedNonWatchOptionsAfterDashDash_Property()
        {
            var options = VerifyOptions(["--", "--property", "x=1"]);

            Assert.False(options.NoLaunchProfile);
            Assert.Equal(["--", "--property", "x=1"], options.CommandArguments);
        }

        [Theory]
        [CombinatorialData]
        public void OptionsSpecifiedBeforeOrAfterRun(bool afterRun)
        {
            var args = new[] { "--project", "P", "--framework", "F", "--property", "P1=V1", "--property", "P2=V2" };
            args = afterRun ? args.Prepend("run").ToArray() : args.Append("run").ToArray();

            var options = VerifyOptions(args);

            Assert.Equal("P", options.ProjectPath);
            Assert.Equal("F", options.TargetFramework);
            Assert.Equal([("P1", "V1"), ("P2", "V2")], options.BuildProperties);

            Assert.Equal(["--project", "P", "--framework", "F", "--property", "P1=V1", "--property", "P2=V2"], options.CommandArguments);
        }

        public enum ArgPosition
        {
            Before,
            After,
            Both
        }

        [Theory]
        [CombinatorialData]
        public void OptionDuplicates_Allowed_Bool(
            ArgPosition position,
            [CombinatorialValues(
                "--verbose",
                "--quiet",
                "--list",
                "--no-hot-reload",
                "--non-interactive")]
            string arg)
        {
            var args = new[] { arg };

            args = position switch
            {
                ArgPosition.Before => args.Prepend("run").ToArray(),
                ArgPosition.Both => args.Concat(new[] { "run" }).Concat(args).ToArray(),
                ArgPosition.After => args.Append("run").ToArray(),
                _ => args,
            };

            var options = VerifyOptions(args);

            Assert.True(arg switch
            {
                "--verbose" => options.GlobalOptions.Verbose,
                "--quiet" => options.GlobalOptions.Quiet,
                "--list" => options.List,
                "--no-hot-reload" => options.GlobalOptions.NoHotReload,
                "--non-interactive" => options.GlobalOptions.NonInteractive,
                _ => false
            });
        }

        [Fact]
        public void MultiplePropertyValues()
        {
            var options = VerifyOptions(["--property", "P1=V1", "run", "--property", "P2=V2"]);
            AssertEx.SequenceEqual(["P1=V1", "P2=V2"], options.BuildProperties.Select(p => $"{p.name}={p.value}"));

            // options must be repeated since --property does not support multiple args
            AssertEx.SequenceEqual(["--property", "P1=V1", "--property", "P2=V2"], options.CommandArguments);
        }

        [Theory]
        [InlineData("--project")]
        [InlineData("--framework")]
        public void OptionDuplicates_NotAllowed(string option)
        {
            VerifyErrors([option, "abc", "run", option, "xyz"],
                $"error ❌ Option '{option}' expects a single argument but 2 were provided.");
        }

        [Theory]
        [InlineData(new[] { "--unrecognized-arg" }, new[] { "--unrecognized-arg" })]
        [InlineData(new[] { "run" }, new string[] { })]
        [InlineData(new[] { "run", "--", "runarg" }, new[] { "--", "runarg" })]
        [InlineData(new[] { "--verbose", "run", "runarg1", "-runarg2" }, new[] { "runarg1", "-runarg2" })]
        // run is after -- and therefore not parsed as a command:
        [InlineData(new[] { "--verbose", "--", "run", "--", "runarg" }, new[] { "--", "run", "--", "runarg" })]
        // run is before -- and therefore parsed as a command:
        [InlineData(new[] { "--verbose", "run", "--", "--", "runarg" }, new[] { "--", "--", "runarg" })]
        public void ParsesRemainingArgs(string[] args, string[] expected)
        {
            var options = VerifyOptions(args);
            Assert.Equal(expected, options.CommandArguments);
        }

        [Fact]
        public void CannotHaveQuietAndVerbose()
        {
            VerifyErrors(["--quiet", "--verbose"],
                $"error ❌ {Resources.Error_QuietAndVerboseSpecified}");
        }

        [Fact]
        public void ShortFormForProjectArgumentPrintsWarning()
        {
            var options = VerifyOptions(["-p", "MyProject.csproj"],
                expectedMessages: [$"warning ⌚ {Resources.Warning_ProjectAbbreviationDeprecated}"]);

            Assert.Equal("MyProject.csproj", options.ProjectPath);
        }

        [Fact]
        public void LongFormForProjectArgumentWorks()
        {
            var options = VerifyOptions(["--project", "MyProject.csproj"]);
            Assert.Equal("MyProject.csproj", options.ProjectPath);
        }

        [Fact]
        public void LongFormForLaunchProfileArgumentWorks()
        {
            var options = VerifyOptions(["--launch-profile", "CustomLaunchProfile"]);
            Assert.NotNull(options);
            Assert.Equal("CustomLaunchProfile", options.LaunchProfileName);
        }

        [Fact]
        public void ShortFormForLaunchProfileArgumentWorks()
        {
            var options = VerifyOptions(["-lp", "CustomLaunchProfile"]);
            Assert.Equal("CustomLaunchProfile", options.LaunchProfileName);
        }
    }
}
