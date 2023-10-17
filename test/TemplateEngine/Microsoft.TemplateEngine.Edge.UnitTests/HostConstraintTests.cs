// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Edge.Constraints;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class HostConstraintTests
    {
        [Fact]
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

            var configModel = TemplateConfigModel.FromJObject(JObject.FromObject(config));
            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Host.HostIdentifier).Returns("host1");
            A.CallTo(() => settings.Host.Version).Returns("2.0.0");
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new HostConstraintFactory() });

            var constraintManager = new TemplateConstraintManager(settings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, default);
            Assert.Equal(TemplateConstraintResult.Status.Allowed, evaluateResult.EvaluationStatus);
        }

        [Fact]
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

            var configModel = TemplateConfigModel.FromJObject(JObject.FromObject(config));
            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Host.HostIdentifier).Returns("host2");
            A.CallTo(() => settings.Host.Version).Returns("2.0.0");
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new HostConstraintFactory() });

            var constraintManager = new TemplateConstraintManager(settings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, default);
            Assert.Equal(TemplateConstraintResult.Status.Restricted, evaluateResult.EvaluationStatus);
            Assert.Equal("Running template on host2 (version: 2.0.0) is not supported, supported hosts is/are: host1, host2(1.0.0), host3([1.0.0-*]).", evaluateResult.LocalizedErrorMessage);
            Assert.Null(evaluateResult.CallToAction);

        }

        [Fact]
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

            var configModel = TemplateConfigModel.FromJObject(JObject.FromObject(config));

            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Host.HostIdentifier).Returns("host3");
            A.CallTo(() => settings.Host.Version).Returns("2.0.0");
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new HostConstraintFactory() });

            var constraintManager = new TemplateConstraintManager(settings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, default);
            Assert.Equal(TemplateConstraintResult.Status.Allowed, evaluateResult.EvaluationStatus);
        }

        [Fact]
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

            var configModel = TemplateConfigModel.FromJObject(JObject.FromObject(config));

            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Host.HostIdentifier).Returns("host3");
            A.CallTo(() => settings.Host.Version).Returns("2.0.0");
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new HostConstraintFactory() });

            var constraintManager = new TemplateConstraintManager(settings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, default);
            Assert.Equal(TemplateConstraintResult.Status.NotEvaluated, evaluateResult.EvaluationStatus);
            Assert.Equal("'{\"hostName\":\"host2\",\"version\":\"1.0.0\"}' is not a valid JSON array.", evaluateResult.LocalizedErrorMessage);
            Assert.Equal("Check the constraint configuration in template.json.", evaluateResult.CallToAction);
        }

        [Theory]
        [InlineData("1.0.0", "1.0", true)]
        [InlineData("(, 1.0.0]", "1.0", true)]
        [InlineData("(, 1.0.0]", "2.0", false)]
        [InlineData("[1.0.0]", "1.0.0", true)]
        [InlineData("[1.0.0]", "2.0.0", false)]
        [InlineData("[1.0, 2.0-preview3]", "2.0-preview1", true)]
        [InlineData("[1.0, 2.0-preview3]", "2.0-preview4", false)]
        [InlineData("[1.0, 2.0-preview3]", "2.0", false)]
        [InlineData("[1.0, 2.0]", "1.2", true)]
        [InlineData("[1.0, 2.0]", "2.2", false)]
        //legacy format
        [InlineData("[1.0-*]", "2.2", true)]
        [InlineData("[*-2.0]", "2.2", false)]
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

            var configModel = TemplateConfigModel.FromJObject(JObject.FromObject(config));
            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Host.HostIdentifier).Returns("host1");
            A.CallTo(() => settings.Host.Version).Returns(hostVersion);
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new HostConstraintFactory() });

            var constraintManager = new TemplateConstraintManager(settings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, default);
            if (expectedResult)
            {
                Assert.Equal(TemplateConstraintResult.Status.Allowed, evaluateResult.EvaluationStatus);
            }
            else
            {
                Assert.Equal(TemplateConstraintResult.Status.Restricted, evaluateResult.EvaluationStatus);
            }
        }

        [Theory]
        [InlineData("host1", "fallback|other", "1.1", true)]
        [InlineData("host1", "fallback|other", "2.1", false)]
        [InlineData("host2", "fallback|other", "2.1", true)]
        [InlineData("host2", "fallback|other", "3.1", false)]
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

            var configModel = TemplateConfigModel.FromJObject(JObject.FromObject(config));
            IEngineEnvironmentSettings settings = A.Fake<IEngineEnvironmentSettings>();
            A.CallTo(() => settings.Host.HostIdentifier).Returns(hostName);
            A.CallTo(() => settings.Host.Version).Returns(hostVersion);
            A.CallTo(() => settings.Host.FallbackHostTemplateConfigNames).Returns(fallbackHostNames.Split('|'));
            A.CallTo(() => settings.Components.OfType<ITemplateConstraintFactory>()).Returns(new[] { new HostConstraintFactory() });

            var constraintManager = new TemplateConstraintManager(settings);
            var evaluateResult = await constraintManager.EvaluateConstraintAsync(configModel.Constraints.Single().Type, configModel.Constraints.Single().Args, default);
            if (expectedResult)
            {
                Assert.Equal(TemplateConstraintResult.Status.Allowed, evaluateResult.EvaluationStatus);
            }
            else
            {
                Assert.Equal(TemplateConstraintResult.Status.Restricted, evaluateResult.EvaluationStatus);
            }
        }
    }
}
