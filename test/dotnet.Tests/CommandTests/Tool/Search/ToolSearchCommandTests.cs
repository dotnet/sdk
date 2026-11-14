// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Search;
using Microsoft.DotNet.Cli.NugetSearch;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Configuration;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace dotnet.Tests.ToolSearchTests;

[TestClass]
public class ToolSearchCommandTests : SdkTest
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteOnlyPrintsPackagesFromMappedOrigins(bool detail)
    {
        await using var server = new ToolSearchTestServer();
        string configPath = Path.Combine(TestAssetsManager.CreateTestDirectory().Path, "NuGet.config");
        server.WriteConfig(configPath);

        (int exitCode, BufferedReporter output, BufferedReporter error) = await RunToolSearchAsync(
            ["dotnet", "tool", "search", "arbitrary text", "--configfile", configPath, .. detail ? new[] { "--detail" } : []],
            new NugetToolSearchApiRequest(), cancellationToken: TestContext.CancellationToken);

        exitCode.Should().Be(0);
        string text = string.Join(Environment.NewLine, output.Lines);
        text.Should().Contain("other.tool").And.Contain("contoso.tool")
            .And.NotContain("contoso.private.tool").And.NotContain("contoso.disabled.tool").And.NotContain("9.0.0");
        error.Lines.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task ExecutePropagatesMappingUnlessSourcesAreOverridden(bool explicitSource, bool additionalSource)
    {
        const string source = "https://source.example.test/v3/index.json";
        string configPath = WriteMappingConfig(source);
        var fake = new FakeNugetToolSearchApiRequest(
            successResponses: new Dictionary<string, IReadOnlyCollection<SearchResultPackage>> { [source] = [] });
        List<string> args = ["dotnet", "tool", "search", "unmapped-search-term", "--configfile", configPath];
        if (explicitSource)
        {
            args.AddRange(["--source", source]);
        }
        if (additionalSource)
        {
            args.AddRange(["--add-source", source]);
        }

        (int exitCode, _, _) = await RunToolSearchAsync(args.ToArray(), fake, cancellationToken: TestContext.CancellationToken);

        exitCode.Should().Be(0);
        PackageSourceMapping? mapping = fake.RequestedMappings.Should().ContainSingle().Subject;
        if (explicitSource)
        {
            mapping.Should().BeNull();
        }
        else
        {
            mapping.Should().NotBeNull();
            mapping!.GetConfiguredPackageSources("Contoso.Tool").Should().Equal("corp");
        }
    }

    [TestMethod]
    public async Task ExecuteRejectsAdditiveOnlySourceWithMappingBeforeQuerying()
    {
        const string source = "https://source.example.test/v3/index.json";
        string configPath = WriteMappingConfig(source);
        var fake = new FakeNugetToolSearchApiRequest();

        Func<Task> execute = () => RunToolSearchAsync(
            ["dotnet", "tool", "search", "tool", "--configfile", configPath, "--add-source", source],
            fake, cancellationToken: TestContext.CancellationToken);

        await execute.Should().ThrowAsync<GracefulException>()
            .WithMessage(Microsoft.DotNet.Cli.CliStrings.CannotUseAddSourceWithSourceMapping);
        fake.RequestedSourceUrls.Should().BeEmpty();
    }

    private string WriteMappingConfig(string source)
    {
        string path = Path.Combine(TestAssetsManager.CreateTestDirectory().Path, "NuGet.config");
        File.WriteAllText(path, $$"""
            <configuration>
              <packageSources>
                <clear />
                <add key="corp" value="{{source}}" />
              </packageSources>
              <packageSourceMapping>
                <clear />
                <packageSource key="corp"><package pattern="Contoso.*" /></packageSource>
              </packageSourceMapping>
            </configuration>
            """);
        return path;
    }

    [TestMethod]
    public async Task ExecuteReturnsOneWhenNoSourcesAreConfiguredOrEnabled()
    {
        string configPath = Path.Combine(TestAssetsManager.CreateTestDirectory().Path, "NuGet.config");
        File.WriteAllText(
            configPath,
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
              </packageSources>
            </configuration>
            """);

        (int exitCode, _, BufferedReporter error) = await RunToolSearchAsync(
            ["dotnet", "tool", "search", "mytool", "--configfile", configPath],
            new FakeNugetToolSearchApiRequest(),
            cancellationToken: TestContext.CancellationToken);

        exitCode.Should().Be(1);
        error.Lines.Should().Contain(l => l.Contains("No NuGet package sources are configured or enabled."));
    }

    [TestMethod]
    public async Task ExecuteRejectsInsecureHttpSourceBeforeQuerying()
    {
        string configPath = Path.Combine(TestAssetsManager.CreateTestDirectory().Path, "NuGet.config");
        File.WriteAllText(
            configPath,
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <add key="insecure" value="http://example.test/v3/index.json" allowInsecureConnections="false" />
              </packageSources>
            </configuration>
            """);
        var fake = new FakeNugetToolSearchApiRequest();

        Func<Task> execute = async () => await RunToolSearchAsync(
            ["dotnet", "tool", "search", "mytool", "--configfile", configPath],
            fake,
            cancellationToken: TestContext.CancellationToken);

        await execute.Should().ThrowAsync<GracefulException>()
            .WithMessage("*allowInsecureConnections*");
        fake.RequestedSourceUrls.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecutePassesTheSearchParametersAndCancellationTokenToEachSource()
    {
        const string source1 = "https://source1.example.test/v3/index.json";
        const string source2 = "https://source2.example.test/v3/index.json";

        var fake = new FakeNugetToolSearchApiRequest(successResponses: new Dictionary<string, IReadOnlyCollection<SearchResultPackage>>
        {
            [source1] = [],
            [source2] = [],
        });
        using CancellationTokenSource cancellation = new();

        (int exitCode, _, _) = await RunToolSearchAsync(
            [
                "dotnet", "tool", "search", "mytool",
                "--source", source1,
                "--source", source2,
                "--skip", "3",
                "--take", "4",
                "--prerelease"
            ],
            fake,
            cancellationToken: cancellation.Token);

        exitCode.Should().Be(0);
        fake.RequestedSourceUrls.Should().Equal(source1, source2);
        foreach (NugetSearchApiParameter requestedParameter in fake.RequestedParameters)
        {
            requestedParameter.SearchTerm.Should().Be("mytool");
            requestedParameter.Skip.Should().Be(3);
            requestedParameter.Take.Should().Be(4);
            requestedParameter.Prerelease.Should().BeTrue();
        }
        fake.RequestedCancellationTokens.Should().OnlyContain(token => token == cancellation.Token);
    }

    [TestMethod]
    public async Task ExecuteReturnsZeroAndPrintsOnlySuccessfulSourcesWhenSomeSourcesFail()
    {
        const string goodSource = "https://good.example.test/v3/index.json";
        const string badSource = "https://bad.example.test/v3/index.json";

        var fake = new FakeNugetToolSearchApiRequest(
            successResponses: new Dictionary<string, IReadOnlyCollection<SearchResultPackage>>
            {
                [goodSource] = [CreateSearchResultPackage("sample.tool")],
            },
            failureMessages: new Dictionary<string, string>
            {
                [badSource] = "the feed did not respond",
            });

        (int exitCode, BufferedReporter output, BufferedReporter error) = await RunToolSearchAsync(
            ["dotnet", "tool", "search", "mytool", "--source", goodSource, "--source", badSource],
            fake,
            cancellationToken: TestContext.CancellationToken);

        exitCode.Should().Be(0);

        output.Lines.Should().Contain(l => l.Contains(goodSource));
        output.Lines.Should().Contain(l => l.Contains("sample.tool"));
        output.Lines.Should().NotContain(l => l.Contains(badSource));

        error.Lines.Should().Contain(l => l.Contains(badSource));
        error.Lines.Should().Contain(l => l.Contains("the feed did not respond"));
        error.Lines.Should().NotContain(l => l.Contains(goodSource));
    }

    [TestMethod]
    public async Task ExecuteReturnsOneAndPrintsFailuresForAllSourcesWhenEverySourceFails()
    {
        const string source1 = "https://source1.example.test/v3/index.json";
        const string source2 = "https://source2.example.test/v3/index.json";

        var fake = new FakeNugetToolSearchApiRequest(failureMessages: new Dictionary<string, string>
        {
            [source1] = "boom1",
            [source2] = "boom2",
        });

        (int exitCode, BufferedReporter output, BufferedReporter error) = await RunToolSearchAsync(
            ["dotnet", "tool", "search", "mytool", "--source", source1, "--source", source2],
            fake,
            cancellationToken: TestContext.CancellationToken);

        exitCode.Should().Be(1);
        output.Lines.Should().BeEmpty();
        error.Lines.Should().Contain(l => l.Contains(source1));
        error.Lines.Should().Contain(l => l.Contains("boom1"));
        error.Lines.Should().Contain(l => l.Contains(source2));
        error.Lines.Should().Contain(l => l.Contains("boom2"));
    }

    [TestMethod]
    public async Task ExecuteQueriesBothSourceAndAddSourceTogether()
    {
        const string exclusiveSource = "https://exclusive.example.test/v3/index.json";
        const string additionalSource = "https://additional.example.test/v3/index.json";

        var fake = new FakeNugetToolSearchApiRequest(successResponses: new Dictionary<string, IReadOnlyCollection<SearchResultPackage>>
        {
            [exclusiveSource] = [],
            [additionalSource] = [],
        });

        (int exitCode, _, _) = await RunToolSearchAsync(
            [
                "dotnet", "tool", "search", "mytool",
                "--source", exclusiveSource,
                "--add-source", additionalSource
            ],
            fake,
            cancellationToken: TestContext.CancellationToken);

        exitCode.Should().Be(0);
        fake.RequestedSourceUrls.Should().Equal(exclusiveSource, additionalSource);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteInitializesCredentialsBeforeQueryingSources(bool interactive)
    {
        const string source = "https://source.example.test/v3/index.json";
        bool? credentialsInteractive = null;
        var fake = new FakeNugetToolSearchApiRequest(
            successResponses: new Dictionary<string, IReadOnlyCollection<SearchResultPackage>>
            {
                [source] = [],
            },
            beforeRequest: () => credentialsInteractive.Should().Be(interactive));
        List<string> args = ["dotnet", "tool", "search", "mytool", "--source", source];
        if (interactive)
        {
            args.Add("--interactive");
        }

        (int exitCode, _, _) = await RunToolSearchAsync(
            args.ToArray(),
            fake,
            setupCredentialService: value => credentialsInteractive = value,
            cancellationToken: TestContext.CancellationToken);

        exitCode.Should().Be(0);
    }

    [TestMethod]
    public async Task ExecuteBoundsConcurrencyAndPrintsResultsInSourceOrder()
    {
        string[] sources = Enumerable.Range(1, 6)
            .Select(index => $"https://source{index}.example.test/v3/index.json")
            .ToArray();
        var fake = new BlockingNugetToolSearchApiRequest(expectedFirstBatchSize: 4);
        Task<(int ExitCode, BufferedReporter Output, BufferedReporter Error)> execution = RunToolSearchAsync(
            ["dotnet", "tool", "search", "mytool", .. sources.SelectMany(source => new[] { "--source", source })],
            fake,
            cancellationToken: TestContext.CancellationToken);

        try
        {
            await fake.FirstBatchStarted.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
            await Task.Delay(100, TestContext.CancellationToken);
            fake.StartedRequestCount.Should().Be(4);
        }
        finally
        {
            fake.ReleaseRequests();
        }

        (int exitCode, BufferedReporter output, _) = await execution;
        exitCode.Should().Be(0);
        fake.MaximumConcurrentRequests.Should().Be(4);

        List<string> outputLines = output.Lines.ToList();
        int previousHeadingIndex = -1;
        foreach (string source in sources)
        {
            int headingIndex = outputLines.FindIndex(line => line.Contains(source, StringComparison.Ordinal));
            headingIndex.Should().BeGreaterThan(previousHeadingIndex);
            previousHeadingIndex = headingIndex;
        }
    }

    private static async Task<(int ExitCode, BufferedReporter Output, BufferedReporter Error)> RunToolSearchAsync(
        string[] args,
        INugetToolSearchApiRequest nugetToolSearchApiRequest,
        string? currentWorkingDirectory = null,
        Action<bool>? setupCredentialService = null,
        CancellationToken cancellationToken = default)
    {
        BufferedReporter capturedOutput = new();
        BufferedReporter capturedError = new();

        Reporter.SetOutput(capturedOutput);
        Reporter.SetError(capturedError);
        try
        {
            ParseResult parseResult = Parser.Parse(args);
            var command = new ToolSearchCommand(
                parseResult,
                nugetToolSearchApiRequest,
                currentWorkingDirectory,
                setupCredentialService ?? (_ => { }));
            int exitCode = await command.ExecuteAsync(cancellationToken);
            return (exitCode, capturedOutput, capturedError);
        }
        finally
        {
            Reporter.SetOutput(Reporter.ConsoleOutReporter);
            Reporter.SetError(Reporter.ConsoleErrReporter);
        }
    }

    private static SearchResultPackage CreateSearchResultPackage(string id) =>
        new(
            new PackageId(id),
            "1.0.0",
            "desc",
            "sum",
            [],
            ["author"],
            1,
            false,
            [new SearchResultPackageVersion("1.0.0", 1)]);

    private sealed class FakeNugetToolSearchApiRequest(
        IReadOnlyDictionary<string, IReadOnlyCollection<SearchResultPackage>>? successResponses = null,
        IReadOnlyDictionary<string, string>? failureMessages = null,
        Action? beforeRequest = null) : INugetToolSearchApiRequest
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyCollection<SearchResultPackage>> _successResponses =
            successResponses ?? new Dictionary<string, IReadOnlyCollection<SearchResultPackage>>();
        private readonly IReadOnlyDictionary<string, string> _failureMessages = failureMessages ?? new Dictionary<string, string>();

        public List<string> RequestedSourceUrls { get; } = [];

        public List<NugetSearchApiParameter> RequestedParameters { get; } = [];

        public List<CancellationToken> RequestedCancellationTokens { get; } = [];

        public List<PackageSourceMapping?> RequestedMappings { get; } = [];

        public Task<IReadOnlyCollection<SearchResultPackage>> GetResult(
            NugetSearchApiParameter nugetSearchApiParameter,
            PackageSource source,
            CancellationToken cancellationToken,
            PackageSourceMapping? sourceMapping = null)
        {
            beforeRequest?.Invoke();
            string sourceUrl = source.Source;
            RequestedSourceUrls.Add(sourceUrl);
            RequestedParameters.Add(nugetSearchApiParameter);
            RequestedCancellationTokens.Add(cancellationToken);
            RequestedMappings.Add(sourceMapping);

            if (_failureMessages.TryGetValue(sourceUrl, out string? failureMessage))
            {
                throw new NugetSearchApiRequestException(failureMessage);
            }

            if (_successResponses.TryGetValue(sourceUrl, out IReadOnlyCollection<SearchResultPackage>? packages))
            {
                return Task.FromResult(packages);
            }

            throw new InvalidOperationException($"Test setup error: no response configured for source '{sourceUrl}'.");
        }
    }

    private sealed class BlockingNugetToolSearchApiRequest(int expectedFirstBatchSize) : INugetToolSearchApiRequest
    {
        private readonly TaskCompletionSource<bool> _firstBatchStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseRequests = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activeRequests;
        private int _maximumConcurrentRequests;
        private int _startedRequestCount;

        public Task FirstBatchStarted => _firstBatchStarted.Task;

        public int MaximumConcurrentRequests => Volatile.Read(ref _maximumConcurrentRequests);

        public int StartedRequestCount => Volatile.Read(ref _startedRequestCount);

        public async Task<IReadOnlyCollection<SearchResultPackage>> GetResult(
            NugetSearchApiParameter nugetSearchApiParameter,
            PackageSource source,
            CancellationToken cancellationToken,
            PackageSourceMapping? sourceMapping = null)
        {
            int activeRequests = Interlocked.Increment(ref _activeRequests);
            int observedMaximum = Volatile.Read(ref _maximumConcurrentRequests);
            while (activeRequests > observedMaximum)
            {
                int previousMaximum = Interlocked.CompareExchange(
                    ref _maximumConcurrentRequests,
                    activeRequests,
                    observedMaximum);
                if (previousMaximum == observedMaximum)
                {
                    break;
                }
                observedMaximum = previousMaximum;
            }

            if (Interlocked.Increment(ref _startedRequestCount) == expectedFirstBatchSize)
            {
                _firstBatchStarted.SetResult(true);
            }

            try
            {
                await _releaseRequests.Task.WaitAsync(cancellationToken);
                return [CreateSearchResultPackage(new Uri(source.Source).Host)];
            }
            finally
            {
                Interlocked.Decrement(ref _activeRequests);
            }
        }

        public void ReleaseRequests() => _releaseRequests.SetResult(true);
    }

}
