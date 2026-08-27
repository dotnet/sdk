// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Search;
using Microsoft.DotNet.Cli.NugetSearch;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace dotnet.Tests.ToolSearchTests;

[TestClass]
public class ToolSearchCommandTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExecuteReturnsOneWhenNoSourcesAreConfiguredOrEnabled()
    {
        using TemporaryDirectory temp = new();
        string configPath = temp.WriteNuGetConfigWithNoSources();

        (int exitCode, _, BufferedReporter error) = await RunToolSearchAsync(
            ["dotnet", "tool", "search", "mytool", "--configfile", configPath],
            new FakeNugetToolSearchApiRequest(),
            cancellationToken: TestContext.CancellationToken);

        exitCode.Should().Be(1);
        error.Lines.Should().Contain(l => l.Contains("No NuGet package sources are configured or enabled."));
    }

    [TestMethod]
    public async Task ExecuteQueriesEverySelectedSourceInTheOrderSpecified()
    {
        const string source1 = "https://source1.example.test/v3/index.json";
        const string source2 = "https://source2.example.test/v3/index.json";
        const string source3 = "https://source3.example.test/v3/index.json";

        var fake = new FakeNugetToolSearchApiRequest(successResponses: new Dictionary<string, IReadOnlyCollection<SearchResultPackage>>
        {
            [source1] = [],
            [source2] = [],
            [source3] = [],
        });

        (int exitCode, _, _) = await RunToolSearchAsync(
            ["dotnet", "tool", "search", "mytool", "--source", source1, "--source", source2, "--source", source3],
            fake,
            cancellationToken: TestContext.CancellationToken);

        exitCode.Should().Be(0);
        fake.RequestedSourceUrls.Should().Equal(source1, source2, source3);
    }

    [TestMethod]
    public async Task ExecutePassesTheSelectedSourceUrlAndTheSameSearchParametersToEachSource()
    {
        const string source1 = "https://source1.example.test/v3/index.json";
        const string source2 = "https://source2.example.test/v3/index.json";

        var fake = new FakeNugetToolSearchApiRequest(successResponses: new Dictionary<string, IReadOnlyCollection<SearchResultPackage>>
        {
            [source1] = [],
            [source2] = [],
        });

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
            cancellationToken: TestContext.CancellationToken);

        exitCode.Should().Be(0);
        fake.RequestedSourceUrls.Should().Equal(source1, source2);
        foreach (NugetSearchApiParameter requestedParameter in fake.RequestedParameters)
        {
            requestedParameter.SearchTerm.Should().Be("mytool");
            requestedParameter.Skip.Should().Be(3);
            requestedParameter.Take.Should().Be(4);
            requestedParameter.Prerelease.Should().BeTrue();
        }
    }

    [TestMethod]
    public async Task ExecutePassesCancellationTokenToEachSource()
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
            ["dotnet", "tool", "search", "mytool", "--source", source1, "--source", source2],
            fake,
            cancellationToken: cancellation.Token);

        exitCode.Should().Be(0);
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
    public async Task ExecuteContinuesWhenASourceDoesNotProvidePackageSearchResource()
    {
        const string goodSource = "https://good.example.test/v3/index.json";
        const string unsupportedSource = "https://unsupported.example.test/v3/index.json";
        var request = new NugetToolSearchApiRequest(
            (source, _) => Task.FromResult<PackageSearchResource?>(
                source.Source == unsupportedSource ? null : new EmptyPackageSearchResource()));

        (int exitCode, BufferedReporter output, BufferedReporter error) = await RunToolSearchAsync(
            ["dotnet", "tool", "search", "mytool", "--source", unsupportedSource, "--source", goodSource],
            request,
            cancellationToken: TestContext.CancellationToken);

        exitCode.Should().Be(0);
        output.Lines.Should().Contain(l => l.Contains(goodSource));
        error.Lines.Should().Contain(l => l.Contains(unsupportedSource));
        error.Lines.Should().Contain(l => l.Contains(nameof(PackageSearchResource)));
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
    public async Task ExecuteInitializesCredentialsBeforeQueryingSources()
    {
        const string source = "https://source.example.test/v3/index.json";
        bool credentialsInitialized = false;
        var fake = new FakeNugetToolSearchApiRequest(
            successResponses: new Dictionary<string, IReadOnlyCollection<SearchResultPackage>>
            {
                [source] = [],
            },
            beforeRequest: () => credentialsInitialized.Should().BeTrue());

        (int exitCode, _, _) = await RunToolSearchAsync(
            ["dotnet", "tool", "search", "mytool", "--source", source, "--interactive"],
            fake,
            setupCredentialService: interactive =>
            {
                interactive.Should().BeTrue();
                credentialsInitialized = true;
            },
            cancellationToken: TestContext.CancellationToken);

        exitCode.Should().Be(0);
    }

    [TestMethod]
    public async Task ExecuteUsesNonInteractiveCredentialsByDefault()
    {
        const string source = "https://source.example.test/v3/index.json";
        bool? interactiveValue = null;
        var fake = new FakeNugetToolSearchApiRequest(
            successResponses: new Dictionary<string, IReadOnlyCollection<SearchResultPackage>>
            {
                [source] = [],
            });

        (int exitCode, _, _) = await RunToolSearchAsync(
            ["dotnet", "tool", "search", "mytool", "--source", source],
            fake,
            setupCredentialService: interactive => interactiveValue = interactive,
            cancellationToken: TestContext.CancellationToken);

        exitCode.Should().Be(0);
        interactiveValue.Should().BeFalse();
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

        // ToolSearchCommand captures Reporter.Output at construction time, so the buffered
        // reporters must be installed before the command instance is created.
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

        public Task<IReadOnlyCollection<SearchResultPackage>> GetResult(
            NugetSearchApiParameter nugetSearchApiParameter,
            PackageSource source,
            CancellationToken cancellationToken)
        {
            beforeRequest?.Invoke();
            string sourceUrl = source.Source;
            RequestedSourceUrls.Add(sourceUrl);
            RequestedParameters.Add(nugetSearchApiParameter);
            RequestedCancellationTokens.Add(cancellationToken);

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
            CancellationToken cancellationToken)
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

    private sealed class EmptyPackageSearchResource : PackageSearchResource
    {
        public override bool SupportsPackageTypeFiltering => true;

        public override Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(
            string searchTerm,
            SearchFilter filters,
            int skip,
            int take,
            ILogger log,
            CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<IPackageSearchMetadata>>([]);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            DirectoryInfo = Directory.CreateTempSubdirectory("dotnet-tool-search-tests-");
        }

        public DirectoryInfo DirectoryInfo { get; }

        public string WriteNuGetConfigWithNoSources()
        {
            string configPath = Path.Combine(DirectoryInfo.FullName, "NuGet.config");
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
            return configPath;
        }

        public void Dispose()
        {
            try
            {
                DirectoryInfo.Delete(recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup; ignore failures caused by files still being handled by the OS.
            }
        }
    }
}
