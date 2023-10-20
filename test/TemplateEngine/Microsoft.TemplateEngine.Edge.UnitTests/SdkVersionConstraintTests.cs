// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Components;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Edge.Constraints;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class SdkVersionConstraintTests
    {
        [Theory]
        [InlineData("1.2.3", true)]
        [InlineData("1.2.3-dev", true)]
        [InlineData("1.2.4", false)]
        [InlineData("4.5.3-dev", false)]
        [InlineData("4.5.3", true)]
        [InlineData("4.5.0", true)]
        [InlineData("4.6.0", false)]
        public async Task Evaluate_ArrayOfVersions(string sdkVersion, bool allowed)
        {
            var config = new
            {
                identity = "test-constraint-01",
                constraints = new
                {
                    specVersions = new
                    {
                        type = "sdk-version",
                        args = new[] { "1.2.3-*", "4.5.*" }
                    }
                }
            };

            var configModel = TemplateConfigModel.FromJObject(JObject.FromObject(config));
            ISdkInfoProvider sdkInfoProvider = new SdkInfoProviderMock(sdkVersion); //A.Fake<ISdkInfoProvider>();
            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Components.OfType<ISdkInfoProvider>()).Returns(new[] { sdkInfoProvider });
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new SdkVersionConstraintFactory() });

            var constraintManager = new TemplateConstraintManager(settings);

            //Workaround needed
            //A.CallTo(() => sdkInfoProvider.GetVersionAsync(A<CancellationToken>._)).Returns(Task.Run(() => sdkVersion));

            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, default);
            Assert.Equal(allowed ? TemplateConstraintResult.Status.Allowed : TemplateConstraintResult.Status.Restricted, evaluateResult.EvaluationStatus);
        }

        [Theory]
        [InlineData("1.2.2", false)]
        [InlineData("1.2.3", true)]
        [InlineData("1.2.3-dev", true)]
        [InlineData("1.2.4", true)]
        [InlineData("4.5.3-dev", false)]
        [InlineData("4.5.3", false)]
        [InlineData("4.5.0", true)]
        [InlineData("4.4.0-dev", true)]
        public async Task Evaluate_SingleVersionRange(string sdkVersion, bool allowed)
        {
            var config = new
            {
                identity = "test-constraint-01",
                constraints = new
                {
                    specVersions = new
                    {
                        type = "sdk-version",
                        args = "(1.2.3-*, 4.5]"
                    }
                }
            };

            var configModel = TemplateConfigModel.FromJObject(JObject.FromObject(config));
            ISdkInfoProvider sdkInfoProvider = new SdkInfoProviderMock(sdkVersion); //A.Fake<ISdkInfoProvider>();
            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Components.OfType<ISdkInfoProvider>()).Returns(new[] { sdkInfoProvider });
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new SdkVersionConstraintFactory() });

            var constraintManager = new TemplateConstraintManager(settings);

            //Workaround needed
            //A.CallTo(() => sdkInfoProvider.GetVersionAsync(A<CancellationToken>._)).Returns(t);

            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, default);
            Assert.Equal(allowed ? TemplateConstraintResult.Status.Allowed : TemplateConstraintResult.Status.Restricted, evaluateResult.EvaluationStatus);
        }

        [Theory]
        [InlineData("1.1.1", new[] { "0.1.2", "1.2.3", "3.4.5" }, true)]
        [InlineData("1.1.1", new[] { "0.1.2", "1.2.2", "3.4.5" }, false)]
        [InlineData("1.1.1", new[] { "0.1.2", "1.2.3", "4.5.6" }, true)]
        public async Task Evaluate_AlternativeInstalledVersions(string sdkVersion, IReadOnlyList<string> installedVersions, bool hasAlternativeInstalled)
        {
            var config = new
            {
                identity = "test-constraint-01",
                constraints = new
                {
                    specVersions = new
                    {
                        type = "sdk-version",
                        args = new[] { "1.2.3-*", "4.5.*" }
                    }
                }
            };

            var configModel = TemplateConfigModel.FromJObject(JObject.FromObject(config));
            ISdkInfoProvider sdkInfoProvider = new SdkInfoProviderMock(sdkVersion, installedVersions); //A.Fake<ISdkInfoProvider>();
            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Components.OfType<ISdkInfoProvider>()).Returns(new[] { sdkInfoProvider });
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new SdkVersionConstraintFactory() });

            var constraintManager = new TemplateConstraintManager(settings);

            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, default);
            Assert.Equal(TemplateConstraintResult.Status.Restricted, evaluateResult.EvaluationStatus);
            Assert.StartsWith(
                hasAlternativeInstalled
                    ? "Sample CTA with alternatives"
                    : "Sample CTA without alternatives",
                evaluateResult.CallToAction);
        }

        // This is a workaround in a weird bug with FakeItEasy, when using:
        //   ISdkInfoProvider sdkInfoProvider = A.Fake<ISdkInfoProvider>();
        //   A.CallTo(() => sdkInfoProvider.GetVersionAsync(A<CancellationToken>._)).Returns(Task.FromResult(sdkVersion));
        // The task then randomly returns empty string in the target code
        private class SdkInfoProviderMock : ISdkInfoProvider
        {
            private readonly string _res;
            private readonly IEnumerable<string> _installed;

            public SdkInfoProviderMock(string res, IEnumerable<string>? installed = null)
            {
                _res = res;
                _installed = installed ?? new[] { _res };
            }

            public Guid Id { get; }

            public Task<string> GetCurrentVersionAsync(CancellationToken cancellationToken) => Task.FromResult(_res);

            public Task<IEnumerable<string>> GetInstalledVersionsAsync(CancellationToken cancellationToken) => Task.FromResult(_installed);

            public string ProvideConstraintRemedySuggestion(
                IReadOnlyList<string> supportedVersions,
                IReadOnlyList<string> viableInstalledVersions) => viableInstalledVersions.Any()
                ? "Sample CTA with alternatives"
                : "Sample CTA without alternatives";
        }
    }
}
