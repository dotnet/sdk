// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [TestClass]
    public class GivenMSBuildLogger
    {
        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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
        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
        public void ItForwardsTaskDetailsEvent()
        {
            var fakeTelemetry = new FakeTelemetry();
            var telemetryEventArgs = new TelemetryEventArgs
            {
                EventName = MSBuildLogger.TasksDetailsTelemetryEventName,
                Properties = new Dictionary<string, string>
                {
                    { "Tasks", "[{\"Name\":\"Copy\",\"ExecutionsCount\":10}]" },
                    { "TaskCount", "1" },
                    { "TotalTaskCount", "1" }
                }
            };

            MSBuildLogger.FormatAndSend(fakeTelemetry, telemetryEventArgs);

            fakeTelemetry.LogEntry.Should().NotBeNull();
            fakeTelemetry.LogEntry.EventName.Should().Be($"msbuild/{MSBuildLogger.TasksDetailsTelemetryEventName}");
            fakeTelemetry.LogEntry.Properties.Keys.Count.Should().Be(3);
            fakeTelemetry.LogEntry.Properties["Tasks"].Should().Be("[{\"Name\":\"Copy\",\"ExecutionsCount\":10}]");
            fakeTelemetry.LogEntry.Properties["TaskCount"].Should().Be("1");
            fakeTelemetry.LogEntry.Properties["TotalTaskCount"].Should().Be("1");
        }

        [TestMethod]
        public void ItForwardsRoslynCompilerCacheEvent()
        {
            var fakeTelemetry = new FakeTelemetry();
            var telemetryEventArgs = new TelemetryEventArgs
            {
                EventName = MSBuildLogger.RoslynCompilerCacheEventName,
                Properties = new Dictionary<string, string>
                {
                    { "cachestatus", "hit" },
                    { "storeresult", "none" },
                    { "language", "C#" },
                    { "keycomputems", "5" },
                    { "restorems", "6" },
                    { "storems", "0" }
                }
            };

            MSBuildLogger.FormatAndSend(fakeTelemetry, telemetryEventArgs);

            fakeTelemetry.LogEntry.Should().NotBeNull();
            fakeTelemetry.LogEntry.EventName.Should().Be($"msbuild/{MSBuildLogger.RoslynCompilerCacheEventName}");
            fakeTelemetry.LogEntry.Properties.Should().BeEquivalentTo(telemetryEventArgs.Properties);
        }

        [TestMethod]
        public void ItCreatesAnInternalActivityForEachBuild()
        {
            ActivitySource activitySource = Activities.Source;
            Activity stoppedActivity = null;
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source == activitySource,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => stoppedActivity = activity,
            };
            ActivitySource.AddActivityListener(listener);

            using Activity parentActivity = new Activity("parent").Start();
            var eventSource = new PersistentDispatcher([]);
            var logger = new MSBuildLogger(new FakeTelemetry());
            logger.Initialize(eventSource);

            eventSource.Dispatch(new BuildStartedEventArgs("Build started.", helpKeyword: null));

            Activity.Current.Should().NotBeSameAs(parentActivity);
            Activity.Current.Kind.Should().Be(ActivityKind.Internal);
            Activity.Current.ParentSpanId.Should().Be(parentActivity.SpanId);

            eventSource.Dispatch(new BuildFinishedEventArgs("Build finished.", helpKeyword: null, succeeded: true));

            Activity.Current.Should().NotBeSameAs(parentActivity);
            stoppedActivity.Should().BeNull();

            logger.Shutdown();

            Activity.Current.Should().BeSameAs(parentActivity);
            stoppedActivity.Should().NotBeNull();
            stoppedActivity.Status.Should().Be(ActivityStatusCode.Ok);
        }

        [TestMethod]
        [DoNotParallelize]
        public void ItUsesTheCurrentParentContextForEachServerBuild()
        {
            string originalTraceParent = Environment.GetEnvironmentVariable(Activities.TRACEPARENT);
            Activity ambientActivity = Activity.Current;
            Activity.Current = null;

            try
            {
                ActivitySource activitySource = Activities.Source;
                using var listener = new ActivityListener
                {
                    ShouldListenTo = source => source == activitySource,
                    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                };
                ActivitySource.AddActivityListener(listener);

                var firstParent = new ActivityContext(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.Recorded,
                    isRemote: true);
                var firstActivity = RunBuildWithParent(firstParent);

                var secondParent = new ActivityContext(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.Recorded,
                    isRemote: true);
                var secondActivity = RunBuildWithParent(secondParent);

                firstActivity.TraceId.Should().Be(firstParent.TraceId);
                firstActivity.ParentSpanId.Should().Be(firstParent.SpanId);
                secondActivity.TraceId.Should().Be(secondParent.TraceId);
                secondActivity.ParentSpanId.Should().Be(secondParent.SpanId);
            }
            finally
            {
                Environment.SetEnvironmentVariable(Activities.TRACEPARENT, originalTraceParent);
                Activity.Current = ambientActivity;
            }

            static Activity RunBuildWithParent(ActivityContext parentContext)
            {
                Environment.SetEnvironmentVariable(
                    Activities.TRACEPARENT,
                    $"00-{parentContext.TraceId}-{parentContext.SpanId}-01");

                var eventSource = new PersistentDispatcher([]);
                var logger = new MSBuildLogger(new FakeTelemetry());
                logger.Initialize(eventSource);
                eventSource.Dispatch(new BuildStartedEventArgs("Build started.", helpKeyword: null));
                Activity activity = Activity.Current;
                eventSource.Dispatch(new BuildFinishedEventArgs("Build finished.", helpKeyword: null, succeeded: true));
                logger.Shutdown();
                return activity;
            }
        }
    }
}
