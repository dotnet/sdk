// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.MSBuild.Tests;
using Microsoft.DotNet.Cli.Utils;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using PackCommand = Microsoft.DotNet.Cli.Commands.Pack.PackCommand;
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
    [DataRow("pack", false, 1)]
    [DataRow("pack", true, 1)]
    [DataRow("publish", false, 1)]
    [DataRow("publish", true, 2)]
    public void PhysicalPackAndPublishExportSubmissionHistograms(string verb, bool outOfProcess, int executions)
    {
        var asset = TestAssetsManager.CopyTestAsset("HelloWorld", identifier: $"{verb}-{outOfProcess}")
            .WithSource();
        string project = Path.Combine(asset.Path, "HelloWorld.csproj");
        string output = Path.Combine(asset.Path, "output");
        string binlogArgument = BinLogArgument([verb, outOfProcess.ToString(), Guid.NewGuid().ToString("N")]);
        Restore(project);

        string? msbuildPath = outOfProcess
            ? Path.Combine(SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "MSBuild.dll")
            : null;
        using var exported = new ActivityExports();
        using Activity? parent = Activities.Source.StartActivity("test-command");
        parent.Should().NotBeNull();
        parent!.TraceStateString = "sdk-test=parent";
        var parseResult = Parser.Parse(
            [
                "dotnet", verb, project, "--no-restore", "--configuration", "Debug", "--output", output,
                "--disable-build-servers", binlogArgument,
            ]);
        CommandBase command = verb == "pack"
            ? PackCommand.FromParseResult(parseResult, msbuildPath)
            : PublishCommand.FromParseResult(parseResult, msbuildPath);
        command.Should().BeAssignableTo<RestoringCommand>();
        ((RestoringCommand)command).SeparateRestoreCommand.Should().BeNull();
        if (outOfProcess)
        {
            ((RestoringCommand)command).GetProcessStartInfo().Arguments.Should().Contain(msbuildPath!);
        }

        exported.AssertNoSubmissions();
        exported.AssertNoReleasePropertyDiscovery();
        Activity.Current.Should().BeSameAs(parent);

        for (int execution = 0; execution < executions; execution++)
        {
            command.Execute().Should().Be(0);

            Activity.Current.Should().BeSameAs(parent);
            if (outOfProcess)
            {
                AssertForwardingContextRestored((RestoringCommand)command, parent);
            }
        }

        Activity[] submissions = exported.AssertSubmissionMeasurements(executions, parent);
        AssertBuildBoundaries(binlogArgument, submissions);
        TelemetryClient.Instance.Should().BeNull("local diagnostic collection must not initialize SDK telemetry");
        if (verb == "pack")
        {
            Directory.GetFiles(output, "*.nupkg").Should().ContainSingle();
        }
        else
        {
            File.Exists(Path.Combine(output, "HelloWorld.dll")).Should().BeTrue();
        }
    }

    [TestMethod]
    [DataRow("pack")]
    [DataRow("publish")]
    public void PackAndPublishExportReleaseSettingsDiscoveryBeforeSubmission(string verb)
    {
        var asset = TestAssetsManager.CopyTestAsset("HelloWorld", identifier: verb)
            .WithSource()
            .WithProjectChanges(projectXml => projectXml.Root!.Add(
                new XElement("PropertyGroup",
                    new XElement("PackRelease", "true"),
                    new XElement("PublishRelease", "true"))));
        string project = Path.Combine(asset.Path, "HelloWorld.csproj");
        string output = Path.Combine(asset.Path, "output");
        string binlogArgument = BinLogArgument([verb, Guid.NewGuid().ToString("N")]);
        Restore(project);

        using var exported = new ActivityExports();
        using Activity? parent = Activities.Source.StartActivity("test-command");
        parent.Should().NotBeNull();
        var parseResult = Parser.Parse(
            [
                "dotnet", verb, project, "--no-restore", "--output", output,
                "--disable-build-servers", binlogArgument,
            ]);
        var command = (RestoringCommand)(verb == "pack"
            ? PackCommand.FromParseResult(parseResult)
            : PublishCommand.FromParseResult(parseResult));

        command.MSBuildArguments.Should().Contain("--property:Configuration=Release");
        exported.AssertNoSubmissions();
        Activity discovery = exported.AssertReleasePropertyDiscovery(parent);
        Activity.Current.Should().BeSameAs(parent);

        command.Execute().Should().Be(0);

        Activity.Current.Should().BeSameAs(parent);
        Activity[] submissions = exported.AssertSubmissionMeasurements(expectedCount: 1, parent);
        (discovery.StartTimeUtc + discovery.Duration).Should().BeOnOrBefore(submissions[0].StartTimeUtc);
        AssertBuildBoundaries(binlogArgument, submissions);
    }

    [TestMethod]
    [DataRow("PackRelease")]
    [DataRow("PublishRelease")]
    public void DisabledReleaseSettingsDiscoveryDoesNotEmitAnActivity(string property)
    {
        Environment.SetEnvironmentVariable(EnvironmentVariableNames.DISABLE_PUBLISH_AND_PACK_RELEASE, "true");
        using var exported = new ActivityExports();
        var locator = new ReleasePropertyProjectLocator(
            userSpecifiedExplicitMSBuildProperties: null,
            propertyToCheck: property,
            commandOptions: new ReleasePropertyProjectLocator.DependentCommandOptions([]));

        locator.GetCustomDefaultConfigurationValueIfSpecified().Should().BeNull();

        exported.AssertNoReleasePropertyDiscovery();
        exported.AssertNoSubmissions();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SeparateRestoreExportsOneMeasurementPerExecutedSubmission(bool failRestore)
    {
        var asset = TestAssetsManager.CopyTestAsset("HelloWorld", identifier: failRestore.ToString()).WithSource();
        string binlogArgument = BinLogArgument([failRestore.ToString(), Guid.NewGuid().ToString("N")]);
        if (failRestore)
        {
            WriteFailingTarget(asset.Path, target: "Restore");
        }

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
        exitCode.Should().Be(failRestore ? 1 : 0);

        Activity.Current.Should().BeSameAs(parent);
        Activity[] submissions = exported.AssertSubmissionMeasurements(failRestore ? 1 : 2, parent);
        AssertBuildBoundaries(binlogArgument, submissions);
        if (!failRestore)
        {
            DateTime firstEnd = submissions[0].StartTimeUtc + submissions[0].Duration;
            submissions[1].StartTimeUtc.Should().BeOnOrAfter(firstEnd);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FileBasedPublishExportsItsBuildSubmission(bool fail)
    {
        var directory = TestAssetsManager.CreateTestDirectory(identifier: fail.ToString());
        string program = Path.Combine(directory.Path, "Program.cs");
        string output = Path.Combine(directory.Path, "output");
        string binlogArgument = BinLogArgument([fail.ToString(), Guid.NewGuid().ToString("N")]);
        File.WriteAllText(program, """
            #:property PublishAot=false
            #:property SelfContained=false
            #:property UseAppHost=false
            Console.WriteLine("File-based activity test");
            """);
        Restore(program);
        if (fail)
        {
            WriteFailingTarget(directory.Path);
        }

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

        exitCode.Should().Be(fail ? 1 : 0);
        Activity.Current.Should().BeSameAs(parent);
        Activity[] submissions = exported.AssertSubmissionMeasurements(expectedCount: 1, parent);
        AssertBuildBoundaries(binlogArgument, submissions);
        if (!fail)
        {
            File.Exists(Path.Combine(output, "Program.dll")).Should().BeTrue();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedPhysicalBuildStillExportsItsSubmission(bool outOfProcess)
    {
        var asset = TestAssetsManager.CopyTestAsset("HelloWorld", identifier: outOfProcess.ToString()).WithSource();
        string project = Path.Combine(asset.Path, "HelloWorld.csproj");
        string binlogArgument = BinLogArgument([Guid.NewGuid().ToString("N")]);
        Restore(project);
        WriteFailingTarget(asset.Path);
        using var exported = new ActivityExports();
        using Activity? parent = Activities.Source.StartActivity("test-command");
        parent.Should().NotBeNull();
        parent!.TraceStateString = "sdk-test=parent";
        var parseResult = Parser.Parse(
            [
                "dotnet", "publish", project, "--no-restore", "--configuration", "Debug",
                "--disable-build-servers", binlogArgument,
            ]);
        var command = (RestoringCommand)PublishCommand.FromParseResult(
            parseResult,
            outOfProcess ? Path.Combine(SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "MSBuild.dll") : null);
        exported.AssertNoSubmissions();

        command.Execute().Should().NotBe(0);

        Activity.Current.Should().BeSameAs(parent);
        Activity[] submissions = exported.AssertSubmissionMeasurements(expectedCount: 1, parent);
        AssertBuildBoundaries(binlogArgument, submissions);
        if (outOfProcess)
        {
            AssertForwardingContextRestored(command, parent);
        }
    }

    [TestMethod]
    public async Task OutOfProcessMSBuildSpanIsParentedByItsSubmission()
    {
        var asset = TestAssetsManager.CopyTestAsset("HelloWorld").WithSource();
        string project = Path.Combine(asset.Path, "HelloWorld.csproj");
        Restore(project);
        await using var collector = await TelemetryCollectorFixture.CreateAsync(TestContext.CancellationToken);

        var command = new DotnetCommand(
                Log, "publish", project, "--no-restore", "--configuration", "Debug", "--disable-build-servers")
            .WithEnvironmentVariable("DOTNET_CLI_RUN_MSBUILD_OUTOFPROC", "1")
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "0")
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER", "1")
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_DISABLE_TRACE_EXPORT", "1")
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_SHUTDOWN_TIMEOUT_MS", "15000")
            .WithEnvironmentVariable("OTEL_SDK_DISABLED", "false")
            .WithEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", collector.Endpoint.ToString())
            .WithEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");

        // Signal-specific settings take precedence over the common loopback endpoint.
        string[] signals = ["TRACES", "METRICS", "LOGS"];
        string[] settings = ["ENDPOINT", "PROTOCOL", "HEADERS"];
        foreach (string signal in signals)
        {
            foreach (string setting in settings)
            {
                command.EnvironmentToRemove.Add($"OTEL_EXPORTER_OTLP_{signal}_{setting}");
            }
        }

        command.EnvironmentToRemove.Add("OTEL_EXPORTER_OTLP_HEADERS");
        command.EnvironmentToRemove.Add("DOTNET_CLI_TELEMETRY_LOG_PATH");
        command.Execute().Should().Pass();

        await collector.WaitForEventsAsync(
            events => events.Any(e => e.Name == "dotnet/cli/msbuild/build")
                && events.Any(e => e.Name == "dotnet/cli/command/finish"),
            cancellationToken: TestContext.CancellationToken);
        IReadOnlyList<CollectedSpan> spans = await collector.GetSpansAsync(TestContext.CancellationToken);
        CollectedSpan submission = spans.Should().ContainSingle(span => span.Name == "msbuild-submission").Subject;
        CollectedSpan build = spans.Should().ContainSingle(span => span.Name == "msbuild").Subject;
        submission.TraceId.Should().NotBeNullOrEmpty();
        submission.SpanId.Should().NotBeNullOrEmpty();
        build.TraceId.Should().Be(submission.TraceId);
        build.ParentSpanId.Should().Be(submission.SpanId);
        spans.Should().Contain(span => span.TraceId == submission.TraceId && span.SpanId == submission.ParentSpanId);
    }

    [TestMethod]
    public void LoggerAndSubmissionActivitiesAreNestedAndMeasuredSeparately()
    {
        using var exported = new ActivityExports();
        using Activity? parent = Activities.Source.StartActivity("test-command");
        parent.Should().NotBeNull();
        using Activity? submission = Activities.Source.StartActivity("msbuild-submission");
        submission.Should().NotBeNull();
        var eventSource = new PersistentDispatcher([]);
        var logger = new MSBuildLogger(new FakeTelemetry());
        logger.Initialize(eventSource);

        try
        {
            eventSource.Dispatch(new BuildStartedEventArgs("Build started.", helpKeyword: null));
            Activity? build = Activity.Current;
            build.Should().NotBeNull();
            build!.OperationName.Should().Be("msbuild");
            build.TraceId.Should().Be(submission!.TraceId);
            build.ParentSpanId.Should().Be(submission.SpanId);

            eventSource.Dispatch(new BuildFinishedEventArgs("Build finished.", helpKeyword: null, succeeded: true));
            Activity.Current.Should().BeSameAs(build, "the logger remains active through final build telemetry");
        }
        finally
        {
            logger.Shutdown();
        }

        Activity.Current.Should().BeSameAs(submission);
        submission!.Stop();
        Activity.Current.Should().BeSameAs(parent);
        exported.AssertSubmissionMeasurements(expectedCount: 1, parent);
    }

    private static void AssertForwardingContextRestored(RestoringCommand command, Activity parent)
    {
        ProcessStartInfo forwarded = command.GetProcessStartInfo();
        forwarded.Environment[Activities.TRACEPARENT].Should().Be(parent.Id);
        forwarded.Environment[Activities.TRACESTATE].Should().Be(parent.TraceStateString);
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
            BuildEventArgs[] boundaries = BinaryLog.ReadRecords(binlog)
                .Select(record => record.Args)
                .OfType<BuildEventArgs>()
                .Where(args => args is BuildStartedEventArgs or BuildFinishedEventArgs)
                .ToArray();
            BuildStartedEventArgs started = boundaries.OfType<BuildStartedEventArgs>().Should().ContainSingle().Subject;
            BuildFinishedEventArgs finished = boundaries.OfType<BuildFinishedEventArgs>().Should().ContainSingle().Subject;
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

    private static void WriteFailingTarget(string directory, string target = "CoreCompile")
    {
        File.WriteAllText(Path.Combine(directory, "Directory.Build.targets"), $"""
            <Project>
              <Target Name="FailActivityTest" BeforeTargets="{target}">
                <Error Text="Expected activity test failure." />
              </Target>
            </Project>
            """);
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
                .AddSource(Activities.Source.Name)
                .SetSampler(new AlwaysOnSampler())
                .AddInMemoryExporter(_activities)
                .Build();
            // A manual reader collects once, without periodic snapshots or timer-based assertions.
            _meter = Sdk.CreateMeterProviderBuilder()
                .AddMeter(Activities.Source.Name)
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
            ActivitySpanId parentSpanId = parent is null ? default : parent.SpanId;
            submissions.Should().OnlyContain(activity => activity.ParentSpanId == parentSpanId);

            Metric metric = _metrics.Should().ContainSingle(
                metric => metric.Name == "dotnet.cli.activity.duration").Subject;
            metric.MeterName.Should().Be("dotnet-cli");
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
