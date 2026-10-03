// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using BinaryLog = Microsoft.Build.Logging.StructuredLogger.BinaryLog;
using PublishCommand = Microsoft.DotNet.Cli.Commands.Publish.PublishCommand;

namespace Microsoft.DotNet.Tests.TelemetryTests;

[TestClass]
// Real in-process command execution uses MSBuild static state as well as process-wide listeners.
[DoNotParallelize]
public sealed class MSBuildActivityTests : SdkTest
{
    private readonly Dictionary<string, string?> _savedEnvironment = [];
    private bool _telemetryDisabledForTests;

    [TestInitialize]
    public void DisableExternalTelemetryAndBuildServers()
    {
        _telemetryDisabledForTests = TelemetryClient.DisabledForTests;
        TelemetryClient.DisabledForTests = true;

        Dictionary<string, string> environment = new()
        {
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "true",
            [EnvironmentVariableNames.SDK_VULNERABILITY_CHECK_DISABLE] = "true",
            [EnvironmentVariableNames.DISABLE_PUBLISH_AND_PACK_RELEASE] = "false",
            ["MSBUILDUSESERVER"] = "0",
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["DOTNET_HOST_PATH"] = SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath,
        };
        foreach ((string name, string value) in environment)
        {
            _savedEnvironment[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    [TestCleanup]
    public void RestoreEnvironment()
    {
        foreach ((string name, string? value) in _savedEnvironment)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        TelemetryClient.DisabledForTests = _telemetryDisabledForTests;
    }

    [TestMethod]
    public void PhysicalPublishExportsSubmissionHistograms()
    {
        var asset = TestAssetsManager.CopyTestAsset("HelloWorld")
            .WithSource()
            .WithProjectChanges(projectXml => projectXml.Root!.Add(
                new XElement("PropertyGroup",
                    new XElement("PublishRelease", "true"))));
        string project = Path.Combine(asset.Path, "HelloWorld.csproj");
        string output = Path.Combine(asset.Path, "output");
        string binlogArgument = BinLogArgument([Guid.NewGuid().ToString("N")]);
        Restore(project);

        string msbuildPath = Path.Combine(SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "MSBuild.dll");
        using var exported = new ActivityExports();
        using Activity? parent = Activities.Source.StartActivity("test-command");
        parent.Should().NotBeNull();
        string[] arguments =
        [
            "dotnet", "publish", project, "--no-restore", "--output", output,
            "--disable-build-servers", binlogArgument,
        ];
        var configured = Parser.Parse([.. arguments, "--configuration", "Debug"]);
        _ = PublishCommand.FromParseResult(configured, msbuildPath);
        exported.AssertNoReleasePropertyDiscovery();

        var parseResult = Parser.Parse(arguments);
        var command = (RestoringCommand)PublishCommand.FromParseResult(parseResult, msbuildPath);
        command.MSBuildArguments.Should().Contain("--property:Configuration=Release");
        command.SeparateRestoreCommand.Should().BeNull();
        Activity discovery = exported.AssertReleasePropertyDiscovery(parent);
        command.GetProcessStartInfo().Arguments.Should().Contain(msbuildPath);

        exported.AssertNoSubmissions();
        Activity.Current.Should().BeSameAs(parent);

        command.Execute().Should().Be(0);
        Activity.Current.Should().BeSameAs(parent);

        Activity[] submissions = exported.AssertSubmissionMeasurements(expectedCount: 1, parent);
        (discovery.StartTimeUtc + discovery.Duration).Should().BeOnOrBefore(submissions[0].StartTimeUtc);
        AssertBuildBoundaries(binlogArgument, submissions);
        TelemetryClient.Instance.Should().BeNull("local diagnostic collection must not initialize SDK telemetry");
        File.Exists(Path.Combine(output, "HelloWorld.dll")).Should().BeTrue();
    }

    [TestMethod]
    public void FailedSeparateRestoreExportsOnlyItsSubmission()
    {
        var asset = TestAssetsManager.CopyTestAsset("HelloWorld").WithSource();
        string binlogArgument = BinLogArgument([Guid.NewGuid().ToString("N")]);
        File.WriteAllText(Path.Combine(asset.Path, "Directory.Build.targets"), """
            <Project>
              <Target Name="FailActivityTest" BeforeTargets="Restore">
                <Error Text="Expected activity test failure." />
              </Target>
            </Project>
            """);

        using var exported = new ActivityExports();
        using Activity? parent = Activities.Source.StartActivity("test-command");
        parent.Should().NotBeNull();
        var command = (RestoringCommand)PublishCommand.FromArgs(
            [
                Path.Combine(asset.Path, "HelloWorld.csproj"),
                "--framework", ToolsetInfo.CurrentTargetFramework,
                "--configuration", "Debug",
                "--disable-build-servers",
                binlogArgument,
            ]);
        command.SeparateRestoreCommand.Should().NotBeNull();
        exported.AssertNoSubmissions();

        int exitCode = command.Execute();
        exitCode.Should().Be(1);

        Activity.Current.Should().BeSameAs(parent);
        Activity[] submissions = exported.AssertSubmissionMeasurements(expectedCount: 1, parent);
        AssertBuildBoundaries(binlogArgument, submissions);
    }

    [TestMethod]
    public void FileBasedPublishExportsItsBuildSubmission()
    {
        var directory = TestAssetsManager.CreateTestDirectory();
        string program = Path.Combine(directory.Path, "Program.cs");
        string output = Path.Combine(directory.Path, "output");
        string binlogArgument = BinLogArgument([Guid.NewGuid().ToString("N")]);
        File.WriteAllText(program, """
            #:property PublishAot=false
            #:property SelfContained=false
            #:property UseAppHost=false
            Console.WriteLine("File-based activity test");
            """);
        Restore(program);
        using var exported = new ActivityExports();
        using Activity? parent = Activities.Source.StartActivity("test-command");
        parent.Should().NotBeNull();
        CommandBase command = PublishCommand.FromArgs(
            [
                program, "--no-restore", "--configuration", "Debug", "--output", output,
                "--disable-build-servers", binlogArgument,
            ]);
        command.Should().BeOfType<VirtualProjectBuildingCommand>();
        exported.AssertNoSubmissions();

        int exitCode = command.Execute();

        exitCode.Should().Be(0);
        Activity.Current.Should().BeSameAs(parent);
        Activity[] submissions = exported.AssertSubmissionMeasurements(expectedCount: 1, parent);
        AssertBuildBoundaries(binlogArgument, submissions);
        File.Exists(Path.Combine(output, "Program.dll")).Should().BeTrue();
    }

    private static void AssertBuildBoundaries(string binlogArgument, Activity[] submissions)
    {
        string binlogPattern = Path.GetFullPath(binlogArgument["/bl:".Length..]);
        string[] binlogs = Directory.GetFiles(
            Path.GetDirectoryName(binlogPattern)!,
            Path.GetFileName(binlogPattern).Replace("{}", "*", StringComparison.Ordinal));
        binlogs.Should().HaveCount(submissions.Length);
        List<(DateTime Start, DateTime End)> builds = [];
        foreach (string binlog in binlogs)
        {
            BuildEventArgs[] events = BinaryLog.ReadRecords(binlog)
                .Select(record => record.Args)
                .OfType<BuildEventArgs>()
                .ToArray();
            BuildStartedEventArgs started = events.OfType<BuildStartedEventArgs>().Should().ContainSingle().Subject;
            BuildFinishedEventArgs finished = events.OfType<BuildFinishedEventArgs>().Should().ContainSingle().Subject;
            builds.Add((started.Timestamp.ToUniversalTime(), finished.Timestamp.ToUniversalTime()));
        }

        builds.Sort((left, right) => left.Start.CompareTo(right.Start));
        for (int i = 0; i < submissions.Length; i++)
        {
            submissions[i].StartTimeUtc.Should().BeOnOrBefore(builds[i].Start);
            (submissions[i].StartTimeUtc + submissions[i].Duration).Should().BeOnOrAfter(builds[i].End);
        }
    }

    private void Restore(string project)
    {
        new DotnetCommand(Log, "restore", project, "--disable-build-servers")
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
            .Execute()
            .Should()
            .Pass();
    }

    private sealed class ActivityExports : IDisposable
    {
        private readonly List<Activity> _activities = [];
        private readonly List<Metric> _metrics = [];
        private readonly TracerProvider _traces;
        private readonly MeterProvider _meter;

        public ActivityExports()
        {
            _traces = Sdk.CreateTracerProviderBuilder()
                .AddSource(Activities.Source.Name, Activities.PerformanceSource.Name)
                .SetSampler(new AlwaysOnSampler())
                .AddInMemoryExporter(_activities)
                .Build();
            // A manual reader collects once, without periodic snapshots or timer-based assertions.
            _meter = Sdk.CreateMeterProviderBuilder()
                .AddMeter(Activities.PerformanceSource.Name)
                .AddReader(new BaseExportingMetricReader(new InMemoryExporter<Metric>(_metrics)))
                .Build();
        }

        public void AssertNoSubmissions() =>
            _activities.Should().NotContain(activity => activity.OperationName == "msbuild-submission");

        public void AssertNoReleasePropertyDiscovery() =>
            _activities.Should().NotContain(activity => activity.OperationName == "release-property-discovery");

        public Activity AssertReleasePropertyDiscovery(Activity? parent)
        {
            Activity discovery = _activities.Should().ContainSingle(
                activity => activity.OperationName == "release-property-discovery").Subject;
            discovery.Source.Should().BeSameAs(Activities.PerformanceSource);
            discovery.ParentSpanId.Should().Be(parent?.SpanId ?? default);
            return discovery;
        }

        public Activity[] AssertSubmissionMeasurements(int expectedCount, Activity? parent)
        {
            _traces.ForceFlush().Should().BeTrue();
            _meter.ForceFlush().Should().BeTrue();

            Activity[] submissions = _activities
                .Where(activity => activity.OperationName == "msbuild-submission")
                .OrderBy(activity => activity.StartTimeUtc)
                .ToArray();
            submissions.Should().HaveCount(expectedCount);
            ActivitySpanId parentSpanId = parent?.SpanId ?? default;
            foreach (Activity activity in submissions)
            {
                activity.Source.Should().BeSameAs(Activities.PerformanceSource);
                activity.ParentSpanId.Should().Be(parentSpanId);
            }

            Metric metric = _metrics.Should().ContainSingle(
                metric => metric.Name == "dotnet.cli.activity.duration").Subject;
            metric.MeterName.Should().Be("dotnet-cli-perf");
            metric.Unit.Should().Be("s");
            metric.MetricType.Should().Be(MetricType.Histogram);

            HashSet<string> measuredActivities = [];
            foreach (ref readonly MetricPoint point in metric.GetMetricPoints())
            {
                string? activityName = null;
                foreach (var tag in point.Tags)
                {
                    if (tag.Key == "activity.name")
                    {
                        activityName = tag.Value.Should().BeOfType<string>().Subject;
                    }
                }

                activityName.Should().NotBeNull();
                measuredActivities.Add(activityName!).Should().BeTrue("each activity name has a distinct histogram series");
                Activity[] activities = _activities.Where(activity => activity.OperationName == activityName).ToArray();
                activities.Should().NotBeEmpty();
                point.GetHistogramCount().Should().Be(activities.Length);
                point.GetHistogramSum().Should().BeApproximately(
                    activities.Sum(activity => activity.Duration.TotalSeconds), 1e-9);
            }

            measuredActivities.Should().BeEquivalentTo(_activities.Select(activity => activity.OperationName).Distinct());
            return submissions;
        }

        public void Dispose()
        {
            _meter.Dispose();
            _traces.Dispose();
        }
    }
}
