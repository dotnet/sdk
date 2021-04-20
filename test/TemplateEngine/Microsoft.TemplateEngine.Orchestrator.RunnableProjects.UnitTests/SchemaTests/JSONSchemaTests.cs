// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;
using System.IO;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.SchemaTests
{
    public class JSONSchemaTests
    {
        [Theory(DisplayName = nameof(IsJSONSchemaValid))]
        [InlineData(@"SchemaTests/BasicTest.json")]
        [InlineData(@"SchemaTests/GeneratorTest.json")]
        [InlineData(@"SchemaTests/StarterWebTest.json")]
        [InlineData(@"SchemaTests/PostActionTest.json")]
        [InlineData(@"SchemaTests/SymbolsTest.json")]
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
                        Assert.True(templateConfig.IsValid(schema, out IList<string> errors),
                            "The JSON file is not valid against the schema" +
                            Environment.NewLine +
                            string.Join(Environment.NewLine, errors));
                    }
                }

            }
        }
    }
}
