// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using System.CommandLine.Parsing;
using Parser = Microsoft.DotNet.Cli.Parser;
using System.Collections.Generic;
using Xunit;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;
using System;

namespace Microsoft.DotNet.Tests
{
    /// <summary>
    /// Only adding the performance data tests for now as the TelemetryCommandTests cover most other scenarios already
    /// </summary>
    public class TelemetryFilterTests : SdkTest
    {
        private readonly FakeRecordEventNameTelemetry _fakeTelemetry;

        public string EventName { get; set; }

        public IDictionary<string, string> Properties { get; set; }

        public TelemetryFilterTests(ITestOutputHelper log) : base(log)
        {
            _fakeTelemetry = new FakeRecordEventNameTelemetry();
            TelemetryEventEntry.Subscribe(_fakeTelemetry.TrackEvent);
            TelemetryEventEntry.TelemetryFilter = new TelemetryFilter(Sha256Hasher.HashWithNormalizedCasing);
        }

        [Fact]
        public void TopLevelCommandNameShouldBeSentToTelemetryWithoutPerformanceData()
        {
            var parseResult = Parser.Instance.Parse(new List<string>() { "build" });
            TelemetryEventEntry.SendFiltered(parseResult);
            _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "toplevelparser/command" &&
                  e.Properties.ContainsKey("verb") &&
                  e.Properties["verb"] == Sha256Hasher.Hash("BUILD") &&
                  e.Measurement == null);
        }

        [Fact]
        public void TopLevelCommandNameShouldBeSentToTelemetryWithPerformanceData()
        {
            var parseResult = Parser.Instance.Parse(new List<string>() { "build" });
            TelemetryEventEntry.SendFiltered(Tuple.Create(parseResult, new Dictionary<string, double>() { { "Startup Time", 12345 } }));
            _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "toplevelparser/command" &&
                  e.Properties.ContainsKey("verb") &&
                  e.Properties["verb"] == Sha256Hasher.Hash("BUILD") &&
                  e.Measurement.ContainsKey("Startup Time") &&
                  e.Measurement["Startup Time"] == 12345);
        }

        [Fact]
        public void TopLevelCommandNameShouldBeSentToTelemetryWithZeroPerformanceData()
        {
            var parseResult = Parser.Instance.Parse(new List<string>() { "build" });
            TelemetryEventEntry.SendFiltered(Tuple.Create(parseResult, new Dictionary<string, double>() { { "Startup Time", 0 } }));
            _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "toplevelparser/command" &&
                  e.Properties.ContainsKey("verb") &&
                  e.Properties["verb"] == Sha256Hasher.Hash("BUILD") &&
                  e.Measurement == null);
        }

        [Fact]
        public void TopLevelCommandNameShouldBeSentToTelemetryWithSomeZeroPerformanceData()
        {
            var parseResult = Parser.Instance.Parse(new List<string>() { "build" });
            TelemetryEventEntry.SendFiltered(Tuple.Create(parseResult, new Dictionary<string, double>() { { "Startup Time", 0 },{ "Parse Time", 23456 } }));
            _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "toplevelparser/command" &&
                  e.Properties.ContainsKey("verb") &&
                  e.Properties["verb"] == Sha256Hasher.Hash("BUILD") &&
                  !e.Measurement.ContainsKey("Startup Time") &&
                  e.Measurement.ContainsKey("Parse Time") &&
                  e.Measurement["Parse Time"] == 23456);
        }

        [Fact]
        public void SubLevelCommandNameShouldBeSentToTelemetryWithoutPerformanceData()
        {
            var parseResult = Parser.Instance.Parse(new List<string>() { "new", "console" });
            TelemetryEventEntry.SendFiltered(parseResult);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" &&
                    e.Properties.ContainsKey("argument") &&
                    e.Properties["argument"] == Sha256Hasher.Hash("CONSOLE") &&
                    e.Properties.ContainsKey("verb") &&
                    e.Properties["verb"] == Sha256Hasher.Hash("NEW") &&
                    e.Measurement == null);
        }

        [Fact]
        public void SubLevelCommandNameShouldBeSentToTelemetryWithPerformanceData()
        {
            var parseResult = Parser.Instance.Parse(new List<string>() { "new", "console" });
            TelemetryEventEntry.SendFiltered(Tuple.Create(parseResult, new Dictionary<string, double>() { { "Startup Time", 34567 } }));
            _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "sublevelparser/command" &&
                    e.Properties.ContainsKey("argument") &&
                    e.Properties["argument"] == Sha256Hasher.Hash("CONSOLE") &&
                    e.Properties.ContainsKey("verb") &&
                    e.Properties["verb"] == Sha256Hasher.Hash("NEW") &&
                    e.Measurement.ContainsKey("Startup Time") &&
                    e.Measurement["Startup Time"] == 34567);
        }

        [Fact]
        public void SubLevelCommandNameShouldBeSentToTelemetryWithZeroPerformanceData()
        {
            var parseResult = Parser.Instance.Parse(new List<string>() { "new", "console" });
            TelemetryEventEntry.SendFiltered(Tuple.Create(parseResult, new Dictionary<string, double>() { { "Startup Time", 0 } }));
            _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "sublevelparser/command" &&
                    e.Properties.ContainsKey("argument") &&
                    e.Properties["argument"] == Sha256Hasher.Hash("CONSOLE") &&
                    e.Properties.ContainsKey("verb") &&
                    e.Properties["verb"] == Sha256Hasher.Hash("NEW") &&
                    e.Measurement == null);
}

        [Fact]
        public void SubLevelCommandNameShouldBeSentToTelemetryWithSomeZeroPerformanceData()
        {
            var parseResult = Parser.Instance.Parse(new List<string>() { "new", "console" });
            TelemetryEventEntry.SendFiltered(Tuple.Create(parseResult, new Dictionary<string, double>() { { "Startup Time", 0 }, { "Parse Time", 45678 } }));
            _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "sublevelparser/command" &&
                    e.Properties.ContainsKey("argument") &&
                    e.Properties["argument"] == Sha256Hasher.Hash("CONSOLE") &&
                    e.Properties.ContainsKey("verb") &&
                    e.Properties["verb"] == Sha256Hasher.Hash("NEW") &&
                    !e.Measurement.ContainsKey("Startup Time") &&
                    e.Measurement.ContainsKey("Parse Time") &&
                    e.Measurement["Parse Time"] == 45678);
        }
    }
}
