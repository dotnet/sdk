// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenMSBuildLogger
    {
        [Fact]
        public void ItBlocksTelemetryThatIsNotInTheList()
        {
            var fakeTelemetry = new FakeTelemetry();
            var telemetryEventArgs = new TelemetryEventArgs
            {
                EventName = "User Defined Event Name",
                Properties = new Dictionary<string, string>
                {
                    { "User Defined Key", "User Defined Value"},
                }
            };

            MSBuildLogger.FormatAndSend(fakeTelemetry, telemetryEventArgs);

            fakeTelemetry.LogEntry.Should().BeNull();
        }

        [Fact]
        public void ItDoesNotMasksExceptionTelemetry()
        {
            var fakeTelemetry = new FakeTelemetry();
            var telemetryEventArgs = new TelemetryEventArgs
            {
                EventName = MSBuildLogger.SdkTaskBaseCatchExceptionTelemetryEventName,
                Properties = new Dictionary<string, string>
                {
                    { "exceptionType", "System.Exception"},
                    { "detail", "Exception detail"}
                }
            };

            MSBuildLogger.FormatAndSend(fakeTelemetry, telemetryEventArgs);

            fakeTelemetry.LogEntry.Should().NotBeNull();
            fakeTelemetry.LogEntry.EventName.Should().Be(MSBuildLogger.SdkTaskBaseCatchExceptionTelemetryEventName);
            fakeTelemetry.LogEntry.Properties.Keys.Count.Should().Be(2);
            fakeTelemetry.LogEntry.Properties["exceptionType"].Should().Be("System.Exception");
            fakeTelemetry.LogEntry.Properties["detail"].Should().Be("Exception detail");
        }

        [Fact]
        public void ItDoesNotMaskPublishPropertiesTelemetry()
        {
            var fakeTelemetry = new FakeTelemetry();
            var telemetryEventArgs = new TelemetryEventArgs
            {
                EventName = MSBuildLogger.PublishPropertiesTelemetryEventName,
                Properties = new Dictionary<string, string>
                {
                    { "PublishReadyToRun", "null"},
                    { "otherProperty", "otherProperty value"}
                }
            };

            MSBuildLogger.FormatAndSend(fakeTelemetry, telemetryEventArgs);

            fakeTelemetry.LogEntry.EventName.Should().Be(MSBuildLogger.PublishPropertiesTelemetryEventName);
            fakeTelemetry.LogEntry.Properties.Keys.Count.Should().Be(2);
            fakeTelemetry.LogEntry.Properties["PublishReadyToRun"].Should().Be("null");
            fakeTelemetry.LogEntry.Properties["otherProperty"].Should().Be("otherProperty value");
        }

        [Fact]
        public void ItDoesNotMaskReadyToRunTelemetry()
        {
            var fakeTelemetry = new FakeTelemetry();
            var telemetryEventArgs = new TelemetryEventArgs
            {
                EventName = MSBuildLogger.ReadyToRunTelemetryEventName,
                Properties = new Dictionary<string, string>
                {
                    { "PublishReadyToRunUseCrossgen2", "null"},
                    { "otherProperty", "otherProperty value"}
                }
            };

            MSBuildLogger.FormatAndSend(fakeTelemetry, telemetryEventArgs);

            fakeTelemetry.LogEntry.EventName.Should().Be(MSBuildLogger.ReadyToRunTelemetryEventName);
            fakeTelemetry.LogEntry.Properties.Keys.Count.Should().Be(2);
            fakeTelemetry.LogEntry.Properties["PublishReadyToRunUseCrossgen2"].Should().Be("null");
            fakeTelemetry.LogEntry.Properties["otherProperty"].Should().Be("otherProperty value");
        }

        // Reproduce https://github.com/dotnet/sdk/issues/3868
        [Fact]
        public void ItCanSendProperties()
        {
            var fakeTelemetry = new FakeTelemetry();
            var telemetryEventArgs = new TelemetryEventArgs
            {
                EventName = "targetframeworkeval",
                Properties = new Dictionary<string, string>
                {
                    { "TargetFrameworkVersion", ".NETFramework,Version=v4.6"},
                    { "RuntimeIdentifier", "null"},
                    { "SelfContained", "null"},
                    { "UseApphost", "null"},
                    { "OutputType", "Library"}
                }
            };

            MSBuildLogger.FormatAndSend(fakeTelemetry, telemetryEventArgs);

            fakeTelemetry.LogEntry.Properties.Should().BeEquivalentTo(telemetryEventArgs.Properties);
        }
    }
}
