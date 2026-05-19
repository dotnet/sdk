// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Utilities;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.TelemetryTests;

/// <summary>
/// Only adding the performance data tests for now as the TelemetryCommandTests cover most other scenarios already
/// </summary>
public class TelemetryFilterTests : SdkTest
{
    private readonly FakeRecordEventNameTelemetry _fakeTelemetry;

    public string? EventName { get; set; }

    public IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

    public TelemetryFilterTests(ITestOutputHelper log) : base(log)
    {
        _fakeTelemetry = new FakeRecordEventNameTelemetry();
        TelemetryEventEntry.Subscribe(_fakeTelemetry.TrackEvent);
        TelemetryEventEntry.TelemetryFilter = new TelemetryFilter(Sha256Hasher.HashWithNormalizedCasing);
    }

    [Fact]
    public void TopLevelCommandNameShouldBeSentToTelemetry()
    {
        var parseResult = Parser.Parse(["build"]);
        TelemetryEventEntry.SendFiltered(parseResult);
        _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "toplevelparser/command" &&
                e.Properties.ContainsKey("verb") &&
                e.Properties["verb"] == Sha256Hasher.Hash("BUILD"));
    }

    [Fact]
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

    [Fact]
    public void SubLevelCommandNameShouldBeSentToTelemetry()
    {
        var parseResult = Parser.Parse(["new", "console"]);
        TelemetryEventEntry.SendFiltered(parseResult);
        _fakeTelemetry
            .LogEntries.Should()
            .Contain(e => e.EventName == "sublevelparser/command" &&
                e.Properties.ContainsKey("argument") &&
                e.Properties["argument"] == Sha256Hasher.Hash("CONSOLE") &&
                e.Properties.ContainsKey("verb") &&
                e.Properties["verb"] == Sha256Hasher.Hash("NEW"));
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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
}
