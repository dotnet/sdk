// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    [TestClass]
    public class OSConstraintTests
    {
        public TestContext TestContext { get; set; } = null!;

        private static EnvironmentSettingsHelper s_helper = null!;
        private readonly IEngineEnvironmentSettings _sharedSettings;

        public OSConstraintTests()
        {
            _sharedSettings = s_helper.CreateEnvironment();
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_helper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_helper?.Dispose();

        [TestMethod]
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

            var configModel = TemplateConfigModel.FromJObject(JsonNode.Parse(JsonSerializer.Serialize(config))!.AsObject());
            var constraintManager = new TemplateConstraintManager(_sharedSettings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, TestContext.CancellationToken);

            Assert.AreEqual(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), evaluateResult.EvaluationStatus == TemplateConstraintResult.Status.Allowed);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.AreEqual($"Running template on {RuntimeInformation.OSDescription} is not supported, supported OS is/are: {OSPlatform.Windows}.", evaluateResult.LocalizedErrorMessage);
            }
            else
            {
                Assert.IsNull(evaluateResult.LocalizedErrorMessage);
            }
            Assert.IsNull(evaluateResult.CallToAction);
        }

        [TestMethod]
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

            var configModel = TemplateConfigModel.FromJObject(JsonNode.Parse(JsonSerializer.Serialize(config))!.AsObject());
            var constraintManager = new TemplateConstraintManager(_sharedSettings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, TestContext.CancellationToken);

            var pass = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

            Assert.AreEqual(pass, evaluateResult.EvaluationStatus == TemplateConstraintResult.Status.Allowed);

            if (!pass)
            {
                Assert.AreEqual($"Running template on {RuntimeInformation.OSDescription} is not supported, supported OS is/are: {OSPlatform.Windows}, {OSPlatform.Linux}.", evaluateResult.LocalizedErrorMessage);
            }
            else
            {
                Assert.IsNull(evaluateResult.LocalizedErrorMessage);
            }
            Assert.IsNull(evaluateResult.CallToAction);
        }
    }
}
