// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class CommandLineOptionsTests
    {
        private readonly TestLogger _testLogger = new();

        private CommandLineOptions VerifyOptions(string[] args, string expectedOutput = "", string[] expectedMessages = null)
            => VerifyOptions(args, actualOutput => AssertEx.Equal(expectedOutput, actualOutput), expectedMessages ?? []);

        private CommandLineOptions VerifyOptions(string[] args, Action<string> outputValidator, string[] expectedMessages)
        {
            var output = new StringWriter();
            var options = CommandLineOptions.Parse(args, _testLogger, output: output, errorCode: out var errorCode);

            Assert.Equal(expectedMessages, _testLogger.GetAndClearMessages());
            outputValidator(output.ToString());

            Assert.NotNull(options);
            Assert.Equal(0, errorCode);
            return options;
        }

        private void VerifyErrors(string[] args, params string[] expectedErrors)
        {
            var output = new StringWriter();
            var options = CommandLineOptions.Parse(args, _testLogger, output: output, errorCode: out var errorCode);

            AssertEx.Equal(expectedErrors, _testLogger.GetAndClearMessages());
            Assert.Empty(output.ToString());

            Assert.Null(options);
            Assert.NotEqual(0, errorCode);
        }

        [Theory]
        [InlineData([new[] { "-h" }])]
        [InlineData([new[] { "-?" }])]
        [InlineData([new[] { "--help" }])]
        [InlineData([new[] { "--help", "--bogus" }])]
        public void HelpArgs(string[] args)
        {
            var output = new StringWriter();
            var options = CommandLineOptions.Parse(args, _testLogger, output: output, errorCode: out var errorCode);
            Assert.Null(options);
            Assert.Equal(0, errorCode);

            Assert.Empty(_testLogger.GetAndClearMessages());
            Assert.Contains("Usage:", output.ToString());
        }

        [Theory]
        [InlineData("-p:P=V", "P", "V")]
        [InlineData("-p:P==", "P", "=")]
        [InlineData("-p:P=A=B", "P", "A=B")]
        [InlineData("-p: P\t = V ", "P", " V ")]
        [InlineData("-p:P=", "P", "")]
        public void BuildProperties_Valid(string argValue, string name, string value)
        {
            var properties = CommandLineOptions.ParseBuildProperties([argValue]);
            AssertEx.SequenceEqual([(name, value)], properties);
        }

        [Theory]
        [InlineData("P")]
        [InlineData("=P3")]
        [InlineData("=")]
        [InlineData("==")]
        public void BuildProperties_Invalid(string argValue)
        {
            var properties = CommandLineOptions.ParseBuildProperties([argValue]);
            AssertEx.SequenceEqual([], properties);
        }

        [Fact]
        public void ImplicitCommand()
        {
            var options = VerifyOptions([]);
            Assert.Equal("run", options.Command);
            AssertEx.SequenceEqual([], options.CommandArguments);
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
            var args = options.CommandArguments.ToList();
            Assert.Equal(command, options.ExplicitCommand);
            Assert.Equal(command, options.Command);
            Assert.Empty(args);
        }

        [Theory]
        [CombinatorialData]
        public void RunOptions_LaunchProfile_NotPassedThrough(bool beforeCommand)
        {
            var options = VerifyOptions(beforeCommand ? ["--launch-profile", "p", "test"] : ["test", "--launch-profile", "p"]);
            Assert.Equal("test", options.Command);
            AssertEx.SequenceEqual([], options.CommandArguments);
        }

        [Theory]
        [CombinatorialData]
        public void RunOptions_NoLaunchProfile_NotPassedThrough(bool beforeCommand)
        {
            var options = VerifyOptions(beforeCommand ? ["--no-launch-profile", "test"] : ["test", "--no-launch-profile"]);
            Assert.Equal("test", options.Command);
            AssertEx.SequenceEqual([], options.CommandArguments);
        }

        [Theory]
        [CombinatorialData]
        public void RunOption_Project_NotPassedThrough(bool beforeCommand)
        {
            var options = VerifyOptions(beforeCommand ? ["--project", "MyProject.csproj", "test"] : ["test", "--project", "MyProject.csproj"]);
            Assert.Equal("test", options.Command);
            AssertEx.SequenceEqual([], options.CommandArguments);
        }

        [Theory]
        [CombinatorialData]
        public void WatchOptions_NotPassedThrough(
            [CombinatorialValues("--quiet", "--verbose", "--no-hot-reload", "--non-interactive")] string option,
            bool beforeCommand)
        {
            var options = VerifyOptions(beforeCommand ? [option, "test"] : ["test", option]);
            Assert.Equal("test", options.Command);
            AssertEx.SequenceEqual([], options.CommandArguments);
        }

        [Fact]
        public void RunOptions_LaunchProfile_Watch()
        {
            var options = VerifyOptions(["-lp", "P", "run"]);
            Assert.Equal("P", options.LaunchProfileName);
            Assert.Equal("run", options.Command);
            AssertEx.SequenceEqual(["-lp", "P"], options.CommandArguments);
        }

        [Fact]
        public void RunOptions_LaunchProfile_Run()
        {
            var options = VerifyOptions(["run", "-lp", "P"]);
            Assert.Equal("P", options.LaunchProfileName);
            Assert.Equal("run", options.Command);
            AssertEx.SequenceEqual(["-lp", "P"], options.CommandArguments);
        }

        [Fact]
        public void RunOptions_LaunchProfile_Both()
        {
            VerifyErrors(["-lp", "P1", "run", "-lp", "P2"],
                "[Error] Option '-lp' expects a single argument but 2 were provided.");
        }

        [Fact]
        public void RunOptions_NoProfile_Watch()
        {
            var options = VerifyOptions(["--no-launch-profile", "run"]);

            Assert.True(options.NoLaunchProfile);
            Assert.Equal("run", options.Command);
            AssertEx.SequenceEqual(["--no-launch-profile"], options.CommandArguments);
        }

        [Fact]
        public void RunOptions_NoProfile_Run()
        {
            var options = VerifyOptions(["run", "--no-launch-profile"]);

            Assert.True(options.NoLaunchProfile);
            Assert.Equal("run", options.Command);
            AssertEx.SequenceEqual(["--no-launch-profile"], options.CommandArguments);
        }

        [Fact]
        public void RunOptions_NoProfile_Both()
        {
            var options = VerifyOptions(["--no-launch-profile", "run", "--no-launch-profile"]);

            Assert.True(options.NoLaunchProfile);
            Assert.Equal("run", options.Command);
            AssertEx.SequenceEqual(["--no-launch-profile"], options.CommandArguments);
        }

        [Fact]
        public void RemainingOptions()
        {
            var options = VerifyOptions(["-watchArg", "--verbose", "run", "-runArg"]);

            Assert.True(options.GlobalOptions.Verbose);
            Assert.Equal("run", options.Command);
            AssertEx.SequenceEqual(["-watchArg", "-runArg"], options.CommandArguments);
        }

        [Fact]
        public void UnknownOption()
        {
            var options = VerifyOptions(["--verbose", "--unknown", "x", "y", "run", "--project", "p"]);

            Assert.Equal("p", options.ProjectPath);
            Assert.Equal("run", options.Command);
            AssertEx.SequenceEqual(["--project", "p", "--unknown", "x", "y"], options.CommandArguments);
        }

        [Fact]
        public void RemainingOptionsDashDash()
        {
            var options = VerifyOptions(["-watchArg", "--", "--verbose", "run", "-runArg"]);

            Assert.False(options.GlobalOptions.Verbose);
            Assert.Equal("run", options.Command);
            AssertEx.SequenceEqual(["-watchArg", "--", "--verbose", "run", "-runArg",], options.CommandArguments);
        }

        [Fact]
        public void RemainingOptionsDashDashRun()
        {
            var options = VerifyOptions(["--", "run"]);

            Assert.False(options.GlobalOptions.Verbose);
            Assert.Equal("run", options.Command);
            AssertEx.SequenceEqual(["--", "run"], options.CommandArguments);
        }

        [Fact]
        public void NoOptionsAfterDashDash()
        {
            var options = VerifyOptions(["--"]);
            Assert.Equal("run", options.Command);
            AssertEx.SequenceEqual([], options.CommandArguments);
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
            AssertEx.SequenceEqual(["--", "-f", "TFM"], options.CommandArguments);
        }

        [Fact]
        public void ParsedNonWatchOptionsAfterDashDash_Project()
        {
            var options = VerifyOptions(["--", "--project", "proj"]);

            Assert.Null(options.ProjectPath);
            AssertEx.SequenceEqual(["--", "--project", "proj"], options.CommandArguments);
        }

        [Fact]
        public void ParsedNonWatchOptionsAfterDashDash_NoLaunchProfile()
        {
            var options = VerifyOptions(["--", "--no-launch-profile"]);

            Assert.False(options.NoLaunchProfile);
            AssertEx.SequenceEqual(["--", "--no-launch-profile"], options.CommandArguments);
        }

        [Fact]
        public void ParsedNonWatchOptionsAfterDashDash_LaunchProfile()
        {
            var options = VerifyOptions(["--", "--launch-profile", "p"]);

            Assert.False(options.NoLaunchProfile);
            AssertEx.SequenceEqual(["--", "--launch-profile", "p"], options.CommandArguments);
        }

        [Fact]
        public void ParsedNonWatchOptionsAfterDashDash_Property()
        {
            var options = VerifyOptions(["--", "--property", "x=1"]);

            Assert.False(options.NoLaunchProfile);
            AssertEx.SequenceEqual(["--", "--property", "x=1"], options.CommandArguments);
        }

        [Theory]
        [InlineData("-bl")]
        [InlineData("/bl")]
        [InlineData("/bl:X.binlog")]
        [InlineData("-binaryLogger")]
        [InlineData("/binaryLogger")]
        [InlineData("--binaryLogger:LogFile=output.binlog;ProjectImports=None")]
        public void BinaryLoggerOption_AfterDashDash(string option)
        {
            var options1 = VerifyOptions(["--", option]);

            AssertEx.SequenceEqual(["--", option], options1.CommandArguments);
            AssertEx.SequenceEqual(["--property:NuGetInteractive=false"], options1.BuildArguments);

            var options2 = VerifyOptions([option, "A", "--", "-bl:XXX"]);

            AssertEx.SequenceEqual([option, "A", "--", "-bl:XXX"], options2.CommandArguments);
            AssertEx.SequenceEqual(["--property:NuGetInteractive=false", option], options2.BuildArguments);
        }

        [Theory]
        [InlineData("-bl:")]
        [InlineData("/bl:")]
        [InlineData("-binaryLogger:")]
        [InlineData("/binaryLogger:")]
        public void BinaryLoggerOption_NoValue(string option)
        {
            var options = VerifyOptions([option]);

            AssertEx.SequenceEqual([option], options.CommandArguments);
            AssertEx.SequenceEqual(["--property:NuGetInteractive=false"], options.BuildArguments);
        }

        [Theory]
        [CombinatorialData]
        public void OptionsSpecifiedBeforeOrAfterRun(bool afterRun)
        {
            var args = new[] { "--project", "P", "--framework", "F", "--property", "P1=V1", "--property", "P2=V2" };
            args = afterRun ? [.. args.Prepend("run")] : [.. args.Append("run")];

            var options = VerifyOptions(args);

            Assert.Equal("P", options.ProjectPath);
            Assert.Equal("F", options.TargetFramework);

            // the forwarding function of --property property joins the properties with `:`:
            AssertEx.SequenceEqual(["--property:TargetFramework=F", "--property:P1=V1", "--property:P2=V2", NugetInteractiveProperty], options.BuildArguments);

            // it's ok to keep the two arguments and not to join them with `:` since `run` command handles these options correctly
            AssertEx.SequenceEqual(["--project", "P", "--framework", "F", "--property", "P1=V1", "--property", "P2=V2"], options.CommandArguments);
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
            AssertEx.SequenceEqual(["--property:P1=V1", "--property:P2=V2", NugetInteractiveProperty], options.BuildArguments);

            // options must be repeated since --property does not support multiple args
            AssertEx.SequenceEqual(["--property", "P1=V1", "--property", "P2=V2"], options.CommandArguments);
        }

        [Theory]
        [InlineData("--project")]
        [InlineData("--framework")]
        public void OptionDuplicates_NotAllowed(string option)
        {
            VerifyErrors([option, "abc", "run", option, "xyz"],
                $"[Error] Option '{option}' expects a single argument but 2 were provided.");
        }

        [Theory]
        [InlineData(new[] { "--unrecognized-arg" }, new[] { "--unrecognized-arg" })]
        [InlineData(new[] { "run" }, new string[] { })]
        [InlineData(new[] { "run", "--", "runarg" }, new[] {  "--", "runarg" })]
        [InlineData(new[] { "--verbose", "run", "runarg1", "-runarg2" }, new[] {  "runarg1", "-runarg2" })]
        // run is after -- and therefore not parsed as a command:
        [InlineData(new[] { "--verbose", "--", "run", "--", "runarg" }, new[] {  "--", "run", "--", "runarg" })]
        // run is before -- and therefore parsed as a command:
        [InlineData(new[] { "--verbose", "run", "--", "--", "runarg" }, new[] {  "--", "--", "runarg" })]
        public void ParsesRemainingArgs(string[] args, string[] expected)
        {
            var options = VerifyOptions(args);
            Assert.Equal(expected, options.CommandArguments);
        }

        [Fact]
        public void CannotHaveQuietAndVerbose()
        {
            VerifyErrors(["--quiet", "--verbose"],
                $"[Error] {Resources.Error_QuietAndVerboseSpecified}");
        }

        [Fact]
        public void ShortFormForProjectArgumentPrintsWarning()
        {
            var options = VerifyOptions(["-p", "MyProject.csproj"],
                expectedMessages: [$"[Warning] {Resources.Warning_ProjectAbbreviationDeprecated}"]);

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

        private const string NugetInteractiveProperty = "--property:NuGetInteractive=false";

        /// <summary>
        /// Validates that options that the "run" command forwards to "build" command are forwarded by dotnet-watch.
        /// </summary>
        [Theory]
        [InlineData(new[] { "--configuration", "release" }, new[] { "--property:Configuration=release", NugetInteractiveProperty })]
        [InlineData(new[] { "--framework", "net9.0" }, new[] { "--property:TargetFramework=net9.0", NugetInteractiveProperty })]
        [InlineData(new[] { "--runtime", "arm64" }, new[] { "--property:RuntimeIdentifier=arm64", "--property:_CommandLineDefinedRuntimeIdentifier=true", NugetInteractiveProperty })]
        [InlineData(new[] { "--property", "b=1" }, new[] { "--property:b=1", NugetInteractiveProperty })]
        [InlineData(new[] { "/p:b=1" }, new[] { "--property:b=1", NugetInteractiveProperty }, new[] { "/p", "b=1" })] // it's ok to split the argument into two since `dotnet run` handles `/p b=1`
        [InlineData(new[] { "--interactive" }, new[] { "--property:NuGetInteractive=true" })]
        [InlineData(new[] { "--no-restore" }, new[] { NugetInteractiveProperty, "-restore:false" })]
        [InlineData(new[] { "--sc" }, new[] { NugetInteractiveProperty, "--property:SelfContained=true", "--property:_CommandLineDefinedSelfContained=true" })]
        [InlineData(new[] { "--self-contained" }, new[] { NugetInteractiveProperty, "--property:SelfContained=true", "--property:_CommandLineDefinedSelfContained=true" })]
        [InlineData(new[] { "--no-self-contained" }, new[] { NugetInteractiveProperty, "--property:SelfContained=false", "--property:_CommandLineDefinedSelfContained=true" })]
        [InlineData(new[] { "--verbosity", "q" }, new[] { NugetInteractiveProperty, "--verbosity:q" })]
        [InlineData(new[] { "--arch", "arm", "--os", "win" }, new[] { NugetInteractiveProperty, "--property:RuntimeIdentifier=win-arm" })]
        [InlineData(new[] { "--disable-build-servers" }, new[] { NugetInteractiveProperty, "--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false" })]
        [InlineData(new[] { "-bl" }, new[] { NugetInteractiveProperty, "-bl" })]
        [InlineData(new[] { "/bl" }, new[] { NugetInteractiveProperty, "/bl" })]
        [InlineData(new[] { "/bl:X.binlog" }, new[] { NugetInteractiveProperty, "/bl:X.binlog" })]
        [InlineData(new[] { "-binaryLogger" }, new[] { NugetInteractiveProperty, "-binaryLogger" })]
        [InlineData(new[] { "/binaryLogger" }, new[] { NugetInteractiveProperty, "/binaryLogger" })]
        [InlineData(new[] { "--binaryLogger:LogFile=output.binlog;ProjectImports=None" }, new[] { NugetInteractiveProperty, "--binaryLogger:LogFile=output.binlog;ProjectImports=None" })]
        public void ForwardedBuildOptions_Run(string[] args, string[] buildArgs, string[] commandArgs = null)
        {
            var runOptions = VerifyOptions(["run", .. args]);
            AssertEx.SequenceEqual(buildArgs, runOptions.BuildArguments);
            AssertEx.SequenceEqual(commandArgs ?? args, runOptions.CommandArguments);
        }

        [Theory]
        [InlineData(new[] { "--property:b=1" }, new[] { "--property:b=1" }, Skip = "https://github.com/dotnet/sdk/issues/44655")]
        [InlineData(new[] { "--property", "b=1" }, new[] { "--property", "b=1" }, Skip = "https://github.com/dotnet/sdk/issues/44655")]
        [InlineData(new[] { "/p:b=1" }, new[] { "/p:b=1" }, Skip = "https://github.com/dotnet/sdk/issues/44655")]
        [InlineData(new[] { "/bl" }, new[] { "/bl" })]
        [InlineData(new[] { "--binaryLogger:LogFile=output.binlog;ProjectImports=None" }, new[] { "--binaryLogger:LogFile=output.binlog;ProjectImports=None" })]
        public void ForwardedBuildOptions_Test(string[] args, string[] commandArgs)
        {
            var runOptions = VerifyOptions(["test", .. args]);
            AssertEx.SequenceEqual(["--property:NuGetInteractive=false", "--target:VSTest", .. commandArgs], runOptions.BuildArguments);
            AssertEx.SequenceEqual(commandArgs, runOptions.CommandArguments);
        }

        [Fact]
        public void ForwardedBuildOptions_ArtifactsPath()
        {
            var path = TestContext.Current.TestAssetsDirectory;

            var args = new[] { "--artifacts-path", path };
            var buildArgs = new[] { NugetInteractiveProperty, @"--property:ArtifactsPath=" + path };

            var options = VerifyOptions(["run", .. args]);
            AssertEx.SequenceEqual(buildArgs, options.BuildArguments);
        }
    }
}
