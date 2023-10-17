// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Components;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Edge.Constraints;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class WorkloadConstraintTests
    {
        [Theory]
        [InlineData(new[] { "workload", "workloadAA" }, false)]
        [InlineData(new[] { "workload", "workloadA" }, true)]
        [InlineData(new[] { "workloadB", "workload" }, true)]
        [InlineData(new string[0], false)]
        public async Task Evaluate_ArrayOfVersions(IReadOnlyList<string> workloads, bool allowed)
        {
            var config = new
            {
                identity = "test-constraint-01",
                constraints = new
                {
                    specVersions = new
                    {
                        type = "workload",
                        args = new[] { "workloadA", "workloadB" }
                    }
                }
            };

            var configModel = TemplateConfigModel.FromJObject(JObject.FromObject(config));
            IWorkloadsInfoProvider workloadInfoProvider = new WorkloadsInfoProviderMock(workloads); //A.Fake<IWorkloadsInfoProvider>();
            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Components.OfType<IWorkloadsInfoProvider>()).Returns(new[] { workloadInfoProvider });
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new WorkloadConstraintFactory() });

            var constraintManager = new TemplateConstraintManager(settings);

            //A.CallTo(() => workloadInfoProvider
            //    .GetInstalledWorkloadsAsync(A<CancellationToken>._))
            //    .Returns(Task.FromResult(workloads.Select(s => new WorkloadInfo(s, $"D:{s}"))));

            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, default);
            Assert.Equal(allowed ? TemplateConstraintResult.Status.Allowed : TemplateConstraintResult.Status.Restricted, evaluateResult.EvaluationStatus);
        }

        [Fact]
        public async Task Evaluate_MultipleConflictingProviders()
        {
            var config = new
            {
                identity = "test-constraint-01",
                constraints = new
                {
                    specVersions = new
                    {
                        type = "workload",
                        args = new[] { "workloadA", "workloadB" }
                    }
                }
            };

            var configModel = TemplateConfigModel.FromJObject(JObject.FromObject(config));
            IWorkloadsInfoProvider workloadInfoProviderA = A.Fake<IWorkloadsInfoProvider>();
            A.CallTo(() => workloadInfoProviderA
                    .GetInstalledWorkloadsAsync(A<CancellationToken>._))
                .Returns(Task.FromResult(new[] { "workload", "workloadA" }.Select(s => new WorkloadInfo(s, $"D:{s}"))));

            IWorkloadsInfoProvider workloadInfoProviderB = A.Fake<IWorkloadsInfoProvider>();
            A.CallTo(() => workloadInfoProviderB
                    .GetInstalledWorkloadsAsync(A<CancellationToken>._))
                .Returns(Task.FromResult(new[] { "workload", "workloadA", "workloadB" }.Select(s => new WorkloadInfo(s, $"D:{s}"))));

            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Components.OfType<IWorkloadsInfoProvider>()).Returns(new[] { workloadInfoProviderA, workloadInfoProviderB });
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new WorkloadConstraintFactory() });
            List<(LogLevel, string)> messagesCollection = new();
            ILogger logger = new InMemoryLoggerProvider(messagesCollection).CreateLogger("x");
            A.CallTo(() => settings.Host.Logger).Returns(logger);

            var constraintManager = new TemplateConstraintManager(settings);

            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, default);
            Assert.Equal(TemplateConstraintResult.Status.NotEvaluated, evaluateResult.EvaluationStatus);
            Assert.Equal(0, messagesCollection.Count(t => t.Item1 >= LogLevel.Warning));
            Assert.StartsWith("The constraint 'workload' failed to initialize", evaluateResult.LocalizedErrorMessage);
        }

        [Fact]
        public async Task Evaluate_MultipleDuplicateProviders()
        {
            var config = new
            {
                identity = "test-constraint-01",
                constraints = new
                {
                    specVersions = new
                    {
                        type = "workload",
                        args = new[] { "workloadA", "workloadB" }
                    }
                }
            };

            var configModel = TemplateConfigModel.FromJObject(JObject.FromObject(config));
            IWorkloadsInfoProvider workloadInfoProviderA = A.Fake<IWorkloadsInfoProvider>();
            A.CallTo(() => workloadInfoProviderA
                    .GetInstalledWorkloadsAsync(A<CancellationToken>._))
                .Returns(Task.FromResult(new[] { "workload", "workloadA" }.Select(s => new WorkloadInfo(s, $"D:{s}"))));

            IWorkloadsInfoProvider workloadInfoProviderB = A.Fake<IWorkloadsInfoProvider>();
            A.CallTo(() => workloadInfoProviderB
                    .GetInstalledWorkloadsAsync(A<CancellationToken>._))
                .Returns(Task.FromResult(new[] { "workloadA", "workload" }.Select(s => new WorkloadInfo(s, $"D:{s}"))));

            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Components.OfType<IWorkloadsInfoProvider>()).Returns(new[] { workloadInfoProviderA, workloadInfoProviderB });
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new WorkloadConstraintFactory() });
            List<(LogLevel, string)> messagesCollection = new();
            ILogger logger = new InMemoryLoggerProvider(messagesCollection).CreateLogger("x");
            A.CallTo(() => settings.Host.Logger).Returns(logger);

            var constraintManager = new TemplateConstraintManager(settings);

            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, default);
            Assert.Equal(TemplateConstraintResult.Status.NotEvaluated, evaluateResult.EvaluationStatus);
            Assert.Equal(0, messagesCollection.Count(t => t.Item1 >= LogLevel.Warning));
            Assert.StartsWith("The constraint 'workload' failed to initialize", evaluateResult.LocalizedErrorMessage);
        }

        // This is a workaround in a weird bug with FakeItEasy - more details in SdkVersionConstraintTests.cs
        private class WorkloadsInfoProviderMock : IWorkloadsInfoProvider
        {
            private readonly IEnumerable<WorkloadInfo> _res;

            public WorkloadsInfoProviderMock(IEnumerable<string> res) => _res = res.Select(s => new WorkloadInfo(s, $"D:{s}"));

            public Guid Id { get; }

            public Task<IEnumerable<WorkloadInfo>> GetInstalledWorkloadsAsync(CancellationToken token) => Task.FromResult(_res);

            public string ProvideConstraintRemedySuggestion(IReadOnlyList<string> supportedWorkloads) => "Sample CTA";
        }
    }
}
