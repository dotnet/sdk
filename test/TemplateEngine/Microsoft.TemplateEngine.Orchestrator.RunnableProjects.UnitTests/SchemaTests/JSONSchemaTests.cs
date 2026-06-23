// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.TemplateEngine.Tests;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.SchemaTests
{
    public class JSONSchemaTests : TestBase
    {
        [Theory(DisplayName = nameof(IsJSONSchemaValid))]
        [InlineData(@"SchemaTests/BasicTest.json")]
        [InlineData(@"SchemaTests/GeneratorTest.json")]
        [InlineData(@"SchemaTests/StarterWebTest.json")]
        [InlineData(@"SchemaTests/PostActionTest.json")]
        [InlineData(@"SchemaTests/SymbolsTest.json")]
        [InlineData(@"SchemaTests/MultiValueChoiceTest.json")]
        [InlineData(@"SchemaTests/ConstraintsTest.json")]
        [InlineData(@"SchemaTests/ConditionalParametersTest.json")]
        public void IsJSONSchemaValid(string testFile)
        {
            string schemaContent = File.ReadAllText(@"SchemaTests/template.json");
            var schema = JsonSchema.FromText(schemaContent);

            string jsonContent = File.ReadAllText(testFile);
            var jsonNode = JsonNode.Parse(jsonContent, null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

            var result = schema.Evaluate(jsonNode, new EvaluationOptions { OutputFormat = OutputFormat.List });

            var errors = result.Details?
                .Where(d => !d.IsValid && d.Errors != null)
                .SelectMany(d => d.Errors!.Values)
                .ToList() ?? new List<string>();

            Assert.True(
                result.IsValid,
                "The JSON file is not valid against the schema" +
                Environment.NewLine +
                string.Join(Environment.NewLine, errors));
        }

        private static readonly string JsonLocation = Path.Combine(".template.config", "template.json");

        public static IEnumerable<object?[]> GetAllTemplates()
        {
            //those templates are intentionally wrong
            string[] exceptions = new[] { "MissingIdentity", "MissingMandatoryConfig" };

            return Directory.EnumerateFiles(TestTemplatesLocation, "template.json", SearchOption.AllDirectories)
                .Where(s => s.Contains(".template.config"))
                .Where(s => !exceptions.Any(e => s.Contains(e)))
                .Select(s => s.Remove(s.Length - JsonLocation.Length).Remove(0, TestTemplatesLocation.Length).Trim(Path.DirectorySeparatorChar))
                .Select(s => new object?[] { s });
        }

        [Theory]
        [MemberData(nameof(GetAllTemplates))]
        public void TestAllTestTemplatesHaveValidJson(string testTemplateName)
        {
            string testFile = Path.Combine(TestTemplatesLocation, testTemplateName, JsonLocation);

            IsJSONSchemaValid(testFile);
        }

        public static IEnumerable<object?[]> GetAllTemplateSamples()
        {
            //those templates are intentionally wrong
            //string[] exceptions = new[] { "MissingIdentity", "MissingMandatoryConfig" };

            return Directory.EnumerateFiles(SampleTemplatesLocation, "template.json", SearchOption.AllDirectories)
                .Where(s => s.Contains(".template.config"))
                //.Where(s => !exceptions.Any(e => s.Contains(e)))
                .Select(s => s.Remove(s.Length - JsonLocation.Length).Remove(0, SampleTemplatesLocation.Length).Trim(Path.DirectorySeparatorChar))
                .Select(s => new object?[] { s });
        }

        [Theory]
        [MemberData(nameof(GetAllTemplateSamples))]
        public void TestAllSampleTemplatesHaveValidJson(string testTemplateName)
        {
            string testFile = Path.Combine(SampleTemplatesLocation, testTemplateName, JsonLocation);
            IsJSONSchemaValid(testFile);
        }
    }
}

