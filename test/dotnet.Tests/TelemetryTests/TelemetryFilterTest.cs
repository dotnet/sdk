// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Utilities;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.TelemetryTests;

/// <summary>
/// Only adding the performance data tests for now as the TelemetryCommandTests cover most other scenarios already
/// </summary>
[TestClass]
public class TelemetryFilterTests : SdkTest
{
    private readonly FakeRecordEventNameTelemetry _fakeTelemetry;

    public string? EventName { get; set; }

    public IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

    public TelemetryFilterTests()
    {
        _fakeTelemetry = new FakeRecordEventNameTelemetry();
        TelemetryEventEntry.Subscribe(_fakeTelemetry.TrackEvent);
        TelemetryEventEntry.TelemetryFilter = new TelemetryFilter(Sha256Hasher.HashWithNormalizedCasing);
    }

    [TestMethod]
    public void TopLevelCommandNameShouldBeSentToTelemetry()
    {
        var parseResult = Parser.Parse(["build"]);
        TelemetryEventEntry.SendFiltered(parseResult);
        _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "toplevelparser/command" &&
                e.Properties.ContainsKey("verb") &&
                e.Properties["verb"] == Sha256Hasher.Hash("BUILD"));
    }

    [TestMethod]
    public void TopLevelCommandNameShouldBeSentToTelemetryWithGlobalJsonState()
    {
        string globalJsonState = "invalid_data";
        var parseResult = Parser.Parse(["build"]);
        TelemetryEventEntry.SendFiltered(new ParseResultWithGlobalJsonState(parseResult, globalJsonState));
        _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "toplevelparser/command" &&
                e.Properties.ContainsKey("verb") &&
                e.Properties["verb"] == Sha256Hasher.Hash("BUILD") &&
                e.Properties.ContainsKey("globalJson") &&
                e.Properties["globalJson"] == Sha256Hasher.HashWithNormalizedCasing(globalJsonState));
    }

    [TestMethod]
    public void SubLevelCommandNameShouldBeSentToTelemetry()
    {
        // Use a fresh DotNetCommandDefinition to avoid test pollution from
        // other tests that may have dynamically added template names (e.g. "console")
        // as subcommands to the static Parser.RootCommand's "new" command.
        // See InstantiateCommand.ExecuteAsync which mutates the static parser tree.
        var rootCommand = new DotNetCommandDefinition();
        // Remove the built-in VersionOption that conflicts with the SDK's custom --version option.
        // In production, Parser.NormalizeRootOptions handles this, but it's private.
        for (int i = rootCommand.Options.Count - 1; i >= 0; i--)
        {
            if (rootCommand.Options[i] is VersionOption)
            {
                rootCommand.Options.RemoveAt(i);
            }
        }
        var parseResult = rootCommand.Parse(["new", "console"], Parser.ParserConfiguration);
        TelemetryEventEntry.SendFiltered(parseResult);
        _fakeTelemetry
            .LogEntries.Should()
            .Contain(e => e.EventName == "sublevelparser/command" &&
                e.Properties.ContainsKey("argument") &&
                e.Properties["argument"] == Sha256Hasher.Hash("CONSOLE") &&
                e.Properties.ContainsKey("verb") &&
                e.Properties["verb"] == Sha256Hasher.Hash("NEW"));
    }

    [TestMethod]
    public void WorkloadSubLevelCommandNameAndArgumentShouldBeSentToTelemetry()
    {
        var parseResult =
            Parser.Parse(["workload", "install", "microsoft-ios-sdk-full"]);
        TelemetryEventEntry.SendFiltered(parseResult);
        _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "sublevelparser/command" &&
                                                        e.Properties.ContainsKey("verb") &&
                                                        e.Properties["verb"] == Sha256Hasher.Hash("WORKLOAD") &&
                                                        e.Properties["subcommand"] ==
                                                        Sha256Hasher.Hash("INSTALL") &&
                                                        e.Properties["argument"] ==
                                                        Sha256Hasher.Hash("MICROSOFT-IOS-SDK-FULL"));
    }

    [TestMethod]
    public void ToolsSubLevelCommandNameAndArgumentShouldBeSentToTelemetry()
    {
        var parseResult =
            Parser.Parse(["tool", "install", "dotnet-format"]);
        TelemetryEventEntry.SendFiltered(parseResult);
        _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "sublevelparser/command" &&
                                                        e.Properties.ContainsKey("verb") &&
                                                        e.Properties["verb"] == Sha256Hasher.Hash("TOOL") &&
                                                        e.Properties["subcommand"] ==
                                                        Sha256Hasher.Hash("INSTALL") &&
                                                        e.Properties["argument"] ==
                                                        Sha256Hasher.Hash("DOTNET-FORMAT"));
    }

    [TestMethod]
    public void WhenCalledWithDiagnosticWorkloadSubLevelCommandNameAndArgumentShouldBeSentToTelemetry()
    {
        var parseResult =
            Parser.Parse(["-d", "workload", "install", "microsoft-ios-sdk-full"]);
        TelemetryEventEntry.SendFiltered(parseResult);
        _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "sublevelparser/command" &&
                                                        e.Properties.ContainsKey("verb") &&
                                                        e.Properties["verb"] == Sha256Hasher.Hash("WORKLOAD") &&
                                                        e.Properties["subcommand"] ==
                                                        Sha256Hasher.Hash("INSTALL") &&
                                                        e.Properties["argument"] ==
                                                        Sha256Hasher.Hash("MICROSOFT-IOS-SDK-FULL"));
    }

    [TestMethod]
    public void WhenCalledWithMissingArgumentWorkloadSubLevelCommandNameAndArgumentShouldBeSentToTelemetry()
    {
        var parseResult =
            Parser.Parse(["-d", "workload", "install"]);
        TelemetryEventEntry.SendFiltered(parseResult);
        _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "sublevelparser/command" &&
                                                        e.Properties.ContainsKey("verb") &&
                                                        e.Properties["verb"] == Sha256Hasher.Hash("WORKLOAD") &&
                                                        e.Properties["subcommand"] ==
                                                        Sha256Hasher.Hash("INSTALL"));
    }

    [TestMethod]
    public void DotnetHelpShouldSendHelpVerbToTelemetry()
    {
        var parseResult = Parser.Parse(["--help"]);
        TelemetryEventEntry.SendFiltered(parseResult);
        _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "toplevelparser/command" &&
                e.Properties.ContainsKey("verb") &&
                e.Properties["verb"] == Sha256Hasher.Hash("--HELP") &&
                e.Properties.ContainsKey("help") &&
                e.Properties["help"] == Sha256Hasher.Hash("TRUE"));
    }

    [TestMethod]
    public void DotnetVersionShouldSendVersionVerbToTelemetry()
    {
        var parseResult = Parser.Parse(["--version"]);
        TelemetryEventEntry.SendFiltered(parseResult);
        _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "toplevelparser/command" &&
                e.Properties.ContainsKey("verb") &&
                e.Properties["verb"] == Sha256Hasher.Hash("--VERSION"));
    }

    [TestMethod]
    public void DotnetInfoShouldSendInfoVerbToTelemetry()
    {
        var parseResult = Parser.Parse(["--info"]);
        TelemetryEventEntry.SendFiltered(parseResult);
        _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "toplevelparser/command" &&
                e.Properties.ContainsKey("verb") &&
                e.Properties["verb"] == Sha256Hasher.Hash("--INFO"));
    }

    [TestMethod]
    public void SubcommandHelpShouldSendVerbWithHelpProperty()
    {
        var parseResult = Parser.Parse(["build", "--help"]);
        TelemetryEventEntry.SendFiltered(parseResult);
        _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "toplevelparser/command" &&
                e.Properties.ContainsKey("verb") &&
                e.Properties["verb"] == Sha256Hasher.Hash("BUILD") &&
                e.Properties.ContainsKey("help") &&
                e.Properties["help"] == Sha256Hasher.Hash("TRUE"));
    }

    [TestMethod]
    public void RegularBuildCommandShouldNotHaveHelpProperty()
    {
        var parseResult = Parser.Parse(["build"]);
        TelemetryEventEntry.SendFiltered(parseResult);
        _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "toplevelparser/command" &&
                e.Properties.ContainsKey("verb") &&
                e.Properties["verb"] == Sha256Hasher.Hash("BUILD") &&
                !e.Properties.ContainsKey("help"));
    }
}
