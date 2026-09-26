// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Utils;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using BinaryLog = Microsoft.Build.Logging.StructuredLogger.BinaryLog;
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
    [DataRow("publish", true, 2)]
    public void PhysicalPackAndPublishExportSubmissionHistograms(string verb, bool outOfProcess, int executions)
    {
        var asset = TestAssetsManager.CopyTestAsset("HelloWorld", identifier: $"{verb}-{outOfProcess}")
            .WithSource()
            .WithProjectChanges(projectXml => projectXml.Root!.Add(
                new XElement("PropertyGroup",
                    new XElement("PackRelease", "true"),
                    new XElement("PublishRelease", "true")),
                new XElement("Target",
                    new XAttribute("Name", "ReportActivityContext"),
                    new XAttribute("BeforeTargets", "Build"),
                    new XElement("Message",
                        new XAttribute("Importance", "High"),
                        new XAttribute("Text", "ACTIVITY_TRACEPARENT=$(TRACEPARENT)")),
                    new XElement("Message",
                        new XAttribute("Importance", "High"),
                        new XAttribute("Text", "ACTIVITY_TRACESTATE=$(TRACESTATE)")))));
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
        string[] arguments =
        [
            "dotnet", verb, project, "--no-restore", "--output", output,
            "--disable-build-servers", binlogArgument,
        ];
        var configured = Parser.Parse([.. arguments, "--configuration", "Debug"]);
        _ = verb == "pack"
            ? PackCommand.FromParseResult(configured, msbuildPath)
            : PublishCommand.FromParseResult(configured, msbuildPath);
        exported.AssertNoReleasePropertyDiscovery();

        var parseResult = Parser.Parse(arguments);
        var command = (RestoringCommand)(verb == "pack"
            ? PackCommand.FromParseResult(parseResult, msbuildPath)
            : PublishCommand.FromParseResult(parseResult, msbuildPath));
        command.MSBuildArguments.Should().Contain("--property:Configuration=Release");
        command.SeparateRestoreCommand.Should().BeNull();
        Activity discovery = exported.AssertReleasePropertyDiscovery(parent);
        if (outOfProcess)
        {
            command.GetProcessStartInfo().Arguments.Should().Contain(msbuildPath!);
        }

        exported.AssertNoSubmissions();
        Activity.Current.Should().BeSameAs(parent);

        for (int execution = 0; execution < executions; execution++)
        {
            command.Execute().Should().Be(0);

            Activity.Current.Should().BeSameAs(parent);
            if (outOfProcess)
            {
                AssertForwardingContextRestored(command, parent);
            }
        }

        Activity[] submissions = exported.AssertSubmissionMeasurements(executions, parent);
        (discovery.StartTimeUtc + discovery.Duration).Should().BeOnOrBefore(submissions[0].StartTimeUtc);
        AssertBuildBoundaries(binlogArgument, submissions, verifyForwardedContext: outOfProcess);
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
    public void DisabledReleaseSettingsDiscoveryDoesNotEmitAnActivity()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariableNames.DISABLE_PUBLISH_AND_PACK_RELEASE, "true");
        using var exported = new ActivityExports();
        var locator = new ReleasePropertyProjectLocator(
            userSpecifiedExplicitMSBuildProperties: null,
            propertyToCheck: "PackRelease",
            commandOptions: new ReleasePropertyProjectLocator.DependentCommandOptions([]));

        locator.GetCustomDefaultConfigurationValueIfSpecified().Should().BeNull();

        exported.AssertNoReleasePropertyDiscovery();
        exported.AssertSubmissionMeasurements(expectedCount: 0, parent: null);
    }

    [TestMethod]
    [DataRow(false, "success")]
    [DataRow(true, "success")]
    [DataRow(true, "missing-target")]
    public void PreparatoryDeviceDiscoveryMeasuresTargetsAndSkipsMissingTargets(bool sharedSession, string scenario)
    {
        var directory = TestAssetsManager.CreateTestDirectory(identifier: $"{sharedSession}-{scenario}");
        string project = Path.Combine(directory.Path, "devices.csproj");
        string restored = Path.Combine(directory.Path, "restored.txt");
        string computed = Path.Combine(directory.Path, "computed.txt");
        File.Delete(restored);
        File.Delete(computed);
        var projectXml = XDocument.Parse($"""
            <Project>
              <Target Name="Restore">
                <Error Condition="'$(TargetFramework)' != ''" Text="Restore must evaluate the outer project." />
                <WriteLinesToFile File="$(MSBuildProjectDirectory){Path.DirectorySeparatorChar}restored.txt" Lines="restored" />
              </Target>
              <Target Name="ComputeAvailableDevices" Returns="@(Devices)">
                <Error Condition="!Exists('$(MSBuildProjectDirectory){Path.DirectorySeparatorChar}restored.txt')"
                       Text="Device discovery must follow restore." />
                <WriteLinesToFile File="$(MSBuildProjectDirectory){Path.DirectorySeparatorChar}computed.txt" Lines="computed" />
                <ItemGroup>
                  <Devices Include="test-device">
                    <Description>$(TargetFramework)</Description>
                    <RuntimeIdentifier>test-rid</RuntimeIdentifier>
                  </Devices>
                </ItemGroup>
              </Target>
            </Project>
            """);
        if (scenario == "missing-target")
        {
            projectXml.Root!.Elements("Target")
                .Single(target => target.Attribute("Name")!.Value == "ComputeAvailableDevices").Remove();
        }

        projectXml.Save(project);
        var msbuildArgs = SolutionAndProjectUtility.AnalyzeStandardTestMSBuildArgs(
            [$"-p:TargetFramework={ToolsetInfo.CurrentTargetFramework}"]);
        using var exported = new ActivityExports();
        using Activity? parent = Activities.Source.StartActivity("test-command");
        parent.Should().NotBeNull();
        var targetEvents = new ConcurrentQueue<BuildEventArgs>();
        var dispatcher = new PersistentDispatcher([]);
        dispatcher.AnyEventRaised += (_, args) =>
        {
            if (args is TargetStartedEventArgs or TargetFinishedEventArgs)
            {
                targetEvents.Enqueue(args);
            }
        };
        var logger = new FacadeLogger(dispatcher);
        using MSBuildSession? session = sharedSession ? new MSBuildSession(msbuildArgs, logger) : null;
        using var selector = new RunCommandSelector(
            project, isInteractive: false, msbuildArgs, new Dictionary<string, string>(),
            sharedSession ? "dotnet test" : "dotnet run", binaryLogger: logger, buildSession: session);

        selector.TrySelectTargetFramework(out string? selectedFramework).Should().BeTrue();
        selectedFramework.Should().BeNull();
        selector.HasValidProject.Should().BeFalse();
        exported.AssertActivities("project-selection", expectedCount: 0, parent);

        bool success = selector.TryComputeAvailableDevices(noRestore: false, out var devices, out bool restoreWasPerformed);
        success.Should().Be(scenario == "success");
        restoreWasPerformed.Should().Be(success);
        if (success)
        {
            RunCommandSelector.DeviceItem device = devices.Should().ContainSingle().Subject;
            device.Id.Should().Be("test-device");
            device.Description.Should().Be(ToolsetInfo.CurrentTargetFramework);
            device.RuntimeIdentifier.Should().Be("test-rid");
            selector.TryComputeAvailableDevices(noRestore: true, out var cachedDevices, out bool restoredAgain).Should().BeTrue();
            cachedDevices.Should().ContainSingle().Which.Should().Be(device);
            restoredAgain.Should().BeFalse();
            File.ReadAllLines(restored).Should().Equal("restored");
            File.ReadAllLines(computed).Should().Equal("computed", "computed");
        }
        else
        {
            devices.Should().BeNull();
            File.Exists(restored).Should().BeFalse();
            File.Exists(computed).Should().BeFalse();
        }

        session?.Complete();

        Activity.Current.Should().BeSameAs(parent);
        exported.AssertActivities("project-selection", success ? 2 : 1, parent);
        Activity[] discovery = exported.AssertActivities("device-discovery", success ? 2 : 0, parent);
        exported.AssertSubmissionMeasurements(expectedCount: 0, parent);
        TargetStartedEventArgs[] starts = targetEvents.OfType<TargetStartedEventArgs>().ToArray();
        TargetFinishedEventArgs[] finishes = targetEvents.OfType<TargetFinishedEventArgs>().ToArray();
        string[] expectedTargets = success ? ["Restore", "ComputeAvailableDevices", "ComputeAvailableDevices"] : [];
        starts.Select(args => args.TargetName).Should().Equal(expectedTargets);
        finishes.Select(args => args.TargetName).Should().Equal(starts.Select(args => args.TargetName));
        for (int i = 0; i < starts.Length; i++)
        {
            DateTime start = starts[i].Timestamp.ToUniversalTime();
            DateTime end = finishes[i].Timestamp.ToUniversalTime();
            discovery.Should().ContainSingle(activity =>
                activity.StartTimeUtc <= start && activity.StartTimeUtc + activity.Duration >= end);
        }
    }

    [TestMethod]
    public void PreparatoryTestProjectDiscoveryIncludesInnerFrameworksAndSkipsExplicitDevices()
    {
        var directory = TestAssetsManager.CreateTestDirectory();
        string project = Path.Combine(directory.Path, "discovery.csproj");
        File.WriteAllText(project, $"""
            <Project>
              <PropertyGroup>
                <TargetFrameworks>{ToolsetInfo.CurrentTargetFramework};{ToolsetInfo.NextTargetFramework}</TargetFrameworks>
                <SelectedFramework>$(TargetFramework)</SelectedFramework>
              </PropertyGroup>
            </Project>
            """);
        var definition = new TestCommandDefinition.MicrosoftTestingPlatform();
        var options = MSBuildUtility.GetBuildOptions(definition.Parse(["--project", project, "--no-build"]));
        using var exported = new ActivityExports();
        using Activity? parent = Activities.Source.StartActivity("test-command");
        parent.Should().NotBeNull();
        using var session = new MSBuildSession(
            SolutionAndProjectUtility.AnalyzeStandardTestMSBuildArgs(options.MSBuildArgs), logger: null);

        SolutionAndProjectUtility.EvaluateProjectForDeviceSelection(
            project, options with { Device = "test-device" }, session).Should().BeNull();
        SolutionAndProjectUtility.EvaluateProjectForDeviceSelection(
            project, options with { MSBuildArgs = ["-p:Device=test-device"] }, session).Should().BeNull();
        session.ProjectCollection.LoadedProjects.Should().BeEmpty();
        exported.AssertActivities("test-project-discovery", expectedCount: 0, parent);

        var evaluation = SolutionAndProjectUtility.EvaluateProjectForDeviceSelection(project, options, session);
        evaluation.Should().NotBeNull();
        evaluation!.EvaluatedProjects.Should().HaveCount(3);
        evaluation.ProjectsByFramework.Select(entry => entry.Key).Should().BeEquivalentTo(
            [ToolsetInfo.CurrentTargetFramework, ToolsetInfo.NextTargetFramework]);
        foreach (var (framework, instance) in evaluation.ProjectsByFramework)
        {
            instance.GetPropertyValue("SelectedFramework").Should().Be(framework);
        }

        Activity.Current.Should().BeSameAs(parent);
        exported.AssertActivities("test-project-discovery", expectedCount: 1, parent);
        exported.AssertSubmissionMeasurements(expectedCount: 0, parent);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void PreparatoryTestEnvironmentDiscoveryEndsBeforeBuild(bool passEnvironment)
    {
        var directory = TestAssetsManager.CreateTestDirectory(identifier: passEnvironment.ToString());
        string project = Path.Combine(directory.Path, "environment.csproj");
        string observed = Path.Combine(directory.Path, "observed.txt");
        string propsFile = Path.Combine(directory.Path, "obj", "dotnet-test-env.props");
        string binlogArgument = BinLogArgument([Guid.NewGuid().ToString("N")]);
        File.Delete(observed);
        File.WriteAllText(project, $"""
            <Project>
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                <IntermediateOutputPath>obj</IntermediateOutputPath>
              </PropertyGroup>
              <Import Project="$(CustomBeforeMicrosoftCommonProps)" Condition="'$(CustomBeforeMicrosoftCommonProps)' != ''" />
              <ItemGroup>
                <ProjectCapability Include="{Constants.RuntimeEnvironmentVariableSupport}" />
              </ItemGroup>
              <Target Name="{TestCommandDefinition.MicrosoftTestingPlatform.BuildTargetName}">
                <PropertyGroup>
                  <_ObservedEnvironmentVariables>@(RuntimeEnvironmentVariable->'%(Identity)=%(Value)')</_ObservedEnvironmentVariables>
                </PropertyGroup>
                <WriteLinesToFile File="$(MSBuildProjectDirectory){Path.DirectorySeparatorChar}observed.txt"
                                  Lines="variables=$(_ObservedEnvironmentVariables)" Overwrite="true" />
              </Target>
            </Project>
            """);
        var definition = new TestCommandDefinition.MicrosoftTestingPlatform();
        var options = MSBuildUtility.GetBuildOptions(
            definition.Parse(["--project", project, "--device", "test-device", "--no-restore", binlogArgument])) with
        {
            EnvironmentVariables = passEnvironment
                ? new Dictionary<string, string> { ["ACTIVITY_TEST"] = "value" }
                : new Dictionary<string, string>(),
        };
        using var exported = new ActivityExports();
        using Activity? parent = Activities.Source.StartActivity("test-command");
        parent.Should().NotBeNull();
        using var session = new MSBuildSession(
            SolutionAndProjectUtility.AnalyzeStandardTestMSBuildArgs(options.MSBuildArgs), logger: null);

        var result = MSBuildUtility.GetProjectsFromProject(project, options, session);

        result.BuildExitCode.Should().Be(0);
        result.Projects.Should().BeEmpty();
        session.Complete();
        Activity.Current.Should().BeSameAs(parent);
        Activity[] discovery = exported.AssertActivities("test-environment-discovery", passEnvironment ? 1 : 0, parent);
        Activity[] submissions = exported.AssertSubmissionMeasurements(expectedCount: 1, parent);
        File.Exists(propsFile).Should().BeFalse();
        File.ReadAllText(observed).Trim().Should().Be(passEnvironment ? "variables=ACTIVITY_TEST=value" : "variables=");
        AssertBuildBoundaries(binlogArgument, submissions);
        if (passEnvironment)
        {
            (discovery[0].StartTimeUtc + discovery[0].Duration).Should().BeOnOrBefore(submissions[0].StartTimeUtc);
        }
    }

    [TestMethod]
    public void PreparatoryTestTargetFrameworkDiscoverySkipsAnExplicitFramework()
    {
        var directory = TestAssetsManager.CreateTestDirectory();
        string project = Path.Combine(directory.Path, "framework.csproj");
        File.WriteAllText(project, $"""
            <Project>
              <PropertyGroup>
                <TargetFrameworks>{ToolsetInfo.CurrentTargetFramework}</TargetFrameworks>
              </PropertyGroup>
            </Project>
            """);
        var definition = new TestCommandDefinition.MicrosoftTestingPlatform();
        string[] arguments = ["--project", project, "--device", "test-device", "--no-build", "--no-restore"];
        using var exported = new ActivityExports();
        using Activity? parent = Activities.Source.StartActivity("test-command");
        parent.Should().NotBeNull();
        var command = new MicrosoftTestingPlatformTestCommand();

        // The SDK-less project has no test modules, so execution stops after discovery.
        command.Run(definition.Parse(arguments), isHelp: false).Should().Be(1);
        exported.AssertActivities("test-target-framework-discovery", expectedCount: 1, parent);
        command.Run(definition.Parse([.. arguments, "--framework", ToolsetInfo.CurrentTargetFramework]), isHelp: false)
            .Should().Be(1);

        Activity.Current.Should().BeSameAs(parent);
        exported.AssertActivities("test-target-framework-discovery", expectedCount: 1, parent);
        exported.AssertActivities("test-project-discovery", expectedCount: 0, parent);
        exported.AssertSubmissionMeasurements(expectedCount: 0, parent);
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

    private static void AssertForwardingContextRestored(RestoringCommand command, Activity parent)
    {
        ProcessStartInfo forwarded = command.GetProcessStartInfo();
        forwarded.Environment[Activities.TRACEPARENT].Should().Be(parent.Id);
        forwarded.Environment[Activities.TRACESTATE].Should().Be(parent.TraceStateString);
    }

    private static void AssertBuildBoundaries(string binlogArgument, Activity[] submissions, bool verifyForwardedContext = false)
    {
        string binlogPattern = Path.GetFullPath(binlogArgument["/bl:".Length..]);
        string[] binlogs = Directory.GetFiles(
            Path.GetDirectoryName(binlogPattern)!,
            Path.GetFileName(binlogPattern).Replace("{}", "*", StringComparison.Ordinal));
        binlogs.Should().HaveCount(submissions.Length);
        List<(DateTime Start, DateTime End, BuildEventArgs[] Events)> builds = [];
        foreach (string binlog in binlogs)
        {
            BuildEventArgs[] events = BinaryLog.ReadRecords(binlog)
                .Select(record => record.Args)
                .OfType<BuildEventArgs>()
                .ToArray();
            BuildStartedEventArgs started = events.OfType<BuildStartedEventArgs>().Should().ContainSingle().Subject;
            BuildFinishedEventArgs finished = events.OfType<BuildFinishedEventArgs>().Should().ContainSingle().Subject;
            builds.Add((started.Timestamp.ToUniversalTime(), finished.Timestamp.ToUniversalTime(), events));
        }

        builds.Sort((left, right) => left.Start.CompareTo(right.Start));
        for (int i = 0; i < submissions.Length; i++)
        {
            submissions[i].StartTimeUtc.Should().BeOnOrBefore(builds[i].Start);
            (submissions[i].StartTimeUtc + submissions[i].Duration).Should().BeOnOrAfter(builds[i].End);
            if (verifyForwardedContext)
            {
                string?[] messages = builds[i].Events.OfType<BuildMessageEventArgs>().Select(args => args.Message).ToArray();
                messages.Should().Contain($"ACTIVITY_TRACEPARENT={submissions[i].Id}");
                messages.Should().Contain($"ACTIVITY_TRACESTATE={submissions[i].TraceStateString}");
            }
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

    private static void WriteFailingTarget(string directory, string target)
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

        public Activity[] AssertActivities(string name, int expectedCount, Activity? parent)
        {
            Activity[] activities = _activities
                .Where(activity => activity.OperationName == name)
                .OrderBy(activity => activity.StartTimeUtc)
                .ToArray();
            activities.Should().HaveCount(expectedCount);
            ActivitySpanId parentSpanId = parent?.SpanId ?? default;
            foreach (Activity activity in activities)
            {
                activity.ParentSpanId.Should().Be(parentSpanId);
            }
            return activities;
        }

        public Activity[] AssertSubmissionMeasurements(int expectedCount, Activity? parent)
        {
            _traces.ForceFlush().Should().BeTrue();
            _meter.ForceFlush().Should().BeTrue();

            Activity[] submissions = AssertActivities("msbuild-submission", expectedCount, parent);
            if (_activities.Count == 0)
            {
                _metrics.Should().BeEmpty();
                return submissions;
            }

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
