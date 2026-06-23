// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class CommandLineOptionsTests
{
    private readonly TestLogger _testLogger = new();

    private CommandLineOptions VerifyOptions(string[] args, string expectedOutput = "", string[] expectedMessages = null)
        => VerifyOptions(args, actualOutput => AssertEx.Equal(expectedOutput, actualOutput), expectedMessages ?? []);

    private CommandLineOptions VerifyOptions(string[] args, Action<string> outputValidator, string[] expectedMessages)
    {
        var output = new StringWriter();
        var options = CommandLineOptions.Parse(args, _testLogger, output: output, errorCode: out var errorCode);

        AssertEx.SequenceEqual(expectedMessages, _testLogger.GetAndClearMessages());
        outputValidator(output.ToString());

        Assert.IsNotNull(options);
        Assert.AreEqual(0, errorCode);
        return options;
    }

    private void VerifyErrors(string[] args, params string[] expectedErrors)
    {
        var output = new StringWriter();
        var options = CommandLineOptions.Parse(args, _testLogger, output: output, errorCode: out var errorCode);

        AssertEx.Equal(expectedErrors, _testLogger.GetAndClearMessages());
        Assert.IsEmpty(output.ToString());

        Assert.IsNull(options);
        Assert.AreNotEqual(0, errorCode);
    }

    [TestMethod]
    [DataRow([new[] { "-h" }])]
    [DataRow([new[] { "-?" }])]
    [DataRow([new[] { "--help" }])]
    [DataRow([new[] { "--help", "--bogus" }])]
    public void HelpArgs(string[] args)
    {
        var output = new StringWriter();
        var options = CommandLineOptions.Parse(args, _testLogger, output: output, errorCode: out var errorCode);
        Assert.IsNull(options);
        Assert.AreEqual(0, errorCode);

        Assert.IsEmpty(_testLogger.GetAndClearMessages());
        Assert.Contains("Usage:", output.ToString());
    }

    [TestMethod]
    public void ImplicitCommand()
    {
        var options = VerifyOptions([]);
        Assert.AreEqual("run", options.Command.Name);
        AssertEx.SequenceEqual([], options.CommandArguments);
    }

    [TestMethod]
    [DataRow("add")]
    [DataRow("build")]
    [DataRow("build-server")]
    [DataRow("clean")]
    [DataRow("format")]
    [DataRow("help")]
    [DataRow("list")]
    [DataRow("msbuild")]
    [DataRow("new")]
    [DataRow("nuget")]
    [DataRow("pack")]
    [DataRow("publish")]
    [DataRow("remove")]
    [DataRow("restore")]
    [DataRow("run")]
    [DataRow("sdk")]
    [DataRow("solution")]
    [DataRow("store")]
    [DataRow("test")]
    [DataRow("tool")]
    [DataRow("vstest")]
    [DataRow("workload")]
    public void ExplicitCommand(string command)
    {
        var options = VerifyOptions([command]);
        var args = options.CommandArguments.ToList();
        Assert.IsTrue(options.IsExplicitCommand);
        Assert.AreEqual(command, options.Command.Name);
        Assert.IsEmpty(args);
    }

    [TestMethod]

    [CombinatorialData]
    public void WatchOptions_NotPassedThrough(
        [CombinatorialValues("--quiet", "--verbose", "--no-hot-reload", "--non-interactive")] string option,
        bool beforeCommand)
    {
        var options = VerifyOptions(beforeCommand ? [option, "test"] : ["test", option]);
        Assert.AreEqual("test", options.Command.Name);
        AssertEx.SequenceEqual([], options.CommandArguments);
    }

    [TestMethod]
    public void QuietAndVerbose()
    {
         VerifyErrors(["--quiet", "--verbose"],
            expectedErrors: [$"[Error] {string.Format(Resources.Cannot_specify_both_0_and_1_options, "--quiet", "--verbose")}"]);
    }

    [TestMethod]
    public void RunOptions_LaunchProfile_Watch()
    {
        var options = VerifyOptions(["-lp", "P", "run"]);
        Assert.AreEqual("P", options.LaunchProfileName);
        Assert.AreEqual("run", options.Command.Name);
        AssertEx.SequenceEqual(["-lp", "P"], options.CommandArguments);
    }

    [TestMethod]
    public void RunOptions_LaunchProfile_Run()
    {
        var options = VerifyOptions(["run", "-lp", "P"]);
        Assert.AreEqual("P", options.LaunchProfileName);
        Assert.AreEqual("run", options.Command.Name);
        AssertEx.SequenceEqual(["-lp", "P"], options.CommandArguments);
    }

    [TestMethod]
    public void RunOptions_LaunchProfile_Both()
    {
        VerifyErrors(["-lp", "P1", "run", "-lp", "P2"],
            "[Error] Option '-lp' expects a single argument but 2 were provided.");
    }

    [TestMethod]
    public void RunOptions_NoProfile_Watch()
    {
        var options = VerifyOptions(["--no-launch-profile", "run"]);

        Assert.IsFalse(options.LaunchProfileName.HasValue);
        Assert.AreEqual("run", options.Command.Name);
        AssertEx.SequenceEqual(["--no-launch-profile"], options.CommandArguments);
    }

    [TestMethod]
    public void RunOptions_NoProfile_Run()
    {
        var options = VerifyOptions(["run", "--no-launch-profile"]);

        Assert.IsFalse(options.LaunchProfileName.HasValue);
        Assert.AreEqual("run", options.Command.Name);
        AssertEx.SequenceEqual(["--no-launch-profile"], options.CommandArguments);
    }

    [TestMethod]
    public void RunOptions_NoProfile_Both()
    {
        var options = VerifyOptions(["--no-launch-profile", "run", "--no-launch-profile"]);

        Assert.IsFalse(options.LaunchProfileName.HasValue);
        Assert.AreEqual("run", options.Command.Name);
        AssertEx.SequenceEqual(["--no-launch-profile"], options.CommandArguments);
    }

    [TestMethod]
    public void RemainingOptions()
    {
        var options = VerifyOptions(["-watchArg", "--verbose", "run", "-runArg"]);

        Assert.AreEqual(LogLevel.Debug, options.GlobalOptions.LogLevel);
        Assert.AreEqual("run", options.Command.Name);
        AssertEx.SequenceEqual(["-watchArg", "-runArg"], options.CommandArguments);
    }

    [TestMethod]
    public void UnknownOption()
    {
        var options = VerifyOptions(["--verbose", "--unknown", "x", "y", "run", "--project", "p"]);

        Assert.AreEqual("p", options.ProjectPath);
        Assert.AreEqual("run", options.Command.Name);
        AssertEx.SequenceEqual(["--project", "p", "--unknown", "x", "y"], options.CommandArguments);
    }

    [TestMethod]
    public void RemainingOptionsDashDash()
    {
        var options = VerifyOptions(["-watchArg", "--", "--verbose", "run", "-runArg"]);

        Assert.AreEqual(LogLevel.Information, options.GlobalOptions.LogLevel);
        Assert.AreEqual("run", options.Command.Name);
        AssertEx.SequenceEqual(["-watchArg", "--", "--verbose", "run", "-runArg",], options.CommandArguments);
    }

    [TestMethod]
    public void RemainingOptionsDashDashRun()
    {
        var options = VerifyOptions(["--", "run"]);

        Assert.AreEqual(LogLevel.Information, options.GlobalOptions.LogLevel);
        Assert.AreEqual("run", options.Command.Name);
        AssertEx.SequenceEqual(["--", "run"], options.CommandArguments);
    }

    [TestMethod]
    public void NoOptionsAfterDashDash()
    {
        var options = VerifyOptions(["--"]);
        Assert.AreEqual("run", options.Command.Name);
        AssertEx.SequenceEqual([], options.CommandArguments);
    }

    /// <summary>
    /// dotnet watch needs to understand some options that are passed to the subcommands.
    /// For example, `-f TFM`
    /// When `dotnet watch run -- -f TFM` is parsed `-f TFM` is ignored.
    /// Therfore, it has to also be ignored by `dotnet run`,
    /// otherwise the TFMs would be inconsistent between `dotnet watch` and `dotnet run`.
    /// </summary>
    [TestMethod]
    public void ParsedNonWatchOptionsAfterDashDash_Framework()
    {
        var options = VerifyOptions(["--", "-f", "TFM"]);

        Assert.IsNull(options.TargetFramework);
        AssertEx.SequenceEqual(["--", "-f", "TFM"], options.CommandArguments);
    }

    [TestMethod]
    public void ParsedNonWatchOptionsAfterDashDash_Project()
    {
        var options = VerifyOptions(["--", "--project", "proj"]);

        Assert.IsNull(options.ProjectPath);
        AssertEx.SequenceEqual(["--", "--project", "proj"], options.CommandArguments);
    }

    [TestMethod]
    public void ParsedNonWatchOptionsAfterDashDash_NoLaunchProfile()
    {
        var options = VerifyOptions(["--", "--no-launch-profile"]);

        Assert.IsTrue(options.LaunchProfileName.HasValue);
        Assert.IsNull(options.LaunchProfileName.Value);
        AssertEx.SequenceEqual(["--", "--no-launch-profile"], options.CommandArguments);
    }

    [TestMethod]
    public void ParsedNonWatchOptionsAfterDashDash_LaunchProfile()
    {
        var options = VerifyOptions(["--", "--launch-profile", "p"]);

        Assert.IsTrue(options.LaunchProfileName.HasValue);
        Assert.IsNull(options.LaunchProfileName.Value);
        AssertEx.SequenceEqual(["--", "--launch-profile", "p"], options.CommandArguments);
    }

    [TestMethod]
    public void ParsedNonWatchOptionsAfterDashDash_Property()
    {
        var options = VerifyOptions(["--", "--property", "x=1"]);

        Assert.IsTrue(options.LaunchProfileName.HasValue);
        Assert.IsNull(options.LaunchProfileName.Value);
        AssertEx.SequenceEqual(["--", "--property", "x=1"], options.CommandArguments);
    }

    [TestMethod]
    [DataRow("-bl")]
    [DataRow("/bl")]
    [DataRow("/bl:X.binlog")]
    [DataRow("-binaryLogger")]
    [DataRow("/binaryLogger")]
    [DataRow("--binaryLogger:LogFile=output.binlog;ProjectImports=None")]
    public void BinaryLoggerOption_AfterDashDash(string option)
    {
        var options1 = VerifyOptions(["--", option]);

        AssertEx.SequenceEqual(["--", option], options1.CommandArguments);
        AssertEx.SequenceEqual(["--", option], options1.CommandArgumentsWithoutBinLog);
        AssertEx.SequenceEqual(["--property:NuGetInteractive=false"], options1.BuildArguments);

        var options2 = VerifyOptions(["-bl:1", option, "A", "--", "-bl:XXX"]);

        AssertEx.SequenceEqual(["-bl:1", option, "A", "--", "-bl:XXX"], options2.CommandArguments);
        AssertEx.SequenceEqual(["A", "--", "-bl:XXX"], options2.CommandArgumentsWithoutBinLog);

        // the last bin log option before "--" is used:
        AssertEx.SequenceEqual(["--property:NuGetInteractive=false", option], options2.BuildArguments);
    }

    [TestMethod]
    [DataRow("-bl:")]
    [DataRow("/bl:")]
    [DataRow("-binaryLogger:")]
    [DataRow("/binaryLogger:")]
    public void BinaryLoggerOption_NoValue(string option)
    {
        var options = VerifyOptions([option]);

        AssertEx.SequenceEqual([option], options.CommandArguments);
        AssertEx.SequenceEqual(["--property:NuGetInteractive=false"], options.BuildArguments);
    }

    [TestMethod]

    [CombinatorialData]
    public void OptionsSpecifiedBeforeOrAfterRun(bool afterRun)
    {
        var args = new[] { "--project", "P", "--framework", "F", "--property", "P1=V1", "--property", "P2=V2" };
        args = afterRun ? [.. args.Prepend("run")] : [.. args.Append("run")];

        var options = VerifyOptions(args);

        Assert.AreEqual("P", options.ProjectPath);
        Assert.AreEqual("F", options.TargetFramework);

        // The forwarding function of --property property joins the properties with `:`
        // --framework is not forwarded as property.
        AssertEx.SequenceEqual(["--property:P1=V1", "--property:P2=V2", NugetInteractiveProperty], options.BuildArguments);

        // It's ok to keep the two arguments and not to join them with `:` since `run` command handles these options correctly
        // --framework is not forwarded, it will be specified explicitly.
        AssertEx.SequenceEqual(["--project", "P", "--property", "P1=V1", "--property", "P2=V2"], options.CommandArguments);
    }

    public enum ArgPosition
    {
        Before,
        After,
        Both
    }

    [TestMethod]

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
            ArgPosition.Before => ["run", .. args],
            ArgPosition.Both => [.. args, "run", .. args],
            ArgPosition.After => [.. args, "run"],
            _ => args,
        };

        var options = VerifyOptions(args);

        Assert.IsTrue(arg switch
        {
            "--verbose" => options.GlobalOptions.LogLevel == LogLevel.Debug,
            "--quiet" => options.GlobalOptions.LogLevel == LogLevel.Warning,
            "--list" => options.List,
            "--no-hot-reload" => options.GlobalOptions.NoHotReload,
            "--non-interactive" => options.GlobalOptions.NonInteractive,
            _ => false
        });
    }

    [TestMethod]
    public void OptionDuplicates_Property()
    {
        var options = VerifyOptions(["--property", "P1=V1", "run", "--property", "P2=V2"]);
        AssertEx.SequenceEqual(["--property:P1=V1", "--property:P2=V2", NugetInteractiveProperty], options.BuildArguments);

        // options must be repeated since --property does not support multiple args
        AssertEx.SequenceEqual(["--property", "P1=V1", "--property", "P2=V2"], options.CommandArguments);
    }

    [TestMethod]
    [DataRow("--file")]
    [DataRow("--project")]
    [DataRow("-p")]
    [DataRow("--framework")]
    public void OptionDuplicates_NotAllowed(string option)
    {
        VerifyErrors([option, "abc", "run", option, "xyz"],
            $"[Error] Option '{option}' expects a single argument but 2 were provided.");
    }

    [TestMethod]
    [DataRow(new[] { "--unrecognized-arg" }, new[] { "--unrecognized-arg" })]
    [DataRow(new[] { "run" }, new string[] { })]
    [DataRow(new[] { "run", "--", "runarg" }, new[] {  "--", "runarg" })]
    [DataRow(new[] { "--verbose", "run", "runarg1", "-runarg2" }, new[] {  "runarg1", "-runarg2" })]
    // run is after -- and therefore not parsed as a command:
    [DataRow(new[] { "--verbose", "--", "run", "--", "runarg" }, new[] {  "--", "run", "--", "runarg" })]
    // run is before -- and therefore parsed as a command:
    [DataRow(new[] { "--verbose", "run", "--", "--", "runarg" }, new[] {  "--", "--", "runarg" })]
    public void ParsesRemainingArgs(string[] args, string[] expected)
    {
        var options = VerifyOptions(args);
        AssertEx.SequenceEqual(expected, options.CommandArguments);
    }

    [TestMethod]
    public void Project_ShortForm()
    {
        var options = VerifyOptions(["-p", "MyProject.csproj"],
            expectedMessages: [$"[Warning] {Resources.Warning_ProjectAbbreviationDeprecated}"]);

        Assert.AreEqual("MyProject.csproj", options.ProjectPath);
    }

    [TestMethod]
    public void Project_ShortAndLongForm()
    {
        VerifyErrors(["-p", "MyProject1.csproj", "--project", "MyProject2.csproj"],
            expectedErrors: [$"[Error] {string.Format(Resources.Cannot_specify_both_0_and_1_options, "--project", "-p")}"]);
    }

    [TestMethod]
    [DataRow("-p")]
    [DataRow("--project")]
    public void Project_File(string projectOption)
    {
        VerifyErrors([projectOption, "MyProject1.csproj", "--file", "a.cs"],
            expectedErrors: [$"[Error] {string.Format(Resources.Cannot_specify_both_0_and_1_options, "--file", projectOption)}"]);
    }

    [TestMethod]
    public void Project_LongForm()
    {
        var options = VerifyOptions(["--project", "MyProject.csproj"]);
        Assert.AreEqual("MyProject.csproj", options.ProjectPath);
    }

    [TestMethod]
    public void File()
    {
        var options = VerifyOptions(["--file", "MyFile.cs"]);
        Assert.AreEqual("MyFile.cs", options.FilePath);
    }

    [TestMethod]
    public void LaunchProfile_LongForm()
    {
        var options = VerifyOptions(["--launch-profile", "CustomLaunchProfile"]);
        Assert.IsNotNull(options);
        Assert.AreEqual("CustomLaunchProfile", options.LaunchProfileName);
    }

    [TestMethod]
    public void LaunchProfile_ShortForm()
    {
        var options = VerifyOptions(["-lp", "CustomLaunchProfile"]);
        Assert.AreEqual("CustomLaunchProfile", options.LaunchProfileName);
    }

    private const string NugetInteractiveProperty = "--property:NuGetInteractive=false";

    /// <summary>
    /// Validates that options that the "run" command forwards to "build" command are forwarded by dotnet-watch.
    /// </summary>
    [TestMethod]
    [DataRow(new[] { "--configuration", "release" }, new[] { "--property:Configuration=release", NugetInteractiveProperty })]
    [DataRow(new[] { "--framework", "net9.0" }, new[] { NugetInteractiveProperty }, new string[0])]
    [DataRow(new[] { "--runtime", "arm64" }, new[] { NugetInteractiveProperty, "--property:RuntimeIdentifier=arm64", "--property:_CommandLineDefinedRuntimeIdentifier=true" })]
    [DataRow(new[] { "--property", "b=1" }, new[] { "--property:b=1", NugetInteractiveProperty })]
    [DataRow(new[] { "--project", "x.csproj" }, new[] { NugetInteractiveProperty }, new[] { "--project", "x.csproj" })]
    [DataRow(new[] { "--launch-profile", "x" }, new[] { NugetInteractiveProperty }, new[] { "--launch-profile", "x" })]
    [DataRow(new[] { "--no-launch-profile" }, new[] { NugetInteractiveProperty }, new[] { "--no-launch-profile" })]
    [DataRow(new[] { "/p:b=1" }, new[] { "--property:b=1", NugetInteractiveProperty }, new[] { "/p", "b=1" })] // it's ok to split the argument into two since `dotnet run` handles `/p b=1`
    [DataRow(new[] { "--interactive" }, new[] { "--property:NuGetInteractive=true" })]
    [DataRow(new[] { "--no-restore" }, new[] { NugetInteractiveProperty, "-restore:false" })]
    [DataRow(new[] { "--sc" }, new[] { NugetInteractiveProperty, "--property:SelfContained=true", "--property:_CommandLineDefinedSelfContained=true" })]
    [DataRow(new[] { "--self-contained" }, new[] { NugetInteractiveProperty, "--property:SelfContained=true", "--property:_CommandLineDefinedSelfContained=true" })]
    [DataRow(new[] { "--no-self-contained" }, new[] { NugetInteractiveProperty, "--property:SelfContained=false", "--property:_CommandLineDefinedSelfContained=true" })]
    [DataRow(new[] { "--verbose" }, new[] { NugetInteractiveProperty }, new string[0])]
    [DataRow(new[] { "--verbosity", "q" }, new[] { NugetInteractiveProperty, "--verbosity:q" })]
    [DataRow(new[] { "--arch", "arm", "--os", "win" }, new[] { NugetInteractiveProperty, "--property:RuntimeIdentifier=win-arm" })]
    [DataRow(new[] { "--disable-build-servers" }, new[] { NugetInteractiveProperty, "--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false" })]
    [DataRow(new[] { "-bl" }, new[] { NugetInteractiveProperty, "-bl" })]
    [DataRow(new[] { "/bl" }, new[] { NugetInteractiveProperty, "/bl" })]
    [DataRow(new[] { "/bl:X.binlog" }, new[] { NugetInteractiveProperty, "/bl:X.binlog" })]
    [DataRow(new[] { "-binaryLogger" }, new[] { NugetInteractiveProperty, "-binaryLogger" })]
    [DataRow(new[] { "/binaryLogger" }, new[] { NugetInteractiveProperty, "/binaryLogger" })]
    [DataRow(new[] { "--binaryLogger:LogFile=output.binlog;ProjectImports=None" }, new[] { NugetInteractiveProperty, "--binaryLogger:LogFile=output.binlog;ProjectImports=None" })]
    public void ForwardedOptionsAndArguments_Run(string[] args, string[] buildArgs, string[] commandArgs = null)
    {
        var runOptions = VerifyOptions(["run", .. args]);
        AssertEx.SequenceEqual(buildArgs, runOptions.BuildArguments);
        AssertEx.SequenceEqual(commandArgs ?? args, runOptions.CommandArguments);
    }

    // TODO:
    // Test MTP: https://github.com/dotnet/sdk/issues/52383

    [TestMethod]
    [DataRow(new[] { "--property:b=1" }, new[] { "--property:b=1" }, new[] { "--property", "b=1" })]
    [DataRow(new[] { "--property", "b=1" }, new[] { "--property:b=1" }, new[] { "--property", "b=1" })]
    [DataRow(new[] { "/p:b=1" }, new[] { "--property:b=1" }, new[] { "/p", "b=1" })]
    [DataRow(new[] { "/bl" }, new[] { "/bl" }, new[] { "/bl" })]
    [DataRow(
        new[] { "--binaryLogger:LogFile=output.binlog;ProjectImports=None" },
        new[] { "--binaryLogger:LogFile=output.binlog;ProjectImports=None" },
        new[] { "--binaryLogger:LogFile=output.binlog;ProjectImports=None" })]
    [DataRow(new[] { "--launch-profile", "x" }, new string[0])]
    [DataRow(new[] { "--no-launch-profile" }, new string[0])]
    [DataRow(new[] { "--project", "x.csproj" }, new string[0], new[] { "x.csproj" })]
    [DataRow(new[] { "--verbose" }, new string[0])]
    public void ForwardedOptionsAndArguments_Test(string[] args, string[] buildArgs, string[] commandArgs = null)
    {
        var runOptions = VerifyOptions(["test", .. args]);

        var isShortProperty = args[0].Contains("-p") || args[0].Contains("/p");

        // `test` subcommand forwards "--target:VSTest" to build, but BuildArguments are used to invoke
        // `dotnet build` and design-time build and should not specify build targets:
        string[] expectedBuildArgs = ["--property:VSTestNoLogo=true", "--property:NuGetInteractive=false", .. buildArgs];
        AssertEx.SequenceEqual(expectedBuildArgs, runOptions.BuildArguments);

        AssertEx.SequenceEqual(commandArgs ?? [], runOptions.CommandArguments);
    }

    [TestMethod]
    [DataRow(new[] { "--property:b=1" }, new[] { "--property:b=1", "--property:NuGetInteractive=false", "--nologo" }, new[] { "--property", "b=1" })]
    [DataRow(new[] { "--property", "b=1" }, new[] { "--property:b=1", "--property:NuGetInteractive=false", "--nologo" }, new[] { "--property", "b=1" })]
    [DataRow(new[] { "/p:b=1" }, new[] { "--property:b=1", "--property:NuGetInteractive=false", "--nologo" }, new[] { "/p", "b=1" })]
    [DataRow(new[] { "/bl" }, new[] { "--property:NuGetInteractive=false", "--nologo", "/bl" }, new[] { "/bl" })]
    [DataRow(
        new[] { "--binaryLogger:LogFile=output.binlog;ProjectImports=None" },
        new[] { "--property:NuGetInteractive=false", "--nologo", "--binaryLogger:LogFile=output.binlog;ProjectImports=None" },
        new[] { "--binaryLogger:LogFile=output.binlog;ProjectImports=None" })]
    [DataRow(new[] { "--launch-profile", "x" }, new[] { "--property:NuGetInteractive=false", "--nologo" })]
    [DataRow(new[] { "--no-launch-profile" }, new[] { "--property:NuGetInteractive=false", "--nologo" })]
    [DataRow(new[] { "--project", "x.csproj" }, new[] { "--property:NuGetInteractive=false", "--nologo" }, new[] { "x.csproj" })]
    [DataRow(new[] { "--verbose" }, new[] { "--property:NuGetInteractive=false", "--nologo" })]
    public void ForwardedOptionsAndArguments_Build(string[] args, string[] buildArgs, string[] commandArgs = null)
    {
        var runOptions = VerifyOptions(["build", .. args]);

        AssertEx.SequenceEqual(buildArgs, runOptions.BuildArguments);

        AssertEx.SequenceEqual(commandArgs ?? [], runOptions.CommandArguments);
    }

    [TestMethod]
    public void ForwardedBuildOptions_ArtifactsPath()
    {
        var path = SdkTestContext.Current.TestAssetsDirectory;

        var args = new[] { "--artifacts-path", path };
        var buildArgs = new[] { NugetInteractiveProperty, @"--property:ArtifactsPath=" + path };

        var options = VerifyOptions(["run", .. args]);
        AssertEx.SequenceEqual(buildArgs, options.BuildArguments);
    }
}
