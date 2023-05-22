// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Tests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

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
            using (TextReader schemaFileStream = File.OpenText(@"SchemaTests/template.json"))
            {
                JSchema schema = JSchema.Load(new JsonTextReader(schemaFileStream));
                using (TextReader jsonFileStream = File.OpenText(testFile))
                {
                    using (JsonTextReader jsonReader = new JsonTextReader(jsonFileStream))
                    {
                        JObject templateConfig = (JObject)JToken.ReadFrom(jsonReader);
                        Assert.True(
                            templateConfig.IsValid(schema, out IList<string> errors),
                            "The JSON file is not valid against the schema" +
                            Environment.NewLine +
                            string.Join(Environment.NewLine, errors));
                    }
                }
            }
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

