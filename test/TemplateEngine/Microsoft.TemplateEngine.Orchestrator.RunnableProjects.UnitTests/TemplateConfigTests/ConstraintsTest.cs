// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class ConstraintsTest
    {
        [Fact]
        public void CanReadConstraintDefinition()
        {
            var json = new
            {
                identity = "test",
                constraints = new
                {
                    one = new
                    {
                        type = "con1",
                        args = "arg"
                    },
                    two = new
                    {
                        type = "con2",
                        args = new[]
                        {
                            "one", "two", "three"
                        }
                    },
                    three = new
                    {
                        type = "con3",
                        args = new
                        {
                            one = "one",
                            two = "two",
                        }
                    },
                    four = new
                    {
                        type = "con4"
                    },
                }
            };

            var model = TemplateConfigModel.FromJObject(JObject.FromObject(json));

            Assert.Equal(4, model.Constraints.Count);
            Assert.Equal("con1", model.Constraints[0].Type);
            Assert.Equal("con2", model.Constraints[1].Type);
            Assert.Equal("con3", model.Constraints[2].Type);
            Assert.Equal("con4", model.Constraints[3].Type);

            Assert.Equal("\"arg\"", model.Constraints[0].Args);
            Assert.Equal("""["one","two","three"]""", model.Constraints[1].Args);
            Assert.Equal(/*lang=json,strict*/ """{"one":"one","two":"two"}""", model.Constraints[2].Args);
            Assert.Null(model.Constraints[3].Args);
        }

        [Fact]
        public void CannotReadConstraint_WhenTypeIsNotSet()
        {
            var json = new
            {
                identity = "test",
                constraints = new
                {
                    one = new
                    {
                        args = "arg"
                    }
                }
            };

            List<(LogLevel, string)> loggedMessages = new List<(LogLevel, string)>();
            InMemoryLoggerProvider loggerProvider = new InMemoryLoggerProvider(loggedMessages);
            var model = TemplateConfigModel.FromJObject(JObject.FromObject(json), loggerProvider.CreateLogger("test"));
            Assert.Empty(model.Constraints);
            Assert.Single(loggedMessages);
            Assert.Equal($"Constraint definition '{JObject.FromObject(new { args = "arg" })}' does not contain mandatory property 'type'.", loggedMessages.Single().Item2);
        }

        [Fact]
        public void CannotReadConstraint_WhenArrayIsDenfined()
        {
            var json = new
            {
                identity = "test",
                constraints = new
                {
                    one = new[] { "one", "two" }
                }
            };

            List<(LogLevel, string)> loggedMessages = new List<(LogLevel, string)>();
            InMemoryLoggerProvider loggerProvider = new InMemoryLoggerProvider(loggedMessages);
            var model = TemplateConfigModel.FromJObject(JObject.FromObject(json), loggerProvider.CreateLogger("test"));
            Assert.Empty(model.Constraints);
            Assert.Single(loggedMessages);
            Assert.Equal("'constraints' should contain objects.", loggedMessages.Single().Item2);
        }
    }
}
