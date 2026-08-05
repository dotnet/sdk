// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    [TestClass]
    public class ConstraintsTest
    {
        [TestMethod]
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

            var model = TemplateConfigModel.FromJObject(JsonNode.Parse(JsonSerializer.Serialize(json))!.AsObject());

            Assert.HasCount(4, model.Constraints);
            Assert.AreEqual("con1", model.Constraints[0].Type);
            Assert.AreEqual("con2", model.Constraints[1].Type);
            Assert.AreEqual("con3", model.Constraints[2].Type);
            Assert.AreEqual("con4", model.Constraints[3].Type);

            Assert.AreEqual("\"arg\"", model.Constraints[0].Args);
            Assert.AreEqual("""["one","two","three"]""", model.Constraints[1].Args);
            Assert.AreEqual(/*lang=json,strict*/ """{"one":"one","two":"two"}""", model.Constraints[2].Args);
            Assert.IsNull(model.Constraints[3].Args);
        }

        [TestMethod]
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
            var model = TemplateConfigModel.FromJObject(JsonNode.Parse(JsonSerializer.Serialize(json))!.AsObject(), loggerProvider.CreateLogger("test"));
            Assert.IsEmpty(model.Constraints);
            Assert.ContainsSingle(loggedMessages);
            Assert.AreEqual($"Constraint definition '{JsonNode.Parse(JsonSerializer.Serialize(new { args = "arg" }))!.ToJsonString()}' does not contain mandatory property 'type'.", loggedMessages.Single().Item2);
        }

        [TestMethod]
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
            var model = TemplateConfigModel.FromJObject(JsonNode.Parse(JsonSerializer.Serialize(json))!.AsObject(), loggerProvider.CreateLogger("test"));
            Assert.IsEmpty(model.Constraints);
            Assert.ContainsSingle(loggedMessages);
            Assert.AreEqual("'constraints' should contain objects.", loggedMessages.Single().Item2);
        }
    }
}
