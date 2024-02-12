// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class OSConstraintTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _sharedSettings;

        public OSConstraintTests(EnvironmentSettingsHelper helper)
        {
            _sharedSettings = helper.CreateEnvironment();
        }

        [Fact]
        public async Task CanReadStringConfiguration()
        {
            var config = new
            {
                identity = "test",
                constraints = new
                {
                    winOnly = new
                    {
                        type = "os",
                        args = "Windows"
                    }
                }
            };

            var configModel = TemplateConfigModel.FromJObject(JObject.FromObject(config));
            var constraintManager = new TemplateConstraintManager(_sharedSettings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, default);

            Assert.Equal(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), evaluateResult.EvaluationStatus == TemplateConstraintResult.Status.Allowed);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Equal($"Running template on {RuntimeInformation.OSDescription} is not supported, supported OS is/are: {OSPlatform.Windows}.", evaluateResult.LocalizedErrorMessage);
            }
            else
            {
                Assert.Null(evaluateResult.LocalizedErrorMessage);
            }
            Assert.Null(evaluateResult.CallToAction);
        }

        [Fact]
        public async Task CanReadArrayConfiguration()
        {
            var config = new
            {
                identity = "test",
                constraints = new
                {
                    winOnly = new
                    {
                        type = "os",
                        args = new[] { "Windows", "Linux" }
                    }
                }
            };

            var configModel = TemplateConfigModel.FromJObject(JObject.FromObject(config));
            var constraintManager = new TemplateConstraintManager(_sharedSettings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, default);

            var pass = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

            Assert.Equal(pass, evaluateResult.EvaluationStatus == TemplateConstraintResult.Status.Allowed);

            if (!pass)
            {
                Assert.Equal($"Running template on {RuntimeInformation.OSDescription} is not supported, supported OS is/are: {OSPlatform.Windows}, {OSPlatform.Linux}.", evaluateResult.LocalizedErrorMessage);
            }
            else
            {
                Assert.Null(evaluateResult.LocalizedErrorMessage);
            }
            Assert.Null(evaluateResult.CallToAction);
        }
    }
}
