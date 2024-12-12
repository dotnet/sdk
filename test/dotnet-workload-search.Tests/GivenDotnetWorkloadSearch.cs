// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Workload.Install.Tests;
using Microsoft.DotNet.Workloads.Workload.Search;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Search.Tests
{
    public class GivenDotnetWorkloadSearch : SdkTest
    {
        private readonly BufferedReporter _reporter;
        private readonly IEnumerable<WorkloadResolver.WorkloadInfo> _availableWorkloads =
            new List<WorkloadResolver.WorkloadInfo>()
            {
                CreateWorkloadInfo("mock-workload-1"),
                CreateWorkloadInfo("mock-workload-2"),
                CreateWorkloadInfo("mock-workload-3"),
                CreateWorkloadInfo("fake-workload-1"),
                CreateWorkloadInfo("fake-workload-2", "Fake description 2")
            };

        static WorkloadResolver.WorkloadInfo CreateWorkloadInfo(string id, string description = null)
            => new(new WorkloadId(id), description);

        public GivenDotnetWorkloadSearch(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
        }

        [Theory]
        [InlineData("--invalidArgument")]
        [InlineData("notAVersion")]
        [InlineData("1.2")] // too short
        [InlineData("1.2.3.4.5")] // too long
        [InlineData("1.2-3.4")] // numbers after [-, +] don't count
        public void GivenInvalidArgumentToWorkloadSearchVersionItFailsCleanly(string argument)
        {
            _reporter.Clear();
            var parseResult = Parser.Instance.Parse($"dotnet workload search version {argument}");
            var workloadResolver = new MockWorkloadResolver(Enumerable.Empty<WorkloadResolver.WorkloadInfo>());
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetPath: null, "9.0.100", workloadResolver);
            var command = () => new WorkloadSearchVersionsCommand(parseResult, _reporter, workloadResolverFactory);
            command.Should().Throw<CommandParsingException>();
        }

        [Fact]
        public void GivenNoWorkloadsAreInstalledSearchIsEmpty()
        {
            _reporter.Clear();
            var parseResult = Parser.Instance.Parse("dotnet workload search");
            var workloadResolver = new MockWorkloadResolver(Enumerable.Empty<WorkloadResolver.WorkloadInfo>());
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetPath: null, "6.0.100", workloadResolver);
            var command = new WorkloadSearchCommand(parseResult, _reporter, workloadResolverFactory);
            command.Execute();

            _reporter.Lines.Count.Should().Be(4, because: "Output should have header and no values.");
        }

        [Fact]
        public void GivenNoStubIsProvidedSearchShowsAllWorkloads()
        {
            _reporter.Clear();
            var parseResult = Parser.Instance.Parse("dotnet workload search");
            var workloadResolver = new MockWorkloadResolver(_availableWorkloads);
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetPath: null, "6.0.100", workloadResolver);
            var command = new WorkloadSearchCommand(parseResult, _reporter, workloadResolverFactory);
            command.Execute();

            var output = string.Join(" ", _reporter.Lines);
            foreach (var workload in _availableWorkloads)
            {
                output.Contains(workload.Id.ToString()).Should().BeTrue();
                if (workload.Description != null)
                {
                    output.Contains(workload.Description).Should().BeTrue();
                }
            }
        }

        [Fact]
        public void GivenDetailedVerbositySearchShowsAllColumns()
        {
            _reporter.Clear();
            var parseResult = Parser.Instance.Parse("dotnet workload search -v d");
            var workloadResolver = new MockWorkloadResolver(_availableWorkloads);
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetPath: null, "6.0.100", workloadResolver);
            var command = new WorkloadSearchCommand(parseResult, _reporter, workloadResolverFactory);
            command.Execute();

            var output = string.Join(" ", _reporter.Lines);
            foreach (var workload in _availableWorkloads)
            {
                output.Contains(workload.Id.ToString()).Should().BeTrue();
                if (workload.Description != null)
                {
                    output.Contains(workload.Description).Should().BeTrue();
                }
            }
        }

        [Fact]
        public void GivenStubIsProvidedSearchShowsAllMatchingWorkloads()
        {
            _reporter.Clear();
            var parseResult = Parser.Instance.Parse("dotnet workload search mock");
            var workloadResolver = new MockWorkloadResolver(_availableWorkloads);
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetPath: null, "6.0.100", workloadResolver);
            var command = new WorkloadSearchCommand(parseResult, _reporter, workloadResolverFactory);
            command.Execute();

            var output = string.Join(" ", _reporter.Lines);
            var expectedWorkloads = _availableWorkloads.Take(3);
            foreach (var workload in expectedWorkloads)
            {
                output.Contains(workload.Id.ToString()).Should().BeTrue();
                if (workload.Description != null)
                {
                    output.Contains(workload.Description).Should().BeTrue();
                }
            }
        }

        [Fact]
        public void GivenSearchResultsAreOrdered()
        {
            _reporter.Clear();
            var parseResult = Parser.Instance.Parse("dotnet workload search");
            var workloadResolver = new MockWorkloadResolver(_availableWorkloads);
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetPath: null, "6.0.100", workloadResolver);
            var command = new WorkloadSearchCommand(parseResult, _reporter, workloadResolverFactory);
            command.Execute();

            _reporter.Lines[3].Should().Contain("fake-workload-1");
            _reporter.Lines[4].Should().Contain("fake-workload-2");
            _reporter.Lines[5].Should().Contain("mock-workload-1");
            _reporter.Lines[6].Should().Contain("mock-workload-2");
            _reporter.Lines[7].Should().Contain("mock-workload-3");
        }

        [Fact]
        public void GivenWorkloadSearchItSearchesDescription()
        {
            _reporter.Clear();
            var parseResult = Parser.Instance.Parse("dotnet workload search description");
            var workloadResolver = new MockWorkloadResolver(_availableWorkloads);
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetPath: null, "6.0.100", workloadResolver);
            var command = new WorkloadSearchCommand(parseResult, _reporter, workloadResolverFactory);
            command.Execute();

            _reporter.Lines.Count.Should().Be(5);
            _reporter.Lines[3].Should().Contain("fake-workload-2");
        }
    }
}
