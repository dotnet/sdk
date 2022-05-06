// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class OSConstraintTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _sharedSettings;

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

            var configModel = SimpleConfigModel.FromJObject(JObject.FromObject(config));
            var constraintManager = new TemplateConstraintManager(_sharedSettings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, default).ConfigureAwait(false);

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

            var configModel = SimpleConfigModel.FromJObject(JObject.FromObject(config));
            var constraintManager = new TemplateConstraintManager(_sharedSettings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, default).ConfigureAwait(false);

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
