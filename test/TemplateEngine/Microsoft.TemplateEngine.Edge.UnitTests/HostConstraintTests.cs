// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Edge.Constraints;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    [TestClass]
    public class HostConstraintTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public async Task CanReadConfiguration_WithoutVersion()
        {
            var config = new
            {
                identity = "test",
                constraints = new
                {
                    host = new
                    {
                        type = "host",
                        args = new[]
                        {
                            new
                            {
                                hostName = "host1",
                                version = string.Empty
                            },
                            new
                            {
                                hostName = "host2",
                                version = "1.0.0"
                            },
                            new
                            {
                                hostName = "host3",
                                version = "[1.0.0-*]"
                            },

                        }
                    }
                }
            };

            var configModel = TemplateConfigModel.FromJObject(JsonNode.Parse(JsonSerializer.Serialize(config))!.AsObject());
            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Host.HostIdentifier).Returns("host1");
            A.CallTo(() => settings.Host.Version).Returns("2.0.0");
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new HostConstraintFactory() });

            var constraintManager = new TemplateConstraintManager(settings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, TestContext.CancellationToken);
            Assert.AreEqual(TemplateConstraintResult.Status.Allowed, evaluateResult.EvaluationStatus);
        }

        [TestMethod]
        public async Task CanReadConfiguration_ExactVersion()
        {
            var config = new
            {
                identity = "test",
                constraints = new
                {
                    host = new
                    {
                        type = "host",
                        args = new[]
                        {
                            new
                            {
                                hostName = "host1",
                                version = string.Empty
                            },
                            new
                            {
                                hostName = "host2",
                                version = "1.0.0"
                            },
                            new
                            {
                                hostName = "host3",
                                version = "[1.0.0-*]"
                            },

                        }
                    }
                }
            };

            var configModel = TemplateConfigModel.FromJObject(JsonNode.Parse(JsonSerializer.Serialize(config))!.AsObject());
            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Host.HostIdentifier).Returns("host2");
            A.CallTo(() => settings.Host.Version).Returns("2.0.0");
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new HostConstraintFactory() });

            var constraintManager = new TemplateConstraintManager(settings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, TestContext.CancellationToken);
            Assert.AreEqual(TemplateConstraintResult.Status.Restricted, evaluateResult.EvaluationStatus);
            Assert.AreEqual("Running template on host2 (version: 2.0.0) is not supported, supported hosts is/are: host1, host2(1.0.0), host3([1.0.0-*]).", evaluateResult.LocalizedErrorMessage);
            Assert.IsNull(evaluateResult.CallToAction);

        }

        [TestMethod]
        public async Task CanReadConfiguration_VersionRange()
        {
            var config = new
            {
                identity = "test",
                constraints = new
                {
                    host = new
                    {
                        type = "host",
                        args = new[]
                        {
                            new
                            {
                                hostName = "host1",
                                version = string.Empty
                            },
                            new
                            {
                                hostName = "host2",
                                version = "1.0.0"
                            },
                            new
                            {
                                hostName = "host3",
                                version = "[1.0.0-*]"
                            },

                        }
                    }
                }
            };

            var configModel = TemplateConfigModel.FromJObject(JsonNode.Parse(JsonSerializer.Serialize(config))!.AsObject());

            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Host.HostIdentifier).Returns("host3");
            A.CallTo(() => settings.Host.Version).Returns("2.0.0");
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new HostConstraintFactory() });

            var constraintManager = new TemplateConstraintManager(settings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, TestContext.CancellationToken);
            Assert.AreEqual(TemplateConstraintResult.Status.Allowed, evaluateResult.EvaluationStatus);
        }

        [TestMethod]
        public async Task FailsOnWrongConfiguration()
        {
            var config = new
            {
                identity = "test",
                constraints = new
                {
                    host = new
                    {
                        type = "host",
                        args =
                            new
                            {
                                hostName = "host2",
                                version = "1.0.0"
                            }
                    }
                }
            };

            var configModel = TemplateConfigModel.FromJObject(JsonNode.Parse(JsonSerializer.Serialize(config))!.AsObject());

            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Host.HostIdentifier).Returns("host3");
            A.CallTo(() => settings.Host.Version).Returns("2.0.0");
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new HostConstraintFactory() });

            var constraintManager = new TemplateConstraintManager(settings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, TestContext.CancellationToken);
            Assert.AreEqual(TemplateConstraintResult.Status.NotEvaluated, evaluateResult.EvaluationStatus);
            Assert.AreEqual("'{\"hostName\":\"host2\",\"version\":\"1.0.0\"}' is not a valid JSON array.", evaluateResult.LocalizedErrorMessage);
            Assert.AreEqual("Check the constraint configuration in template.json.", evaluateResult.CallToAction);
        }

        [TestMethod]
        [DataRow("1.0.0", "1.0", true)]
        [DataRow("(, 1.0.0]", "1.0", true)]
        [DataRow("(, 1.0.0]", "2.0", false)]
        [DataRow("[1.0.0]", "1.0.0", true)]
        [DataRow("[1.0.0]", "2.0.0", false)]
        [DataRow("[1.0, 2.0-preview3]", "2.0-preview1", true)]
        [DataRow("[1.0, 2.0-preview3]", "2.0-preview4", false)]
        [DataRow("[1.0, 2.0-preview3]", "2.0", false)]
        [DataRow("[1.0, 2.0]", "1.2", true)]
        [DataRow("[1.0, 2.0]", "2.2", false)]
        //legacy format
        [DataRow("[1.0-*]", "2.2", true)]
        [DataRow("[*-2.0]", "2.2", false)]
        public async Task CanProcessDifferentVersions(string configuredVersion, string hostVersion, bool expectedResult)
        {
            var config = new
            {
                identity = "test",
                constraints = new
                {
                    host = new
                    {
                        type = "host",
                        args = new[]
                        {
                            new
                            {
                                hostName = "host1",
                                version = configuredVersion
                            }
                        }
                    }
                }
            };

            var configModel = TemplateConfigModel.FromJObject(JsonNode.Parse(JsonSerializer.Serialize(config))!.AsObject());
            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Host.HostIdentifier).Returns("host1");
            A.CallTo(() => settings.Host.Version).Returns(hostVersion);
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new HostConstraintFactory() });

            var constraintManager = new TemplateConstraintManager(settings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, TestContext.CancellationToken);
            if (expectedResult)
            {
                Assert.AreEqual(TemplateConstraintResult.Status.Allowed, evaluateResult.EvaluationStatus);
            }
            else
            {
                Assert.AreEqual(TemplateConstraintResult.Status.Restricted, evaluateResult.EvaluationStatus);
            }
        }

        [TestMethod]
        [DataRow("host1", "fallback|other", "1.1", true)]
        [DataRow("host1", "fallback|other", "2.1", false)]
        [DataRow("host2", "fallback|other", "2.1", true)]
        [DataRow("host2", "fallback|other", "3.1", false)]
        public async Task CanProcessDifferentHostNames(string hostName, string fallbackHostNames, string hostVersion, bool expectedResult)
        {
            var config = new
            {
                identity = "test",
                constraints = new
                {
                    host = new
                    {
                        type = "host",
                        args = new[]
                        {
                            new
                            {
                                hostName = "host1",
                                version = "[1.0, 2.0]"
                            },
                            new
                            {
                                hostName = "fallback",
                                version = "[2.0, 3.0]"
                            },
                        }
                    }
                }
            };

            var configModel = TemplateConfigModel.FromJObject(JsonNode.Parse(JsonSerializer.Serialize(config))!.AsObject());
            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Host.HostIdentifier).Returns(hostName);
            A.CallTo(() => settings.Host.Version).Returns(hostVersion);
            A.CallTo(() => settings.Host.FallbackHostTemplateConfigNames).Returns(fallbackHostNames.Split('|'));
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new HostConstraintFactory() });

            var constraintManager = new TemplateConstraintManager(settings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, TestContext.CancellationToken);
            if (expectedResult)
            {
                Assert.AreEqual(TemplateConstraintResult.Status.Allowed, evaluateResult.EvaluationStatus);
            }
            else
            {
                Assert.AreEqual(TemplateConstraintResult.Status.Restricted, evaluateResult.EvaluationStatus);
            }
        }
    }
}
