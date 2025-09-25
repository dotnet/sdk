// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Commands.MSBuild;

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
                    { "OutputType", "Library"},
                }
            };

            MSBuildLogger.FormatAndSend(fakeTelemetry, telemetryEventArgs);

            fakeTelemetry.LogEntry.Properties.Should().BeEquivalentTo(new Dictionary<string, string>
                {
                    { "TargetFrameworkVersion", "9a871d7066260764d4cb5047e4b10570271d04bd1da275681a4b12bce0b27496"},
                    { "RuntimeIdentifier", "fb329000228cc5a24c264c57139de8bf854fc86fc18bf1c04ab61a2b5cb4b921"},
                    { "SelfContained", "fb329000228cc5a24c264c57139de8bf854fc86fc18bf1c04ab61a2b5cb4b921"},
                    { "UseApphost", "fb329000228cc5a24c264c57139de8bf854fc86fc18bf1c04ab61a2b5cb4b921"},
                    { "OutputType", "d77982267d9699c2a57bcab5bb975a1935f6427002f52fd4569762fd72db3a94"},
                });
        }

        [Fact]
        public void ItAggregatesEvents()
        {
            var fakeTelemetry = new FakeTelemetry();
            fakeTelemetry.Enabled = true;
            var logger = new MSBuildLogger(fakeTelemetry);

            var event1 = new TelemetryEventArgs
            {
                EventName = MSBuildLogger.TaskFactoryTelemetryAggregatedEventName,
                Properties = new Dictionary<string, string>
                {
                    { "AssemblyTaskFactoryTasksExecutedCount", "2" },
                    { "RoslynCodeTaskFactoryTasksExecutedCount", "1" }
                }
            };

            var event2 = new TelemetryEventArgs
            {
                EventName = MSBuildLogger.TaskFactoryTelemetryAggregatedEventName,
                Properties = new Dictionary<string, string>
                {
                    { "AssemblyTaskFactoryTasksExecutedCount", "3" },
                    { "CustomTaskFactoryTasksExecutedCount", "2" }
                }
            };

            var event3 = new TelemetryEventArgs
            {
                EventName = MSBuildLogger.TasksTelemetryAggregatedEventName,
                Properties = new Dictionary<string, string>
                {
                    { "TasksExecutedCount", "3" },
                    { "TaskHostTasksExecutedCount", "2" }
                }
            };

            var event4 = new TelemetryEventArgs
            {
                EventName = MSBuildLogger.TasksTelemetryAggregatedEventName,
                Properties = new Dictionary<string, string>
                {
                    { "TasksExecutedCount", "5" }
                }
            };

            logger.AggregateEvent(event1);
            logger.AggregateEvent(event2);
            logger.AggregateEvent(event3);
            logger.AggregateEvent(event4);

            logger.SendAggregatedEventsOnBuildFinished(fakeTelemetry);

            fakeTelemetry.LogEntries.Should().HaveCount(2);

            var taskFactoryEntry = fakeTelemetry.LogEntries.FirstOrDefault(e => e.EventName == $"msbuild/{MSBuildLogger.TaskFactoryTelemetryAggregatedEventName}");
            taskFactoryEntry.Should().NotBeNull();
            taskFactoryEntry.Properties["AssemblyTaskFactoryTasksExecutedCount"].Should().Be("5"); // 2 + 3
            taskFactoryEntry.Properties["RoslynCodeTaskFactoryTasksExecutedCount"].Should().Be("1"); // 1 + 0
            taskFactoryEntry.Properties["CustomTaskFactoryTasksExecutedCount"].Should().Be("2"); // 0 + 2

            var tasksEntry = fakeTelemetry.LogEntries.FirstOrDefault(e => e.EventName == $"msbuild/{MSBuildLogger.TasksTelemetryAggregatedEventName}");
            tasksEntry.Should().NotBeNull();
            tasksEntry.Properties["TasksExecutedCount"].Should().Be("8"); // 3 + 5
            tasksEntry.Properties["TaskHostTasksExecutedCount"].Should().Be("2"); // 2 + 0
        }

        [Fact]
        public void ItIgnoresNonIntegerPropertiesDuringAggregation()
        {
            var fakeTelemetry = new FakeTelemetry();
            fakeTelemetry.Enabled = true;
            var logger = new MSBuildLogger(fakeTelemetry);
            
            var eventArgs = new TelemetryEventArgs
            {
                EventName = MSBuildLogger.TaskFactoryTelemetryAggregatedEventName,
                Properties = new Dictionary<string, string>
                {
                    { "AssemblyTaskFactoryTasksExecutedCount", "3" },
                    { "InvalidProperty", "not-a-number" },
                    { "InvalidProperty2", "1.234" },
                }
            };

            logger.AggregateEvent(eventArgs);

            logger.SendAggregatedEventsOnBuildFinished(fakeTelemetry);

            fakeTelemetry.LogEntry.Should().NotBeNull();
            fakeTelemetry.LogEntry.EventName.Should().Be($"msbuild/{MSBuildLogger.TaskFactoryTelemetryAggregatedEventName}");
            fakeTelemetry.LogEntry.Properties["AssemblyTaskFactoryTasksExecutedCount"].Should().Be("3");
            fakeTelemetry.LogEntry.Properties.Should().NotContainKey("InvalidProperty");
            fakeTelemetry.LogEntry.Properties.Should().NotContainKey("InvalidProperty2");
        }
    }
}
