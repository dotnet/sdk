// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Tool.Search;
using Microsoft.DotNet.Cli.NugetSearch;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace dotnet.Tests.ToolSearchTests;

[TestClass]
public class NugetSearchApiRequestTests
{
    private static readonly PackageSource Source = new("https://example.test/v3/index.json");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GetResultUsesPackageSearchResourceAndMapsTypedMetadata()
    {
        var packageMetadata = new Mock<IPackageSearchMetadata>(MockBehavior.Strict);
        packageMetadata.SetupGet(metadata => metadata.Identity)
            .Returns(new PackageIdentity("sample.tool", NuGetVersion.Parse("2.0.0")));
        packageMetadata.SetupGet(metadata => metadata.Description).Returns("description");
        packageMetadata.SetupGet(metadata => metadata.Summary).Returns("summary");
        packageMetadata.SetupGet(metadata => metadata.Tags).Returns("tag1 tag2");
        packageMetadata.SetupGet(metadata => metadata.Authors).Returns("Author One, Author Two");
        packageMetadata.SetupGet(metadata => metadata.DownloadCount).Returns(12L);
        packageMetadata.SetupGet(metadata => metadata.PrefixReserved).Returns(true);
        packageMetadata.Setup(metadata => metadata.GetVersionsAsync()).ReturnsAsync(
            [
                new VersionInfo(NuGetVersion.Parse("2.0.0"), 10),
                new VersionInfo(NuGetVersion.Parse("1.0.0"), 2),
            ]);

        var resource = new CapturingPackageSearchResource([packageMetadata.Object]);
        var request = new NugetToolSearchApiRequest(
            (source, _) =>
            {
                source.Should().BeSameAs(Source);
                return Task.FromResult<PackageSearchResource?>(resource);
            });
        var parameter = new NugetSearchApiParameter("sample", skip: 3, take: 4, prerelease: true);

        IReadOnlyCollection<SearchResultPackage> result = await request.GetResult(parameter, Source, CancellationToken.None);

        resource.SearchTerm.Should().Be("sample");
        resource.Filter.Should().NotBeNull();
        resource.Filter!.IncludePrerelease.Should().BeTrue();
        resource.Filter.PackageType.Should().Be(PackageType.DotnetTool.Name);
        resource.Skip.Should().Be(3);
        resource.Take.Should().Be(4);

        SearchResultPackage package = result.Should().ContainSingle().Subject;
        package.Id.ToString().Should().Be("sample.tool");
        package.LatestVersion.Should().Be("2.0.0");
        package.Description.Should().Be("description");
        package.Summary.Should().Be("summary");
        package.Tags.Should().Equal("tag1", "tag2");
        package.Authors.Should().Equal("Author One", "Author Two");
        package.TotalDownloads.Should().Be(12);
        package.Verified.Should().BeTrue();
        package.Versions.Select(version => (version.Version, version.Downloads))
            .Should().Equal(("2.0.0", 10L), ("1.0.0", 2L));
    }

    [TestMethod]
    public async Task GetResultReportsSourceWithoutPackageSearchResource()
    {
        var request = new NugetToolSearchApiRequest(
            (_, _) => Task.FromResult<PackageSearchResource?>(null));

        Func<Task> act = () => request.GetResult(new NugetSearchApiParameter("sample"), Source, CancellationToken.None);

        (await act.Should().ThrowAsync<NugetSearchApiRequestException>())
            .WithMessage($"*{Source.Source}*{nameof(PackageSearchResource)}*");
    }

    [TestMethod]
    public async Task GetResultTranslatesPackageSearchResourceFailures()
    {
        var resource = new CapturingPackageSearchResource(
            new HttpRequestException("the source did not respond"));
        var request = new NugetToolSearchApiRequest(
            (_, _) => Task.FromResult<PackageSearchResource?>(resource));

        Func<Task> act = () => request.GetResult(new NugetSearchApiParameter("sample"), Source, CancellationToken.None);

        await act.Should().ThrowAsync<NugetSearchApiRequestException>()
            .WithMessage("the source did not respond");
    }

    [TestMethod]
    public async Task GetResultTranslatesUnsupportedPackageTypeFiltering()
    {
        var resource = new CapturingPackageSearchResource(
            new NotSupportedException("package type filtering is not supported"));
        var request = new NugetToolSearchApiRequest(
            (_, _) => Task.FromResult<PackageSearchResource?>(resource));

        Func<Task> act = () => request.GetResult(new NugetSearchApiParameter("sample"), Source, CancellationToken.None);

        await act.Should().ThrowAsync<NugetSearchApiRequestException>()
            .WithMessage("package type filtering is not supported");
    }

    [TestMethod]
    public async Task GetResultRejectsSourcesWithoutPackageTypeFiltering()
    {
        var request = new NugetToolSearchApiRequest(
            (_, _) => Task.FromResult<PackageSearchResource?>(new UnsupportedPackageTypeFilteringResource()));

        Func<Task> act = () => request.GetResult(new NugetSearchApiParameter("sample"), Source, CancellationToken.None);

        await act.Should().ThrowAsync<NugetSearchApiRequestException>()
            .WithMessage($"*{Source.Source}*package type*");
    }

    [TestMethod]
    public async Task GetResultPropagatesCallerCancellation()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new NugetToolSearchApiRequest(
            async (_, cancellationToken) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return null;
            });
        using CancellationTokenSource cancellation = new();

        Task action = request.GetResult(new NugetSearchApiParameter("sample"), Source, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        cancellation.Cancel();

        await FluentActions.Awaiting(() => action).Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class CapturingPackageSearchResource : PackageSearchResource
    {
        private readonly IEnumerable<IPackageSearchMetadata>? _results;
        private readonly Exception? _exception;

        public CapturingPackageSearchResource(IEnumerable<IPackageSearchMetadata> results)
        {
            _results = results;
        }

        public CapturingPackageSearchResource(Exception exception)
        {
            _exception = exception;
        }

        public string? SearchTerm { get; private set; }

        public SearchFilter? Filter { get; private set; }

        public int Skip { get; private set; }

        public int Take { get; private set; }

        public override bool SupportsPackageTypeFiltering => true;

        public override Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(
            string searchTerm,
            SearchFilter filters,
            int skip,
            int take,
            ILogger log,
            CancellationToken cancellationToken)
        {
            SearchTerm = searchTerm;
            Filter = filters;
            Skip = skip;
            Take = take;

            return _exception is null
                ? Task.FromResult(_results!)
                : Task.FromException<IEnumerable<IPackageSearchMetadata>>(_exception);
        }
    }

    private sealed class UnsupportedPackageTypeFilteringResource : PackageSearchResource
    {
        public override bool SupportsPackageTypeFiltering => false;

        public override Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(
            string searchTerm,
            SearchFilter filters,
            int skip,
            int take,
            ILogger log,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("Search should not be called.");
    }
}
