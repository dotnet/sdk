// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.DotNet.Cli.InternalMicrosoft;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Configurer;

namespace Microsoft.DotNet.Tests.TelemetryTests;

[TestClass]
public class InternalMicrosoftDetectorTests : SdkTest
{
    private readonly string _testDirectory;
    private readonly ManualTimeProvider _timeProvider = new(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));

    public InternalMicrosoftDetectorTests()
    {
        _testDirectory = TestAssetsManager.CreateTestDirectory().Path;
    }

    [TestMethod]
    public async Task PositiveResultIsCachedForSixHours()
    {
        var calls = 0;
        var stages = Stages(new InternalMicrosoftProbe("test", _ =>
        {
            calls++;
            return Task.FromResult(new InternalMicrosoftProbeResult(true, "Alias", "redmond"));
        }));

        var first = await CreateDetector(stages).IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);
        var second = await CreateDetector(stages).IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);

        first.IsInternalMicrosoft.Should().BeTrue();
        first.CacheStatus.Should().Be(InternalMicrosoftDetectorCacheStatus.Miss);
        second.IsInternalMicrosoft.Should().BeTrue();
        second.Alias.Should().Be("alias");
        second.Domain.Should().Be("REDMOND");
        second.CacheStatus.Should().Be(InternalMicrosoftDetectorCacheStatus.Hit);
        calls.Should().Be(1);
    }

    [TestMethod]
    public async Task CleanNegativeResultIsCachedForSixHours()
    {
        var calls = 0;
        var stages = Stages(new InternalMicrosoftProbe("test", _ =>
        {
            calls++;
            return Task.FromResult(InternalMicrosoftProbeResult.NotDetected);
        }));

        var first = await CreateDetector(stages).IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);
        var second = await CreateDetector(stages).IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);

        first.Outcome.Should().Be(InternalMicrosoftDetectorOutcome.NotDetected);
        second.CacheStatus.Should().Be(InternalMicrosoftDetectorCacheStatus.Hit);
        calls.Should().Be(1);
    }

    [TestMethod]
    public async Task CiCacheDoesNotPersistAliasOrDomain()
    {
        var calls = 0;
        var stages = Stages(new InternalMicrosoftProbe("test", _ =>
        {
            calls++;
            return Task.FromResult(new InternalMicrosoftProbeResult(true, "Alias", "redmond"));
        }));

        var first = await CreateDetector(stages, isCI: true).IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);
        var second = await CreateDetector(stages, isCI: true).IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);

        first.Alias.Should().Be("alias");
        first.Domain.Should().Be("REDMOND");
        second.Alias.Should().BeNull();
        second.Domain.Should().BeNull();
        second.CacheStatus.Should().Be(InternalMicrosoftDetectorCacheStatus.Hit);
        calls.Should().Be(1);
    }

    [TestMethod]
    public async Task FailedResultIsNotCached()
    {
        var calls = 0;
        var stages = Stages(new InternalMicrosoftProbe("test", _ =>
        {
            calls++;
            return Task.FromResult(InternalMicrosoftProbeResult.Failed(new("failure", "test")));
        }));

        var first = await CreateDetector(stages).IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);
        var second = await CreateDetector(stages).IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);

        first.Outcome.Should().Be(InternalMicrosoftDetectorOutcome.Failed);
        second.Outcome.Should().Be(InternalMicrosoftDetectorOutcome.Failed);
        calls.Should().Be(2);
    }

    [TestMethod]
    public async Task StaleResultRunsProbesAgain()
    {
        var calls = 0;
        var stages = Stages(new InternalMicrosoftProbe("test", _ =>
        {
            calls++;
            return Task.FromResult(InternalMicrosoftProbeResult.NotDetected);
        }));

        await CreateDetector(stages).IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromHours(7));
        var result = await CreateDetector(stages).IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);

        result.CacheStatus.Should().Be(InternalMicrosoftDetectorCacheStatus.Stale);
        calls.Should().Be(2);
    }

    [TestMethod]
    public async Task CacheFromDifferentCiModeIsStale()
    {
        var calls = 0;
        var stages = Stages(new InternalMicrosoftProbe("test", _ =>
        {
            calls++;
            return Task.FromResult(InternalMicrosoftProbeResult.NotDetected);
        }));

        await CreateDetector(stages).IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);
        var result = await CreateDetector(stages, isCI: true).IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);

        result.CacheStatus.Should().Be(InternalMicrosoftDetectorCacheStatus.Stale);
        calls.Should().Be(2);
    }

    [TestMethod]
    public async Task StagePrefersResultWithRicherIdentity()
    {
        var stages = Stages(
            new InternalMicrosoftProbe("first", _ => Task.FromResult(new InternalMicrosoftProbeResult(true, "first", null))),
            new InternalMicrosoftProbe("second", _ => Task.FromResult(new InternalMicrosoftProbeResult(true, "second", "domain"))));

        var result = await CreateDetector(stages).IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);

        result.Source.Should().Be("second");
        result.Alias.Should().Be("second");
        result.Domain.Should().Be("DOMAIN");
    }

    [TestMethod]
    public async Task PositiveResultAfterStageDeadlineIsRejected()
    {
        var stages = Stages(
            new InternalMicrosoftProbe("late", async _ =>
            {
                await Task.Delay(100, TestContext.CancellationToken);
                return new InternalMicrosoftProbeResult(true, "late", "domain");
            }));
        var detector = CreateDetector(stages, stageTimeout: TimeSpan.FromMilliseconds(10));

        var result = await detector.IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);

        result.IsInternalMicrosoft.Should().BeFalse();
        result.Outcome.Should().Be(InternalMicrosoftDetectorOutcome.TimedOut);
        result.ProbeDiagnostics.Should().ContainSingle()
            .Which.Outcome.Should().Be(InternalMicrosoftProbeOutcome.TimedOut);
    }

    [TestMethod]
    public async Task PositiveResultAfterEarlyStageTimeoutSignalIsRejected()
    {
        var stages = Stages(
            new InternalMicrosoftProbe(
                "late",
                _ => Task.FromResult(new InternalMicrosoftProbeResult(true, "late", "domain"))));
        var detector = CreateDetector(
            stages,
            stageTimeout: TimeSpan.FromSeconds(1),
            createStageTimeoutSource: _ =>
            {
                var source = new CancellationTokenSource();
                source.Cancel();
                return source;
            });

        var result = await detector.IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);

        result.IsInternalMicrosoft.Should().BeFalse();
        result.Outcome.Should().Be(InternalMicrosoftDetectorOutcome.TimedOut);
        result.ProbeDiagnostics.Should().ContainSingle()
            .Which.Outcome.Should().Be(InternalMicrosoftProbeOutcome.TimedOut);
    }

    [TestMethod]
    public async Task EnvironmentGitHubTokenDetectsActiveMicrosoftMembership()
    {
        var handler = new GitHubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/user" => Json(HttpStatusCode.OK, """{"login":"octocat"}"""),
            "/user/memberships/orgs/microsoft" => Json(HttpStatusCode.OK, """{"state":"active"}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        var detector = CreateDefaultDetector(
            handler,
            name => name == "GH_TOKEN" ? "ghp_abcdefghijklmnopqrstuvwxyz012345" : null);

        var result = await detector.IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);

        result.IsInternalMicrosoft.Should().BeTrue();
        result.Source.Should().Be("Environment GitHub token membership");
        result.Alias.Should().BeNull();
        handler.Requests.Should().HaveCount(2);
        handler.Requests.Should().OnlyContain(request => request.AuthorizationScheme == "Bearer");
        result.ProbeDiagnostics.SelectMany(ToDiagnosticStrings).Should()
            .NotContain(value => value.Contains("ghp_", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task EnvironmentGitHubTokensCheckAllCandidatesConcurrently()
    {
        const string InvalidToken = "ghp_invalidtokeninvalidtoken12345";
        const string ValidToken = "ghp_validtokenvalidtokenvalid12345";
        var handler = new GitHubHandler(request =>
        {
            var isValidToken = request.Headers.Authorization?.Parameter == ValidToken;
            return request.RequestUri!.AbsolutePath switch
            {
                "/user" when isValidToken => Json(HttpStatusCode.OK, """{"login":"octocat"}"""),
                "/user" => Json(HttpStatusCode.Unauthorized, "{}"),
                "/user/memberships/orgs/microsoft" => Json(HttpStatusCode.OK, """{"state":"active"}"""),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        });
        var detector = CreateDefaultDetector(
            handler,
            name => name switch
            {
                "GH_TOKEN" => InvalidToken,
                "GITHUB_TOKEN" => ValidToken,
                _ => null
            });

        var result = await detector.IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);

        result.IsInternalMicrosoft.Should().BeTrue();
        handler.Requests.Should().Contain(request => request.AuthorizationParameter == InvalidToken);
        handler.Requests.Should().Contain(request => request.AuthorizationParameter == ValidToken);
    }

    [TestMethod]
    public async Task GhCliAuthenticationRequiredExitCodeIsCleanNegative()
    {
        var detector = CreateDefaultDetector(
            new GitHubHandler(_ => throw new InvalidOperationException("HTTP must not be used.")),
            _ => null,
            runProcess: (_, _, _) => Task.FromResult(new InternalMicrosoftProcessResult("", "", 4, null)),
            commandExists: command => command == "gh");

        var result = await detector.IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);

        result.Outcome.Should().Be(InternalMicrosoftDetectorOutcome.NotDetected);
        result.ProbeDiagnostics.Should().Contain(diagnostic =>
            diagnostic.Source == "gh CLI GitHub org membership" &&
            diagnostic.Outcome == InternalMicrosoftProbeOutcome.NotDetected);
    }

    [TestMethod]
    public async Task MalformedCopilotConfigCanSupplyGitHubToken()
    {
        const string Token = "ghp_copilottokencopilottoken12345";
        var copilotDirectory = Path.Combine(_testDirectory, ".copilot");
        Directory.CreateDirectory(copilotDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(copilotDirectory, "config.json"),
            $$"""{"githubToken":"{{Token}}", this is malformed""",
            TestContext.CancellationToken);
        var handler = new GitHubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/user" => Json(HttpStatusCode.OK, """{"login":"octocat"}"""),
            "/user/memberships/orgs/microsoft" => Json(HttpStatusCode.OK, """{"state":"active"}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        var detector = CreateDefaultDetector(handler, _ => null);

        var result = await detector.IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);

        result.IsInternalMicrosoft.Should().BeTrue();
        result.Source.Should().Be("Copilot CLI GitHub org membership");
        handler.Requests.Should().Contain(request => request.AuthorizationParameter == Token);
    }

    [TestMethod]
    public async Task CiSkipsGitHubIdentityProbes()
    {
        var handler = new GitHubHandler(_ => throw new InvalidOperationException("HTTP must not be used in CI."));
        var detector = CreateDefaultDetector(
            handler,
            name => name == "GH_TOKEN" ? "ghp_abcdefghijklmnopqrstuvwxyz012345" : null,
            isCI: true);

        var result = await detector.IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);

        result.IsInternalMicrosoft.Should().BeFalse();
        result.ProbeDiagnostics.Should().BeEmpty();
        handler.Requests.Should().BeEmpty();
    }

    [TestMethod]
    public void WslKernelReleaseIsDetectedWithoutEnvironmentMarkers()
    {
        InternalMicrosoftDetectionUtilities.IsWsl(
            distroName: null,
            interop: null,
            kernelRelease: "6.6.87.2-microsoft-standard-WSL2").Should().BeTrue();
    }

    [TestMethod]
    public async Task MissingPlatformCommandsAreCleanNegative()
    {
        var wslDetector = new InternalMicrosoftDetector(
            Path.Combine(_testDirectory, "wsl-detector.json"),
            _timeProvider,
            CreateTestContext(
                isCI: false,
                DefaultContextOptions with
                {
                    IsLinux = true,
                    IsWsl = true
                }),
            InternalMicrosoftDetectorOptions.Default);
        var macDetector = new InternalMicrosoftDetector(
            Path.Combine(_testDirectory, "mac-detector.json"),
            _timeProvider,
            CreateTestContext(
                isCI: false,
                DefaultContextOptions with
                {
                    IsMacOS = true
                }),
            InternalMicrosoftDetectorOptions.Default);

        var wslResult = await wslDetector.IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);
        var macResult = await macDetector.IsInternalMicrosoftMachineAsync(TestContext.CancellationToken);

        wslResult.Outcome.Should().Be(InternalMicrosoftDetectorOutcome.NotDetected);
        macResult.Outcome.Should().Be(InternalMicrosoftDetectorOutcome.NotDetected);
    }

    [TestMethod]
    public void WindowsWorkplaceJoinParsesMicrosoftIdentity()
    {
        var result = WindowsWorkplaceJoinDetectionProvider.Parse(
            """
            AzureAdJoined : YES
            TenantId : 72f988bf-86f1-41af-91ab-2d7cd011db47
            User Email : Alias@Microsoft.com
            DomainName : redmond.corp.microsoft.com
            """);

        result.Should().Be(new InternalMicrosoftProbeResult(true, "alias", "REDMOND"));
    }

    [TestMethod]
    public void VisualStudioAccountStoreUsesPersonalizationIdentity()
    {
        using var document = JsonDocument.Parse(
            $$"""
            [
              {
                "Stale": false,
                "IsPersonalizationAccount": false,
                "Properties": {
                  "IdentityProvider": "72f988bf-86f1-41af-91ab-2d7cd011db47",
                  "HomeTenant": "72f988bf-86f1-41af-91ab-2d7cd011db47",
                  "IdTokenPayload": "{\"tid\":\"72f988bf-86f1-41af-91ab-2d7cd011db47\",\"iss\":\"https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/v2.0\",\"preferred_username\":\"fallback@microsoft.com\"}"
                }
              },
              {
                "Stale": false,
                "IsPersonalizationAccount": true,
                "Properties": {
                  "IdentityProvider": "72f988bf-86f1-41af-91ab-2d7cd011db47",
                  "HomeTenant": "72f988bf-86f1-41af-91ab-2d7cd011db47",
                  "IdTokenPayload": "{\"tid\":\"72f988bf-86f1-41af-91ab-2d7cd011db47\",\"iss\":\"https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/v2.0\",\"preferred_username\":\"preferred@microsoft.com\"}"
                }
              }
            ]
            """);

        var result = VisualStudioAccountDetectionParser.Parse(document.RootElement);

        result.IsInternalMicrosoft.Should().BeTrue();
        result.Alias.Should().Be("preferred");
    }

    [TestMethod]
    public void MacPlatformSsoValidatesTenantEndpointsAndIdentity()
    {
        var result = MacPlatformSsoDetectionProvider.Parse(
            """
            Device Configuration:
            { "registrationCompleted": true }
            Login Configuration:
            {
              "issuer": "https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/v2.0",
              "keyEndpointURL": "https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/getkeydata",
              "tokenEndpointURL": "https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/oauth2/v2.0/token"
            }
            User Configuration:
            {
              "kerberosStatus": [
                { "realm": "redmond.corp.microsoft.com", "upn": "Alias@redmond.corp.microsoft.com" }
              ]
            }
            """);

        result.Should().Be(new InternalMicrosoftProbeResult(true, "alias", "REDMOND"));
    }

    [TestMethod]
    public void TelemetryProcessorEnrichesCompletedActivities()
    {
        var telemetry = new InternalMicrosoftTelemetry();
        telemetry.Start(new TestDetector(new(
            true,
            "test",
            "alias",
            "DOMAIN",
            false,
            InternalMicrosoftDetectorOutcome.Detected,
            InternalMicrosoftDetectorCacheStatus.Miss,
            TimeSpan.FromMilliseconds(1),
            [])), TestContext.CancellationToken);
        telemetry.WaitForCompletion();
        var activity = new Activity("command").Start();

        new InternalMicrosoftTelemetryProcessor(telemetry).OnEnd(activity);

        activity.GetTagItem(InternalMicrosoftTelemetryConstants.IsInternal).Should().Be(true);
        activity.GetTagItem(InternalMicrosoftTelemetryConstants.Source).Should().Be("test");
        activity.GetTagItem(InternalMicrosoftTelemetryConstants.Alias).Should().Be("alias");
        activity.GetTagItem(InternalMicrosoftTelemetryConstants.Domain).Should().Be("DOMAIN");
    }

    [TestMethod]
    public void TelemetryProcessorSuppressesIdentityInCi()
    {
        var telemetry = new InternalMicrosoftTelemetry();
        telemetry.Start(new TestDetector(new(
            true,
            "test",
            "alias",
            "DOMAIN",
            true,
            InternalMicrosoftDetectorOutcome.Detected,
            InternalMicrosoftDetectorCacheStatus.Miss,
            TimeSpan.FromMilliseconds(1),
            [])), TestContext.CancellationToken);
        telemetry.WaitForCompletion();
        var activity = new Activity("command").Start();

        new InternalMicrosoftTelemetryProcessor(telemetry).OnEnd(activity);

        activity.GetTagItem(InternalMicrosoftTelemetryConstants.IsInternal).Should().Be(true);
        activity.GetTagItem(InternalMicrosoftTelemetryConstants.Alias).Should().BeNull();
        activity.GetTagItem(InternalMicrosoftTelemetryConstants.Domain).Should().BeNull();
    }

    [TestMethod]
    public void TelemetryTagsAreComputedOnce()
    {
        var telemetry = new InternalMicrosoftTelemetry();
        telemetry.Start(new TestDetector(new(
            true,
            "test",
            "alias",
            "DOMAIN",
            false,
            InternalMicrosoftDetectorOutcome.Detected,
            InternalMicrosoftDetectorCacheStatus.Miss,
            TimeSpan.FromMilliseconds(1),
            [])), TestContext.CancellationToken);
        telemetry.WaitForCompletion();

        telemetry.GetResolvedTags().Should().BeSameAs(telemetry.GetResolvedTags());
    }

    [TestMethod]
    public void TelemetryPropagatesCancellationToken()
    {
        using var cancellationSource = new CancellationTokenSource();
        var detector = new TestDetector(new(
            false,
            null,
            null,
            null,
            false,
            InternalMicrosoftDetectorOutcome.NotDetected,
            InternalMicrosoftDetectorCacheStatus.Miss,
            TimeSpan.FromMilliseconds(1),
            []));
        var telemetry = new InternalMicrosoftTelemetry();

        telemetry.Start(detector, cancellationSource.Token);
        telemetry.WaitForCompletion();

        detector.ObservedCancellationToken.Should().Be(cancellationSource.Token);
    }

    private InternalMicrosoftDetector CreateDetector(
        IReadOnlyList<IReadOnlyList<InternalMicrosoftProbe>> stages,
        bool isCI = false,
        TimeSpan? stageTimeout = null,
        Func<TimeSpan, CancellationTokenSource>? createStageTimeoutSource = null)
    {
        var defaultOptions = InternalMicrosoftDetectorOptions.Default;
        return new(
            Path.Combine(_testDirectory, "detector.json"),
            _timeProvider,
            CreateTestContext(isCI, DefaultContextOptions),
            defaultOptions with
            {
                CreateProbeStages = _ => stages,
                ProbeStageTimeout = stageTimeout ?? defaultOptions.ProbeStageTimeout,
                CreateProbeStageTimeoutSource =
                    createStageTimeoutSource ?? defaultOptions.CreateProbeStageTimeoutSource
            });
    }

    private InternalMicrosoftDetector CreateDefaultDetector(
        HttpMessageHandler handler,
        Func<string, string?> getEnvironmentVariable,
        bool isCI = false,
        Func<string, IReadOnlyList<string>, CancellationToken, Task<InternalMicrosoftProcessResult>>? runProcess = null,
        Func<string, bool>? commandExists = null) =>
        new(
            Path.Combine(_testDirectory, "detector.json"),
            _timeProvider,
            CreateTestContext(
                isCI,
                DefaultContextOptions with
                {
                    GitHubHttpMessageHandler = handler,
                    RunProcess = runProcess ?? DefaultContextOptions.RunProcess,
                    GetEnvironmentVariable = getEnvironmentVariable,
                    CommandExists = commandExists ?? DefaultContextOptions.CommandExists
                }),
            InternalMicrosoftDetectorOptions.Default);

    private InternalMicrosoftDetectionContext CreateTestContext(
        bool isCI,
        InternalMicrosoftDetectionContextOptions options) =>
        new(_testDirectory, isCI, options);

    private static InternalMicrosoftDetectionContextOptions DefaultContextOptions { get; } = new(
        GitHubHttpMessageHandler: null,
        RunProcess: static (_, _, _) => throw new InvalidOperationException("The test did not configure process execution."),
        GetEnvironmentVariable: static _ => null,
        GetEnvironmentVariables: static () => [],
        CommandExists: static _ => false,
        IsWindows: false,
        IsMacOS: false,
        IsLinux: false,
        IsWsl: false,
        MacPlatformSsoPath: "/usr/bin/app-sso",
        GitHubUserAgentVersion: "1.0.0",
        GitHubHttpTimeout: TimeSpan.FromSeconds(3),
        GitHubCandidateTimeout: TimeSpan.FromSeconds(5));

    private static IReadOnlyList<IReadOnlyList<InternalMicrosoftProbe>> Stages(params InternalMicrosoftProbe[] probes) =>
        [probes];

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string content) =>
        new(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

    private static IEnumerable<string> ToDiagnosticStrings(InternalMicrosoftProbeDiagnostic diagnostic)
    {
        yield return diagnostic.Source;
        yield return diagnostic.Outcome;
        if (diagnostic.Failure is { } failure)
        {
            yield return failure.Code;
            yield return failure.Stage;
            yield return failure.ExceptionType ?? string.Empty;
        }
    }

    private sealed class TestDetector(InternalMicrosoftDetectionResult result) : IInternalMicrosoftDetector
    {
        public CancellationToken ObservedCancellationToken { get; private set; }

        public Task<InternalMicrosoftDetectionResult> IsInternalMicrosoftMachineAsync(
            CancellationToken cancellationToken = default)
        {
            ObservedCancellationToken = cancellationToken;
            return Task.FromResult(result);
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;

        public void Advance(TimeSpan duration) => UtcNow += duration;
    }

    private sealed class GitHubHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        public List<(string? AuthorizationScheme, string? AuthorizationParameter, string Path)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            lock (Requests)
            {
                Requests.Add((
                    request.Headers.Authorization?.Scheme,
                    request.Headers.Authorization?.Parameter,
                    request.RequestUri!.AbsolutePath));
            }

            return Task.FromResult(handle(request));
        }
    }
}
