// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class TemplateConstraintManagerTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        public TemplateConstraintManagerTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        [Fact]
        public async Task CanEvaluateConstraint()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-1"));
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-2"));

            var constraintManager = new TemplateConstraintManager(engineEnvironmentSettings);

            var success1 = await constraintManager.EvaluateConstraintAsync("test-1", "yes", default);
            var failure1 = await constraintManager.EvaluateConstraintAsync("test-1", "no", default);
            var notEvaluated1 = await constraintManager.EvaluateConstraintAsync("test-1", "not-valid", default);
            var success2 = await constraintManager.EvaluateConstraintAsync("test-2", "yes", default);

            Assert.Equal(TemplateConstraintResult.Status.Allowed, success1.EvaluationStatus);
            Assert.Null(success1.LocalizedErrorMessage);
            Assert.Null(success1.CallToAction);

            Assert.Equal(TemplateConstraintResult.Status.Restricted, failure1.EvaluationStatus);
            Assert.Equal("cannot run", failure1.LocalizedErrorMessage);
            Assert.Equal("do smth", failure1.CallToAction);

            Assert.Equal(TemplateConstraintResult.Status.NotEvaluated, notEvaluated1.EvaluationStatus);
            Assert.Equal("bad params", notEvaluated1.LocalizedErrorMessage);
            Assert.Null(notEvaluated1.CallToAction);

            Assert.Equal(TemplateConstraintResult.Status.Allowed, success2.EvaluationStatus);
            Assert.Null(success2.LocalizedErrorMessage);
            Assert.Null(success2.CallToAction);
        }

        [Fact]
        public async Task CanGetConstraints()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-1"));
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-2"));

            var constraintManager = new TemplateConstraintManager(engineEnvironmentSettings);

            var constraints = await constraintManager.GetConstraintsAsync();

            Assert.Equal(4, constraints.Count);
            Assert.Equal(new[] { "host", "os", "test-1", "test-2" }, constraints.Select(c => c.Type).OrderBy(t => t));
        }

        [Fact]
        public async Task CanGetConstraints_Filter()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-1"));
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-2"));

            var constraintManager = new TemplateConstraintManager(engineEnvironmentSettings);

            var templateInfo = A.Fake<ITemplateInfo>();
            A.CallTo(() => templateInfo.Constraints).Returns(new[]
            {
                new TemplateConstraintInfo("test-1", "yes")
            });

            var constraints = await constraintManager.GetConstraintsAsync(new[] { templateInfo }, default);

            Assert.Single(constraints);
            Assert.Equal("test-1", constraints.Single().Type);
        }

        [Fact]
        public async Task CanGetConstraints_WhenCreationFailed()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new FailingTestConstraintFactory("test-1"));
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-2"));

            var constraintManager = new TemplateConstraintManager(engineEnvironmentSettings);
            var constraints = await constraintManager.GetConstraintsAsync();

            Assert.Equal(3, constraints.Count);
            Assert.Equal(new[] { "host", "os", "test-2" }, constraints.Select(c => c.Type).OrderBy(t => t));
        }

        [Fact]
        public async Task CannotEvaluateConstraint_WhenCreationFailed()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new FailingTestConstraintFactory("test-1"));

            var constraintManager = new TemplateConstraintManager(engineEnvironmentSettings);

            var result = await constraintManager.EvaluateConstraintAsync("test-1", "yes", default);
            Assert.Equal(TemplateConstraintResult.Status.NotEvaluated, result.EvaluationStatus);
            Assert.Equal("The constraint 'test-1' failed to initialize: creation failed", result.LocalizedErrorMessage);
            Assert.Null(result.CallToAction);
        }

        [Fact]
        public async Task CanEvaluateConstraint_WhenOtherCreationFailed()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new FailingTestConstraintFactory("test-1"));
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-2"));

            var constraintManager = new TemplateConstraintManager(engineEnvironmentSettings);

            var result1 = await constraintManager.EvaluateConstraintAsync("test-1", "yes", default);
            Assert.Equal(TemplateConstraintResult.Status.NotEvaluated, result1.EvaluationStatus);
            Assert.Equal("The constraint 'test-1' failed to initialize: creation failed", result1.LocalizedErrorMessage);
            Assert.Null(result1.CallToAction);

            var success2 = await constraintManager.EvaluateConstraintAsync("test-2", "yes", default);

            Assert.Equal(TemplateConstraintResult.Status.Allowed, success2.EvaluationStatus);
            Assert.Null(success2.LocalizedErrorMessage);
            Assert.Null(success2.CallToAction);
        }

        [Fact]
        public async Task CanEvaluateConstraint_WhenOtherCreationStillRuns()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new LongRunningTestConstraintFactory("test-1"));
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-2"));

            var constraintManager = new TemplateConstraintManager(engineEnvironmentSettings);

            var success2 = await constraintManager.EvaluateConstraintAsync("test-2", "yes", default);

            Assert.Equal(TemplateConstraintResult.Status.Allowed, success2.EvaluationStatus);
            Assert.Null(success2.LocalizedErrorMessage);
            Assert.Null(success2.CallToAction);
        }

        [Fact]
        public async Task CanGetConstraints_DoesNotWaitForNotNeededConstraints()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new LongRunningTestConstraintFactory("test-1"));
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-2"));

            var templateInfo = A.Fake<ITemplateInfo>();
            A.CallTo(() => templateInfo.Constraints).Returns(new[]
            {
                new TemplateConstraintInfo("test-2", "yes")
            });

            IReadOnlyList<ITemplateConstraint>? constraints = null;
            var constraintsTask = Task.Run(async () =>
                {
                    var constraintManager = new TemplateConstraintManager(engineEnvironmentSettings);
                    constraints = await constraintManager.GetConstraintsAsync(new[] { templateInfo }, default);
                });
            var completedTask = await Task.WhenAny(constraintsTask, Task.Delay(10000));

            Assert.Equal(completedTask, constraintsTask);
            Assert.Equal(1, constraints?.Count);
            Assert.Equal("test-2", constraints?.Single().Type);
        }

        [Fact]
        public async Task CanEvaluateConstraints_Success()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-1"));
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-2"));

            var constraintManager = new TemplateConstraintManager(engineEnvironmentSettings);
            ITemplateInfo template = A.Fake<ITemplateInfo>();
            A.CallTo(() => template.Constraints).Returns(new[] { new TemplateConstraintInfo("test-1", "yes"), new TemplateConstraintInfo("test-2", "no") });

            var result = await constraintManager.EvaluateConstraintsAsync(new[] { template }, default);

            Assert.Equal(2, result.Single().Result.Count);
            Assert.Equal(template, result.Single().Template);
            Assert.Equal(TemplateConstraintResult.Status.Allowed, result.Single().Result.Single(r => r.ConstraintType == "test-1").EvaluationStatus);
            Assert.Equal(TemplateConstraintResult.Status.Restricted, result.Single().Result.Single(r => r.ConstraintType == "test-2").EvaluationStatus);
        }

        private class FailingTestConstraintFactory : ITemplateConstraintFactory
        {
            public FailingTestConstraintFactory(string type)
            {
                Type = type;
                Id = Guid.NewGuid();
            }

            public string Type { get; }

            public Guid Id { get; }

            public Task<ITemplateConstraint> CreateTemplateConstraintAsync(IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken)
            {
                throw new Exception("creation failed");
            }
        }

        private class LongRunningTestConstraintFactory : ITemplateConstraintFactory
        {
            public LongRunningTestConstraintFactory(string type)
            {
                Type = type;
                Id = Guid.NewGuid();
            }

            public string Type { get; }

            public Guid Id { get; }

            public async Task<ITemplateConstraint> CreateTemplateConstraintAsync(IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken)
            {
                await Task.Delay(30000);
                throw new Exception("creation failed");
            }
        }

    }
}
