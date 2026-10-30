// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.DotNet.ProjectTools;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public sealed class TestApplicationLaunchTests
{
    [TestMethod]
    public void CreateProcessStartInfo_TopLevelAffectedTests_AddsOptionAndRunMarker()
    {
        using TestApplication application = CreateApplication(
            new TestOptions(false, false, TestListFormat.Text) { AffectedTests = true });

        ProcessStartInfo startInfo = application.CreateProcessStartInfo();

        startInfo.Arguments.Should().Contain("--affected-tests");
        startInfo.Environment[TestOptions.AffectedTestsModeEnvironmentVariable]
            .Should().Be(TestOptions.RunAffectedTestsMode);
    }

    [TestMethod]
    public void CreateProcessStartInfo_TopLevelCollectTestMap_AddsOptionAndCollectMarker()
    {
        using TestApplication application = CreateApplication(
            new TestOptions(false, false, TestListFormat.Text) { CollectTestMap = true });

        ProcessStartInfo startInfo = application.CreateProcessStartInfo();

        startInfo.Arguments.Should().Contain("--collect-test-map");
        startInfo.Arguments.LastIndexOf("--collect-test-map", StringComparison.Ordinal)
            .Should().Be(startInfo.Arguments.IndexOf("--collect-test-map", StringComparison.Ordinal));
        startInfo.Environment[TestOptions.AffectedTestsModeEnvironmentVariable]
            .Should().Be(TestOptions.CollectTestMapMode);
    }

    [TestMethod]
    public void CreateProcessStartInfo_ForwardedAffectedTests_PreservesOriginalArgumentPosition()
    {
        string[] forwardedArguments = ["--minimum-expected-tests", "--affected-tests", "1"];
        using TestApplication application = CreateApplication(
            new TestOptions(false, false, TestListFormat.Text)
            {
                AffectedTests = true,
                AffectedTestsForwarded = true,
            },
            forwardedArguments);

        ProcessStartInfo startInfo = application.CreateProcessStartInfo();

        int minimumIndex = startInfo.Arguments.IndexOf("--minimum-expected-tests", StringComparison.Ordinal);
        int affectedIndex = startInfo.Arguments.IndexOf("--affected-tests", StringComparison.Ordinal);
        int valueIndex = startInfo.Arguments.IndexOf(" 1", affectedIndex, StringComparison.Ordinal);
        minimumIndex.Should().BeGreaterThanOrEqualTo(0);
        affectedIndex.Should().BeGreaterThan(minimumIndex);
        valueIndex.Should().BeGreaterThan(affectedIndex);
        startInfo.Arguments.LastIndexOf("--affected-tests", StringComparison.Ordinal).Should().Be(affectedIndex);
        startInfo.Environment[TestOptions.AffectedTestsModeEnvironmentVariable]
            .Should().Be(TestOptions.RunAffectedTestsMode);
    }

    [TestMethod]
    public void CreateProcessStartInfo_ForwardedCollectTestMap_PreservesOriginalArgumentPosition()
    {
        string[] forwardedArguments = ["--filter", "TestClass", "--collect-test-map"];
        using TestApplication application = CreateApplication(
            new TestOptions(false, false, TestListFormat.Text)
            {
                CollectTestMap = true,
                CollectTestMapForwarded = true,
            },
            forwardedArguments);

        ProcessStartInfo startInfo = application.CreateProcessStartInfo();

        int filterIndex = startInfo.Arguments.IndexOf("--filter", StringComparison.Ordinal);
        int collectIndex = startInfo.Arguments.IndexOf("--collect-test-map", StringComparison.Ordinal);
        filterIndex.Should().BeGreaterThanOrEqualTo(0);
        collectIndex.Should().BeGreaterThan(filterIndex);
        startInfo.Arguments.LastIndexOf("--collect-test-map", StringComparison.Ordinal).Should().Be(collectIndex);
        startInfo.Environment[TestOptions.AffectedTestsModeEnvironmentVariable]
            .Should().Be(TestOptions.CollectTestMapMode);
    }

    [TestMethod]
    public void CreateProcessStartInfo_OrdinaryRun_RemovesInheritedModuleMarker()
    {
        using TestApplication application = CreateApplication(
            new TestOptions(false, false, TestListFormat.Text),
            environmentVariables: new Dictionary<string, string>
            {
                [TestOptions.AffectedTestsModeEnvironmentVariable] = TestOptions.CollectTestMapMode,
            });

        ProcessStartInfo startInfo = application.CreateProcessStartInfo();

        startInfo.Environment.ContainsKey(TestOptions.AffectedTestsModeEnvironmentVariable).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("browser-wasm")]
    [DataRow("wasi-wasm")]
    public void CreateProcessStartInfo_WebAssembly_UsesAuthenticatedHttpTransport(string runtimeIdentifier)
    {
        using TestApplication application = CreateApplication(
            new TestOptions(false, false, TestListFormat.Text),
            runtimeIdentifier: runtimeIdentifier);

        ProcessStartInfo startInfo = application.CreateProcessStartInfo();
        Assert.IsNotNull(application.HttpResponseFilePath);
        string responseFilePath = application.HttpResponseFilePath!;
        string responseFileContents = File.ReadAllText(responseFilePath);

        startInfo.Arguments.Should().Contain("@" + responseFilePath);
        startInfo.Arguments.Should().NotContain(CliConstants.DotNetTestPipeOptionKey);
        startInfo.Arguments.Should().NotContain(CliConstants.DotNetTestHttpEndpointOptionKey);
        startInfo.Arguments.Should().NotContain(CliConstants.DotNetTestHttpTokenOptionKey);
        responseFileContents.Should().Contain($"{CliConstants.ServerOptionKey} {CliConstants.ServerOptionValue}");
        responseFileContents.Should().Contain($"{CliConstants.DotNetTestTransportOptionKey} {CliConstants.DotNetTestHttpTransportValue}");
        responseFileContents.Should().Contain(CliConstants.DotNetTestHttpEndpointOptionKey);
        responseFileContents.Should().Contain(CliConstants.DotNetTestHttpTokenOptionKey);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(responseFilePath)
                .Should()
                .Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        string loggedArguments = application.GetArgumentsForLogging(startInfo.Arguments);
        loggedArguments.Should().NotContain("http://127.0.0.1:");
        loggedArguments.Should().NotContain(CliConstants.DotNetTestHttpTokenOptionKey);
    }

    [TestMethod]
    public void Dispose_BrowserWasm_DeletesHttpTransportResponseFile()
    {
        string responseFilePath;
        using (TestApplication application = CreateApplication(
            new TestOptions(false, false, TestListFormat.Text),
            runtimeIdentifier: "browser-wasm"))
        {
            application.CreateProcessStartInfo();
            responseFilePath = application.HttpResponseFilePath!;
            File.Exists(responseFilePath).Should().BeTrue();
        }

        File.Exists(responseFilePath).Should().BeFalse();
    }

    [TestMethod]
    public void CreateProcessStartInfo_Desktop_UsesNamedPipeTransport()
    {
        using TestApplication application = CreateApplication(
            new TestOptions(false, false, TestListFormat.Text),
            runtimeIdentifier: "win-x64");

        ProcessStartInfo startInfo = application.CreateProcessStartInfo();

        startInfo.Arguments.Should().Contain(CliConstants.DotNetTestPipeOptionKey);
        startInfo.Arguments.Should().NotContain(CliConstants.DotNetTestTransportOptionKey);
        startInfo.Arguments.Should().NotContain(CliConstants.DotNetTestHttpEndpointOptionKey);
        startInfo.Arguments.Should().NotContain(CliConstants.DotNetTestHttpTokenOptionKey);
    }

    [TestMethod]
    public void CreateProcessStartInfo_LaunchProfileAffectedOption_DoesNotCreateSdkMarker()
    {
        var launchProfile = new ProjectLaunchProfile
        {
            CommandLineArgs = "--affected-tests",
            EnvironmentVariables = ImmutableDictionary<string, string>.Empty
                .Add(TestOptions.AffectedTestsModeEnvironmentVariable, TestOptions.RunAffectedTestsMode),
        };
        using TestApplication application = CreateApplication(
            new TestOptions(false, false, TestListFormat.Text),
            launchProfile: launchProfile);

        ProcessStartInfo startInfo = application.CreateProcessStartInfo();

        startInfo.Arguments.Should().Contain("--affected-tests");
        startInfo.Environment.ContainsKey(TestOptions.AffectedTestsModeEnvironmentVariable).Should().BeFalse();
    }

    [TestMethod]
    public void CreateProcessStartInfo_ArtifactWorker_PreservesLaunchProfileDiagnosticsOnly()
    {
        var profile = new ProjectLaunchProfile
        {
            CommandLineArgs = "--diagnostic --diagnostic-verbosity debug --filter NotForTheWorker",
        };
        using TestApplication application = CreateApplication(
            new TestOptions(false, false, TestListFormat.Text, IsArtifactPostProcessing: true),
            launchProfile: profile);

        string arguments = application.CreateProcessStartInfo().Arguments;

        arguments.Should().Contain("--diagnostic --diagnostic-verbosity debug")
            .And.NotContain("--filter")
            .And.NotContain("NotForTheWorker");
    }

    [TestMethod]
    public void CreateProcessStartInfo_ArtifactWorker_PreservesResponseFileDiagnosticsOnly()
    {
        string responseFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N} reporting.rsp");
        try
        {
            File.WriteAllText(responseFile, "--diagnostic --diagnostic-file-prefix \"worker logs\" --filter NotForTheWorker");
            using TestApplication application = CreateApplication(
                new TestOptions(false, false, TestListFormat.Text, IsArtifactPostProcessing: true),
                forwardedArguments: ["@" + responseFile]);

            string arguments = application.CreateProcessStartInfo().Arguments;

            arguments.Should().Contain("--diagnostic --diagnostic-file-prefix \"worker logs\"")
                .And.NotContain("--filter")
                .And.NotContain("NotForTheWorker")
                .And.NotContain(responseFile);
        }
        finally
        {
            File.Delete(responseFile);
        }
    }

    [TestMethod]
    [DataRow("1.0.0")]
    [DataRow("1.3.0")]
    public void CreateHandshakeMessage_AdvertisesRequiredProcessingIndependentlyOfProtocolVersion(string version)
    {
        using TestApplication application = CreateApplication(
            new TestOptions(false, false, TestListFormat.Text),
            artifactPostProcessingManager: new ArtifactPostProcessingManager());

        application.CreateHandshakeMessage(version).Properties[HandshakeMessagePropertyNames.RequiredPostProcessingSupported]
            .Should().Be(bool.TrueString);
    }

    [TestMethod]
    [DataRow("help")]
    [DataRow("discovery")]
    [DataRow("collection")]
    [DataRow("worker")]
    [DataRow("opt-out")]
    [DataRow("no-manager")]
    [DataRow("cancelled")]
    [DataRow("rejected")]
    [DataRow("browser-wasm")]
    [DataRow("wasi-wasm")]
    public void CreateHandshakeMessage_DoesNotPromiseUnsupportedOrSuppressedProcessing(string scenario)
    {
        using var cancellation = new CancellationTokenSource();
        if (scenario == "cancelled")
        {
            cancellation.Cancel();
        }

        var options = new TestOptions(
            scenario == "help", scenario == "discovery", TestListFormat.Text,
            IsArtifactPostProcessing: scenario == "worker")
        {
            CollectTestMap = scenario == "collection",
        };
        using TestApplication application = CreateApplication(
            options,
            runtimeIdentifier: scenario,
            artifactPostProcessingManager: scenario == "no-manager"
                ? null
                : new ArtifactPostProcessingManager(enabled: scenario != "opt-out", cancellation.Token));

        application.CreateHandshakeMessage(scenario == "rejected" ? string.Empty : "1.3.0")
            .Properties.Should().NotContainKey(HandshakeMessagePropertyNames.RequiredPostProcessingSupported);
    }

    private static TestApplication CreateApplication(
        TestOptions testOptions,
        IEnumerable<string>? forwardedArguments = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        ProjectLaunchProfile? launchProfile = null,
        string runtimeIdentifier = "",
        ArtifactPostProcessingManager? artifactPostProcessingManager = null)
    {
        var module = new TestModule(
            new RunProperties("dotnet", "test.dll", null, runtimeIdentifier, string.Empty, string.Empty),
            ProjectFullPath: "test.csproj",
            TargetFramework: "net11.0",
            IsTestingPlatformApplication: true,
            LaunchSettings: launchProfile,
            TargetPath: "test.dll",
            DotnetRootArchVariableName: null,
            EnvironmentVariables: environmentVariables ?? ImmutableDictionary<string, string>.Empty);
        var buildOptions = new BuildOptions(
            new PathOptions(null, null, null, null, ResultsDirectoryLayout.Flat, null, null),
            HasNoRestore: false,
            HasNoBuild: false,
            Verbosity: null,
            NoLaunchProfile: false,
            NoLaunchProfileArguments: false,
            TestApplicationArguments: forwardedArguments?.ToImmutableArray() ?? [],
            MSBuildArgs: [],
            Device: null,
            ListDevices: false,
            EnvironmentVariables: ImmutableDictionary<string, string>.Empty);
        var reporter = new TerminalTestReporter(
            new CapturingConsole(),
            new TerminalTestReporterOptions
            {
                AnsiMode = AnsiMode.SimpleAnsi,
                ShowProgress = false,
            });

        return new TestApplication(
            module,
            buildOptions,
            testOptions,
            TestResultsDirectoryResolver.CreateShared(buildOptions.PathOptions, Directory.GetCurrentDirectory()),
            reporter,
            _ => { },
            artifactPostProcessingManager: artifactPostProcessingManager,
            artifactPostProcessingInvocation: testOptions.IsArtifactPostProcessing
                ? new ArtifactPostProcessingInvocation("manifest.json")
                : null);
    }
}
