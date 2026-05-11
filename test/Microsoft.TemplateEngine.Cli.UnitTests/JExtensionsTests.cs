// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias TemplateEngineCli;

using System.Text.Json.Nodes;

namespace Microsoft.TemplateEngine.Cli.UnitTests;

public class JExtensionsTests
{
    [Fact]
    public void ToInt32ParsesStringPropertyValues()
    {
        JsonObject json = JsonNode.Parse("""
            {
              "precedence": "100"
            }
            """)!.AsObject();

        TemplateEngineCli::Microsoft.TemplateEngine.JExtensions.ToInt32(json, "precedence").Should().Be(100);
    }
}
