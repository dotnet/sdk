// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    [TestClass]
    public class TemplateConstraintManagerTests
    {
        public TestContext TestContext { get; set; } = null!;

        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper(NullMessageSink.Instance);

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        [TestMethod]
        public async Task CanEvaluateConstraint()
        {
            var engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-1"));
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-2"));

            var constraintManager = new TemplateConstraintManager(engineEnvironmentSettings);

            var success1 = await constraintManager.EvaluateConstraintAsync("test-1", "yes", TestContext.CancellationToken);
            var failure1 = await constraintManager.EvaluateConstraintAsync("test-1", "no", TestContext.CancellationToken);
            var notEvaluated1 = await constraintManager.EvaluateConstraintAsync("test-1", "not-valid", TestContext.CancellationToken);
            var success2 = await constraintManager.EvaluateConstraintAsync("test-2", "yes", TestContext.CancellationToken);

            Assert.AreEqual(TemplateConstraintResult.Status.Allowed, success1.EvaluationStatus);
            Assert.IsNull(success1.LocalizedErrorMessage);
            Assert.IsNull(success1.CallToAction);

            Assert.AreEqual(TemplateConstraintResult.Status.Restricted, failure1.EvaluationStatus);
            Assert.AreEqual("cannot run", failure1.LocalizedErrorMessage);
            Assert.AreEqual("do smth", failure1.CallToAction);

            Assert.AreEqual(TemplateConstraintResult.Status.NotEvaluated, notEvaluated1.EvaluationStatus);
            Assert.AreEqual("bad params", notEvaluated1.LocalizedErrorMessage);
            Assert.IsNull(notEvaluated1.CallToAction);

            Assert.AreEqual(TemplateConstraintResult.Status.Allowed, success2.EvaluationStatus);
            Assert.IsNull(success2.LocalizedErrorMessage);
            Assert.IsNull(success2.CallToAction);
        }

        [TestMethod]
        public async Task CanGetConstraints()
        {
            var engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-1"));
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-2"));

            var constraintManager = new TemplateConstraintManager(engineEnvironmentSettings);

            var constraints = await constraintManager.GetConstraintsAsync(cancellationToken: TestContext.CancellationToken);

            Assert.HasCount(4, constraints);
            Assert.AreSequenceEqual(new[] { "host", "os", "test-1", "test-2" }, constraints.Select(c => c.Type).OrderBy(t => t));
        }

        [TestMethod]
        public async Task CanGetConstraints_Filter()
        {
            var engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-1"));
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-2"));

            var constraintManager = new TemplateConstraintManager(engineEnvironmentSettings);

            var templateInfo = A.Fake<ITemplateInfo>();
            A.CallTo(() => templateInfo.Constraints).Returns(new[]
            {
                new TemplateConstraintInfo("test-1", "yes")
            });

            var constraints = await constraintManager.GetConstraintsAsync(new[] { templateInfo }, TestContext.CancellationToken);

            Assert.ContainsSingle(constraints);
            Assert.AreEqual("test-1", constraints.Single().Type);
        }

        [TestMethod]
        public async Task CanGetConstraints_WhenCreationFailed()
        {
            var engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new FailingTestConstraintFactory("test-1"));
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-2"));

            var constraintManager = new TemplateConstraintManager(engineEnvironmentSettings);
            var constraints = await constraintManager.GetConstraintsAsync(cancellationToken: TestContext.CancellationToken);

            Assert.HasCount(3, constraints);
            Assert.AreSequenceEqual(new[] { "host", "os", "test-2" }, constraints.Select(c => c.Type).OrderBy(t => t));
        }

        [TestMethod]
        public async Task CannotEvaluateConstraint_WhenCreationFailed()
        {
            var engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new FailingTestConstraintFactory("test-1"));

            var constraintManager = new TemplateConstraintManager(engineEnvironmentSettings);

            var result = await constraintManager.EvaluateConstraintAsync("test-1", "yes", TestContext.CancellationToken);
            Assert.AreEqual(TemplateConstraintResult.Status.NotEvaluated, result.EvaluationStatus);
            Assert.AreEqual("The constraint 'test-1' failed to initialize: creation failed", result.LocalizedErrorMessage);
            Assert.IsNull(result.CallToAction);
        }

        [TestMethod]
        public async Task CanEvaluateConstraint_WhenOtherCreationFailed()
        {
            var engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new FailingTestConstraintFactory("test-1"));
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-2"));

            var constraintManager = new TemplateConstraintManager(engineEnvironmentSettings);

            var result1 = await constraintManager.EvaluateConstraintAsync("test-1", "yes", TestContext.CancellationToken);
            Assert.AreEqual(TemplateConstraintResult.Status.NotEvaluated, result1.EvaluationStatus);
            Assert.AreEqual("The constraint 'test-1' failed to initialize: creation failed", result1.LocalizedErrorMessage);
            Assert.IsNull(result1.CallToAction);

            var success2 = await constraintManager.EvaluateConstraintAsync("test-2", "yes", TestContext.CancellationToken);

            Assert.AreEqual(TemplateConstraintResult.Status.Allowed, success2.EvaluationStatus);
            Assert.IsNull(success2.LocalizedErrorMessage);
            Assert.IsNull(success2.CallToAction);
        }

        [TestMethod]
        public async Task CanEvaluateConstraint_WhenOtherCreationStillRuns()
        {
            var engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new LongRunningTestConstraintFactory("test-1"));
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-2"));

            var constraintManager = new TemplateConstraintManager(engineEnvironmentSettings);

            var success2 = await constraintManager.EvaluateConstraintAsync("test-2", "yes", TestContext.CancellationToken);

            Assert.AreEqual(TemplateConstraintResult.Status.Allowed, success2.EvaluationStatus);
            Assert.IsNull(success2.LocalizedErrorMessage);
            Assert.IsNull(success2.CallToAction);
        }

        [TestMethod]
        public async Task CanGetConstraints_DoesNotWaitForNotNeededConstraints()
        {
            var engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
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
                    constraints = await constraintManager.GetConstraintsAsync(new[] { templateInfo }, TestContext.CancellationToken);
                }, TestContext.CancellationToken);
            var completedTask = await Task.WhenAny(constraintsTask, Task.Delay(10000, TestContext.CancellationToken));

            Assert.AreEqual(completedTask, constraintsTask);
            Assert.AreEqual(1, constraints?.Count);
            Assert.AreEqual("test-2", constraints?.Single().Type);
        }

        [TestMethod]
        public async Task CanEvaluateConstraints_Success()
        {
            var engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-1"));
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateConstraintFactory), new TestConstraintFactory("test-2"));

            var constraintManager = new TemplateConstraintManager(engineEnvironmentSettings);
            ITemplateInfo template = A.Fake<ITemplateInfo>();
            A.CallTo(() => template.Constraints).Returns(new[] { new TemplateConstraintInfo("test-1", "yes"), new TemplateConstraintInfo("test-2", "no") });

            var result = await constraintManager.EvaluateConstraintsAsync(new[] { template }, TestContext.CancellationToken);

            Assert.HasCount(2, result.Single().Result);
            Assert.AreEqual(template, result.Single().Template);
            Assert.AreEqual(TemplateConstraintResult.Status.Allowed, result.Single().Result.Single(r => r.ConstraintType == "test-1").EvaluationStatus);
            Assert.AreEqual(TemplateConstraintResult.Status.Restricted, result.Single().Result.Single(r => r.ConstraintType == "test-2").EvaluationStatus);
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
